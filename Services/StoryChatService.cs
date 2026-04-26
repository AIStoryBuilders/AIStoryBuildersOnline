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

        /// <summary>
        /// Sentinel chunk yielded between tool-loop iterations so the UI can
        /// reset its streaming buffer. Prose emitted by the model *before* a
        /// tool call is scratch reasoning and must not appear in the final
        /// bubble alongside the post-tool answer (otherwise the user sees
        /// two contradictory answers concatenated together).
        /// </summary>
        public const string IterationResetSentinel = "\u001E__ITERATION_RESET__\u001E";

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
            bool appliedAnyUpdate = false;

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

                // Look for tool call JSON block
                var toolCall = ExtractToolCall(assistantText);
                if (toolCall == null)
                {
                    // No tool call → this iteration's prose IS the final answer.
                    // Only the final iteration contributes to the persisted bubble,
                    // so a pre-tool draft from an earlier iteration cannot leak into
                    // the saved conversation history.
                    finalSoFar += assistantText;
                    break;
                }

                // Draft iteration that ended with a tool call. Anything the model
                // wrote before/around the tool fence is *scratch reasoning*: it
                // must not be shown to the user or persisted alongside the final
                // answer. Tell the UI to discard whatever it streamed during this
                // iteration before the next iteration starts streaming in.
                yield return IterationResetSentinel;

                string toolResult = await InvokeToolAsync(toolCall.Name, toolCall.Args);
                if (IsMutationTool(toolCall.Name) && B(toolCall.Args, "confirmed") && !LooksLikeToolError(toolResult))
                {
                    appliedAnyUpdate = true;
                }
                await _log.WriteToLogAsync($"ChatTool {toolCall.Name} args={TruncateArgsForLog(toolCall.Args)} result_len={toolResult?.Length ?? 0}");

                // Send the assistant's tool-call turn back so the model has its own
                // context, then deliver the tool result as authoritative ground
                // truth (Tool role where supported, fallback to User).
                //
                // CRITICAL: only re-inject the tool fence itself, NOT the surrounding
                // draft prose. If we replay the full draft (which often contains a
                // pre-tool guess such as a hallucinated paragraph quote) the model
                // will faithfully re-emit that guess in the next iteration before
                // stating the real tool-backed answer, producing two contradictory
                // quotes in one bubble.
                var toolFenceOnly = ExtractToolFenceOnly(assistantText) ?? assistantText;
                messages.Add(new ChatMessage(ChatRole.Assistant, toolFenceOnly));
                messages.Add(new ChatMessage(
                    ChatRole.Tool,
                    $"Tool result for {toolCall.Name} (authoritative — use this verbatim, do not paraphrase from memory):\n```json\n{toolResult}\n```\nNow produce the final user-facing answer based ONLY on this tool result. Do not apologise. Do not restate earlier guesses. Do not include any prose you may have written before the tool call."));
            }

            if (appliedAnyUpdate)
            {
                const string reloadNotice = "\n\n> **Changes applied.** Click the **Re-load Story** button on the Details tab to see the updates reflected in the story.";
                finalSoFar += reloadNotice;
                yield return reloadNotice;
            }

            session.Messages.Add(new ChatDisplayMessage { Role = "assistant", Content = finalSoFar });
        }

        private static readonly HashSet<string> MutationToolNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "RenameCharacter", "UpdateCharacterBackground", "AddCharacter", "DeleteCharacter",
            "AddLocation", "UpdateLocationDescription", "DeleteLocation",
            "AddTimeline", "UpdateWorldFacts",
            "AddChapter", "UpdateChapter", "DeleteChapter",
            "AddParagraph", "UpdateParagraphText", "UpdateParagraphMetadata", "DeleteParagraph"
        };

        private static bool IsMutationTool(string name) => !string.IsNullOrEmpty(name) && MutationToolNames.Contains(name);

        private const int LogArgMaxLength = 200;

        private static string TruncateArgsForLog(Dictionary<string, object> args)
        {
            if (args == null) return "{}";
            var truncated = new Dictionary<string, object>();
            foreach (var kv in args)
            {
                if (kv.Value is string s && s.Length > LogArgMaxLength)
                    truncated[kv.Key] = s[..LogArgMaxLength] + $"…[{s.Length} chars]";
                else
                    truncated[kv.Key] = kv.Value;
            }
            return JsonConvert.SerializeObject(truncated);
        }

        private static bool LooksLikeToolError(string toolResult)
        {
            if (string.IsNullOrWhiteSpace(toolResult)) return true;
            try
            {
                var jt = JToken.Parse(toolResult);
                if (jt is JObject obj)
                {
                    // Check both lowercase and uppercase error fields
                    if (obj["error"] != null || obj["Error"] != null) return true;
                    // Treat explicit Success=false as an error
                    var successToken = obj["Success"] ?? obj["success"];
                    if (successToken != null && successToken.Type == JTokenType.Boolean && !successToken.Value<bool>()) return true;
                }
            }
            catch { }
            return false;
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

            // Expose the actual chapter inventory so the assistant uses real chapter
            // names when calling tools such as ListParagraphs / GetParagraphText,
            // instead of guessing generic names like "Chapter 1".
            try
            {
                var chapters = _query.ListChapters();
                if (chapters != null && chapters.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Chapter inventory (use these exact names when calling chapter/paragraph tools):");
                    foreach (var c in chapters)
                    {
                        sb.AppendLine($"- [seq {c.Sequence}] \"{c.Name}\" — {c.ParagraphCount} paragraph(s)");
                    }
                }
            }
            catch { /* best-effort enrichment only */ }

            sb.AppendLine();
            sb.AppendLine("To invoke a tool, emit a single fenced code block with language `tool` containing JSON:");
            sb.AppendLine("```tool");
            sb.AppendLine("{ \"name\": \"<toolName>\", \"args\": { ... } }");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Read tools (safe):");
            sb.AppendLine("- GetCharacter(name), ListCharacters(), GetLocation(name), ListLocations(),");
            sb.AppendLine("  GetTimeline(name), ListTimelines(), GetChapter(title), ListChapters(),");
            sb.AppendLine("  GetParagraph(chapter, index), ListParagraphs(chapter), GetParagraphText(chapter, index),");
            sb.AppendLine("  GetRelationships(name),");
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
            sb.AppendLine("- AddChapter(title, synopsis, sequence, confirmed)");
            sb.AppendLine("- UpdateChapter(title, newTitle, synopsis, confirmed)");
            sb.AppendLine("- DeleteChapter(title, confirmed)");
            sb.AppendLine("- AddParagraph(chapter, sequence, text, location, timeline, characters, confirmed)");
            sb.AppendLine("- UpdateParagraphText(chapter, index, text, confirmed)");
            sb.AppendLine("- UpdateParagraphMetadata(chapter, index, location, timeline, characters, confirmed)");
            sb.AppendLine("- DeleteParagraph(chapter, index, confirmed)");
            sb.AppendLine();
            sb.AppendLine("Rules for structural edits:");
            sb.AppendLine("- Before UpdateParagraphText, always call GetParagraphText first to anchor the new prose to current metadata.");
            sb.AppendLine("- Paragraph index is 1-based, matching the Sequence field.");
            sb.AppendLine("- If the user asks to \"replace\" or \"rewrite\" a paragraph, prefer UpdateParagraphText.");
            sb.AppendLine("- If the user asks to reorder paragraphs, express it as DeleteParagraph then AddParagraph with the desired sequence.");
            sb.AppendLine("- Omit the sequence arg to append.");
            sb.AppendLine();
            sb.AppendLine("Rules for quoting story content (CRITICAL — failure here produces wrong answers):");
            sb.AppendLine("- NEVER quote, paraphrase, or describe the contents of any paragraph, character bio, location description, or timeline detail without first calling the matching Get… tool in the CURRENT turn. Prior conversation history is NOT a reliable source for prose.");
            sb.AppendLine("- When the user asks for \"the last paragraph\" of a chapter, that means the paragraph whose index equals that chapter's paragraph count from the Chapter inventory above. Resolve it by calling GetParagraphText(chapter, <count>).");
            sb.AppendLine("- When the user asks for \"the first paragraph\", call GetParagraphText(chapter, 1).");
            sb.AppendLine("- The Chapter inventory shows names and counts only — it does NOT contain prose. Never invent paragraph text from a chapter title.");
            sb.AppendLine();
            sb.AppendLine("Rules for handling disagreement (anti-sycophancy):");
            sb.AppendLine("- If the user disputes a fact you stated, do NOT apologise and re-state from memory. Re-call the appropriate Get… tool and report exactly what it returns, even if it confirms your earlier answer.");
            sb.AppendLine("- Do not begin replies with \"You're absolutely right\" or similar capitulations.");
            sb.AppendLine("- If a tool result contradicts your earlier answer, simply state the corrected fact based on the tool — no apology preamble.");
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

        /// <summary>
        /// Returns only the <c>```tool …```</c> fenced block from the supplied
        /// assistant text, with all surrounding draft prose stripped. Used when
        /// re-injecting the assistant's tool-call turn into the message history,
        /// so the model cannot re-read its own pre-tool guesses and faithfully
        /// echo them in the next iteration's answer.
        /// </summary>
        private static string ExtractToolFenceOnly(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = ToolBlockRegex.Match(text);
            return m.Success ? m.Value : null;
        }

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
                    "ListParagraphs" => _query.ListParagraphs(A(args, "chapter")),
                    "GetParagraphText" => _query.GetParagraphText(A(args, "chapter"), I(args, "index")),
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
                    "AddChapter" => await _mutation.AddChapterAsync(
                        A(args, "title"), A(args, "synopsis"), IOpt(args, "sequence"), B(args, "confirmed")),
                    "UpdateChapter" => await _mutation.UpdateChapterAsync(
                        A(args, "title"), A(args, "newTitle"), A(args, "synopsis"), B(args, "confirmed")),
                    "DeleteChapter" => await _mutation.DeleteChapterAsync(A(args, "title"), B(args, "confirmed")),
                    "AddParagraph" => await _mutation.AddParagraphAsync(
                        A(args, "chapter"), IOpt(args, "sequence"), A(args, "text"),
                        A(args, "location"), A(args, "timeline"), SArr(args, "characters"),
                        B(args, "confirmed")),
                    "UpdateParagraphText" => await _mutation.UpdateParagraphTextAsync(
                        A(args, "chapter"), I(args, "index"), A(args, "text"), B(args, "confirmed")),
                    "UpdateParagraphMetadata" => await _mutation.UpdateParagraphMetadataAsync(
                        A(args, "chapter"), I(args, "index"),
                        A(args, "location"), A(args, "timeline"), SArr(args, "characters"),
                        B(args, "confirmed")),
                    "DeleteParagraph" => await _mutation.DeleteParagraphAsync(
                        A(args, "chapter"), I(args, "index"), B(args, "confirmed")),
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

        private static int? IOpt(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var v) || v == null) return null;
            var s = v.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s, out var i) ? i : (int?)null;
        }

        private static IEnumerable<string> SArr(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var v) || v == null)
                return Array.Empty<string>();
            if (v is JArray arr)
                return arr.Select(t => t?.ToString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            if (v is IEnumerable<object> oe)
                return oe.Select(t => t?.ToString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            var str = v.ToString();
            if (string.IsNullOrWhiteSpace(str)) return Array.Empty<string>();
            str = str.Trim().Trim('[', ']');
            return str.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().Trim('"', '\''))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
        }
    }
}
