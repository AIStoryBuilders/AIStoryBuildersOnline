using AIStoryBuilders.AI;
using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AIStoryBuilders.Services
{
    /// <summary>
    /// Conversational chat orchestrator that answers story questions using the
    /// in-memory knowledge graph and can perform preview/confirm mutations via the
    /// <see cref="IGraphMutationService"/>.
    ///
    /// Tool invocation uses a JSON protocol in the model output:
    /// <code>```tool
    /// { "name": "GetCharacter", "args": { "name": "Alice" } }
    /// ```</code>
    /// The service intercepts those blocks, runs the tool, and appends the result
    /// to the conversation before asking the model to continue.
    /// </summary>
    public class StoryChatService : IStoryChatService
    {
        private readonly OrchestratorMethods _orchestrator;
        private readonly IGraphQueryService _query;
        private readonly IGraphMutationService _mutation;
        private readonly IGraphBuilder _builder;
        private readonly AIStoryBuildersService _storyService;
        private readonly LogService _log;

        private IChatClient _client;
        private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
        private Story _activeStory;
        private const int MaxToolIterations = 4;

        public StoryChatService(
            OrchestratorMethods orchestrator,
            IGraphQueryService query,
            IGraphMutationService mutation,
            IGraphBuilder builder,
            AIStoryBuildersService storyService,
            LogService log)
        {
            _orchestrator = orchestrator;
            _query = query;
            _mutation = mutation;
            _builder = builder;
            _storyService = storyService;
            _log = log;
        }

        public void SetActiveStory(Story story)
        {
            _activeStory = story;
        }

        public void RefreshClient()
        {
            _client = null;
        }

        public void ClearSession(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        public ConversationSession GetOrCreateSession(string sessionId)
        {
            return _sessions.GetOrAdd(sessionId, id => new ConversationSession
            {
                SessionId = id,
                StoryTitle = _activeStory?.Title ?? ""
            });
        }

        public async IAsyncEnumerable<string> SendMessageAsync(string userMessage, string sessionId)
        {
            var session = GetOrCreateSession(sessionId);
            session.Messages.Add(new ChatDisplayMessage { Role = "user", Content = userMessage });

            // Ensure graph is available
            if (_activeStory != null && (GraphState.Current == null || GraphState.IsDirty ||
                !string.Equals(GraphState.Current.StoryTitle, _activeStory.Title, StringComparison.OrdinalIgnoreCase)))
            {
                await _storyService.RefreshGraphIfDirtyAsync(_activeStory.Title, _builder);
            }

            if (_client == null)
            {
                await _orchestrator.EnsureSettingsLoadedAsync();
                _client = _orchestrator.CreateChatClient();
            }

            var systemPrompt = BuildSystemPrompt();
            int iterations = 0;
            string finalSoFar = "";
            var messages = BuildMessages(systemPrompt, session);

            while (iterations++ < MaxToolIterations)
            {
                var assistantChunks = new StringBuilder();

                await foreach (var update in _client.GetStreamingResponseAsync(messages))
                {
                    var text = update?.Text;
                    if (string.IsNullOrEmpty(text)) continue;
                    assistantChunks.Append(text);
                    yield return text;
                }

                string assistantText = assistantChunks.ToString();
                finalSoFar += assistantText;

                // Look for tool call JSON block
                var toolCall = ExtractToolCall(assistantText);
                if (toolCall == null)
                {
                    break;
                }

                string toolResult = await InvokeToolAsync(toolCall.Name, toolCall.Args);
                await _log.WriteToLogAsync($"ChatTool {toolCall.Name} args={JsonConvert.SerializeObject(toolCall.Args)} result_len={toolResult?.Length ?? 0}");

                messages.Add(new ChatMessage(ChatRole.Assistant, assistantText));
                messages.Add(new ChatMessage(ChatRole.User, $"Tool result for {toolCall.Name}:\n```json\n{toolResult}\n```\nPlease continue the response for the user based on this data."));
                yield return "\n\n";
            }

            session.Messages.Add(new ChatDisplayMessage { Role = "assistant", Content = finalSoFar });
        }

        private List<ChatMessage> BuildMessages(string systemPrompt, ConversationSession session)
        {
            var list = new List<ChatMessage> { new ChatMessage(ChatRole.System, systemPrompt) };
            foreach (var m in session.Messages)
            {
                var role = m.Role switch
                {
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User
                };
                list.Add(new ChatMessage(role, m.Content ?? ""));
            }
            return list;
        }

        private string BuildSystemPrompt()
        {
            var details = _query.GetStoryDetails();
            var sb = new StringBuilder();
            sb.AppendLine("You are a story assistant for the AIStoryBuilders application.");
            sb.AppendLine("You have access to a knowledge graph of the story and can call tools.");
            sb.AppendLine();
            sb.AppendLine("Story details:");
            sb.AppendLine($"- Title: {details?.Title}");
            sb.AppendLine($"- Synopsis: {details?.Synopsis}");
            sb.AppendLine($"- Characters: {details?.CharacterCount}, Locations: {details?.LocationCount}, Timelines: {details?.TimelineCount}, Chapters: {details?.ChapterCount}, Paragraphs: {details?.ParagraphCount}");
            sb.AppendLine();
            sb.AppendLine("To invoke a tool, emit a single fenced code block with language `tool` containing JSON:");
            sb.AppendLine("```tool");
            sb.AppendLine("{ \"name\": \"<toolName>\", \"args\": { ... } }");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Read tools (safe):");
            sb.AppendLine("- GetCharacter(name), ListCharacters(), GetLocation(name), ListLocations(),");
            sb.AppendLine("  GetTimeline(name), ListTimelines(), GetChapter(title), ListChapters(),");
            sb.AppendLine("  GetParagraph(chapter, index), GetRelationships(name),");
            sb.AppendLine("  GetAppearances(characterName), GetLocationUsage(locationName),");
            sb.AppendLine("  GetInteractions(characterName), FindOrphans(),");
            sb.AppendLine("  GetCharacterArc(characterName), GetLocationEvents(locationName),");
            sb.AppendLine("  GetGraphSummary(), GetStoryDetails(), ListAttributes(parentType, parentName),");
            sb.AppendLine("  GetTimelineContext(timelineName, chapterSequence, paragraphSequence)");
            sb.AppendLine();
            sb.AppendLine("Write tools (preview + confirm). Always call first with confirmed=false to obtain a summary, relay it to the user, and only call with confirmed=true after explicit user approval:");
            sb.AppendLine("- RenameCharacter(oldName, newName, confirmed)");
            sb.AppendLine("- UpdateCharacterBackground(name, type, description, timeline, confirmed)");
            sb.AppendLine("- AddCharacter(name, role, backstory, confirmed)");
            sb.AppendLine("- DeleteCharacter(name, confirmed)");
            sb.AppendLine("- AddLocation(name, description, confirmed)");
            sb.AppendLine("- UpdateLocationDescription(name, description, timeline, confirmed)");
            sb.AppendLine("- DeleteLocation(name, confirmed)");
            sb.AppendLine("- AddTimeline(name, description, start, end, confirmed)");
            sb.AppendLine("- UpdateWorldFacts(facts, confirmed)");
            sb.AppendLine();
            sb.AppendLine("When you have finished, respond in Markdown without a tool block.");
            return sb.ToString();
        }

        private sealed class ToolCall
        {
            public string Name { get; set; }
            public Dictionary<string, object> Args { get; set; } = new();
        }

        private static readonly Regex ToolBlockRegex = new(
            @"```tool\s*([\s\S]*?)```", RegexOptions.IgnoreCase);

        private static ToolCall ExtractToolCall(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = ToolBlockRegex.Match(text);
            if (!m.Success) return null;
            var payload = m.Groups[1].Value.Trim();
            try
            {
                var jObj = JObject.Parse(payload);
                var name = jObj["name"]?.ToString();
                if (string.IsNullOrEmpty(name)) return null;
                var args = new Dictionary<string, object>();
                if (jObj["args"] is JObject argsObj)
                {
                    foreach (var p in argsObj.Properties())
                    {
                        args[p.Name] = p.Value?.ToObject<object>();
                    }
                }
                return new ToolCall { Name = name, Args = args };
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> InvokeToolAsync(string name, Dictionary<string, object> args)
        {
            try
            {
                object result = name switch
                {
                    "GetCharacter" => _query.GetCharacter(A(args, "name")),
                    "ListCharacters" => _query.ListCharacters(),
                    "GetLocation" => _query.GetLocation(A(args, "name")),
                    "ListLocations" => _query.ListLocations(),
                    "GetTimeline" => _query.GetTimeline(A(args, "name")),
                    "ListTimelines" => _query.ListTimelines(),
                    "GetChapter" => _query.GetChapter(A(args, "title")),
                    "ListChapters" => _query.ListChapters(),
                    "GetParagraph" => _query.GetParagraph(A(args, "chapter"), I(args, "index")),
                    "GetRelationships" => _query.GetRelationships(A(args, "name")),
                    "GetAppearances" => _query.GetAppearances(A(args, "characterName")),
                    "GetLocationUsage" => _query.GetLocationUsage(A(args, "locationName")),
                    "GetInteractions" => _query.GetInteractions(A(args, "characterName")),
                    "FindOrphans" => _query.FindOrphans(),
                    "GetCharacterArc" => _query.GetCharacterArc(A(args, "characterName")),
                    "GetLocationEvents" => _query.GetLocationEvents(A(args, "locationName")),
                    "GetGraphSummary" => _query.GetGraphSummary(),
                    "GetStoryDetails" => _query.GetStoryDetails(),
                    "ListAttributes" => _query.ListAttributes(A(args, "parentType"), A(args, "parentName")),
                    "GetTimelineContext" => _query.GetTimelineContext(A(args, "timelineName"), I(args, "chapterSequence"), I(args, "paragraphSequence")),
                    "RenameCharacter" => await _mutation.RenameCharacterAsync(A(args, "oldName"), A(args, "newName"), B(args, "confirmed")),
                    "UpdateCharacterBackground" => await _mutation.UpdateCharacterBackgroundAsync(A(args, "name"), A(args, "type"), A(args, "description"), A(args, "timeline"), B(args, "confirmed")),
                    "AddCharacter" => await _mutation.AddCharacterAsync(A(args, "name"), A(args, "role"), A(args, "backstory"), B(args, "confirmed")),
                    "DeleteCharacter" => await _mutation.DeleteCharacterAsync(A(args, "name"), B(args, "confirmed")),
                    "AddLocation" => await _mutation.AddLocationAsync(A(args, "name"), A(args, "description"), B(args, "confirmed")),
                    "UpdateLocationDescription" => await _mutation.UpdateLocationDescriptionAsync(A(args, "name"), A(args, "description"), A(args, "timeline"), B(args, "confirmed")),
                    "DeleteLocation" => await _mutation.DeleteLocationAsync(A(args, "name"), B(args, "confirmed")),
                    "AddTimeline" => await _mutation.AddTimelineAsync(A(args, "name"), A(args, "description"), A(args, "start"), A(args, "end"), B(args, "confirmed")),
                    "UpdateWorldFacts" => await _mutation.UpdateWorldFactsAsync(A(args, "facts"), B(args, "confirmed")),
                    _ => new { error = $"Unknown tool '{name}'." }
                };

                // After a mutation, refresh the graph if dirty.
                if (GraphState.IsDirty && _activeStory != null)
                {
                    await _storyService.RefreshGraphIfDirtyAsync(_activeStory.Title, _builder);
                }

                return JsonConvert.SerializeObject(result ?? new { }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync($"InvokeToolAsync {name}: {ex.Message}");
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }

        private static string A(Dictionary<string, object> args, string key)
            => args != null && args.TryGetValue(key, out var v) && v != null ? v.ToString() : "";

        private static int I(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var v) || v == null) return 0;
            return int.TryParse(v.ToString(), out var i) ? i : 0;
        }

        private static bool B(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var v) || v == null) return false;
            return bool.TryParse(v.ToString(), out var b) && b;
        }
    }
}
