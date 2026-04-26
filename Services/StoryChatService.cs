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
        private readonly ConcurrentDictionary<string, PendingPreview> _pendingPreviews = new();
        private Story _activeStory;
        private const int MaxToolIterations = 4;

        /// <summary>
        /// Highly visible banner shown after any successful mutation, telling
        /// the user to click the <em>Re-load Story</em> button so the change
        /// is reflected in the story view. Rendered as a Markdown blockquote
        /// with a bold heading and rule lines so it stands out clearly from
        /// the model's prose.
        /// </summary>
        private const string ReloadNoticeBanner =
            "\n\n---\n\n" +
            "> ### Action required\n" +
            "> **Click the \"Re-load Story\" button on the Details tab** to see the changes you just made reflected in the story.\n\n" +
            "---\n\n";

        private static string BuildFailureBanner(string error)
        {
            var safe = string.IsNullOrWhiteSpace(error) ? "unknown error" : error.Replace("\r", " ").Replace("\n", " ");
            if (safe.Length > 400) safe = safe.Substring(0, 400) + "…";
            return "\n\n---\n\n" +
                "> ### Operation failed\n" +
                "> **The change was NOT applied.**\n" +
                "> Error: " + safe + "\n\n" +
                "---\n\n";
        }

        private static string ExtractErrorMessage(string toolResult)
        {
            if (string.IsNullOrWhiteSpace(toolResult)) return null;
            try
            {
                var jt = JToken.Parse(toolResult);
                if (jt is JObject obj)
                {
                    var err = obj["Error"] ?? obj["error"];
                    if (err != null && err.Type != JTokenType.Null)
                    {
                        var s = err.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                    var summary = obj["Summary"] ?? obj["summary"];
                    if (summary != null && summary.Type != JTokenType.Null)
                    {
                        var s = summary.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Records a mutation tool that was called in preview mode
        /// (<c>confirmed=false</c>) and returned a successful preview.
        /// When the user's next message is an affirmative such as "yes",
        /// the orchestrator re-invokes the same tool with <c>confirmed=true</c>
        /// deterministically — it does not rely on the model to remember to
        /// re-emit the tool call.
        /// </summary>
        private sealed class PendingPreview
        {
            public string ToolName { get; set; }
            public Dictionary<string, object> Args { get; set; }
            public string Summary { get; set; }
        }

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
            _pendingPreviews.TryRemove(sessionId, out _);
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
            string lastMutationFailure = null;

            // Deterministic safety net for the most hallucination-prone class
            // of question: "what is the {first|last|Nth} {paragraph|sentence}
            // of chapter N". We resolve the paragraph from the graph ourselves
            // and inject the verbatim text as a transient user message *before*
            // the model generates anything. The transient message lives only
            // inside this turn's `messages` list — it is NOT added to
            // session.Messages, so it does not pollute future turns.
            var preFetched = TryPreFetchParagraphContext(userMessage);
            if (preFetched != null)
            {
                messages.Add(new ChatMessage(
                    ChatRole.User,
                    "AUTHORITATIVE STORY DATA (verbatim from the knowledge graph). " +
                    "Use ONLY the fields below to answer the previous question. " +
                    "Quote the Text field exactly as written. Do not invent prose, do not paraphrase from memory.\n" +
                    "```json\n" + preFetched + "\n```"));
            }

            // Deterministic confirm-on-yes: when the previous turn produced a
            // mutation preview and the user's reply is an affirmative such as
            // "yes" / "ok" / "do it", re-invoke the same tool with
            // confirmed=true ourselves. This guarantees the change is actually
            // applied and the reload notice is shown, regardless of whether
            // the model remembers to re-emit the tool call.
            bool isAffirmative = IsAffirmative(userMessage);
            bool hasPending = _pendingPreviews.ContainsKey(sessionId);
            bool autoConfirmApplied = false;
            bool previewEmitted = false;
            await _log.WriteToLogAsync($"ChatTurn session={sessionId} user='{Truncate(userMessage, 60)}' affirmative={isAffirmative} pendingPreview={hasPending}");

            if (isAffirmative && _pendingPreviews.TryRemove(sessionId, out var pending))
            {
                await _log.WriteToLogAsync($"ChatTurn auto-confirming tool={pending.ToolName} args={TruncateArgsForLog(pending.Args)}");
                var confirmedArgs = new Dictionary<string, object>(pending.Args ?? new Dictionary<string, object>())
                {
                    ["confirmed"] = true
                };
                string confirmResult = await InvokeToolAsync(pending.ToolName, confirmedArgs);
                bool confirmIsError = LooksLikeToolError(confirmResult);
                await _log.WriteToLogAsync($"ChatTurn auto-confirm result_len={confirmResult?.Length ?? 0} isError={confirmIsError}");
                if (!confirmIsError)
                {
                    // Mark that a mutation succeeded; the reload banner is
                    // emitted at the end of the stream, AFTER the model's
                    // acknowledgement prose, so the user reads the
                    // confirmation first and the call-to-action last.
                    appliedAnyUpdate = true;
                    autoConfirmApplied = true;
                }
                else
                {
                    lastMutationFailure = ExtractErrorMessage(confirmResult) ?? "unknown error";
                    await _log.WriteToLogAsync($"ChatTurn auto-confirm FAILED: {lastMutationFailure}");
                }
                // Inject the executed mutation as a synthetic assistant tool call
                // + tool result so the model sees a coherent transcript and can
                // produce a brief acknowledgement to the user.
                var syntheticToolCall = $"```tool\n{JsonConvert.SerializeObject(new { name = pending.ToolName, args = confirmedArgs }, Formatting.Indented)}\n```";
                messages.Add(new ChatMessage(ChatRole.Assistant, syntheticToolCall));
                string autoConfirmContinuation = confirmIsError
                    ? $"Tool result for {pending.ToolName} (THIS MUTATION FAILED):\n```json\n{confirmResult}\n```\nIMPORTANT: the change was NOT applied. Tell the user clearly that the operation failed and quote the error message. DO NOT claim success. DO NOT say 'I've added' or 'I've updated'. Do not call more tools."
                    : $"Tool result for {pending.ToolName}:\n```json\n{confirmResult}\n```\nThe change has ALREADY been applied — do NOT call this tool or any other mutation tool again. Reply with a single short sentence confirming what was done, then stop. Do NOT emit any tool block.";
                messages.Add(new ChatMessage(ChatRole.User, autoConfirmContinuation));
            }

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

                bool isMutation = IsMutationTool(toolCall.Name);
                bool isConfirmed = B(toolCall.Args, "confirmed");

                // If we already applied a mutation via auto-confirm in this
                // turn, do NOT re-execute another mutation tool call from the
                // model — that would silently duplicate the change. Instead
                // feed back a synthetic "already applied" result so the
                // model wraps up with prose only.
                if (autoConfirmApplied && isMutation)
                {
                    await _log.WriteToLogAsync($"ChatTurn blocked duplicate mutation {toolCall.Name} after auto-confirm");
                    messages.Add(new ChatMessage(ChatRole.Assistant, assistantText));
                    messages.Add(new ChatMessage(ChatRole.User,
                        $"The change has ALREADY been applied earlier in this turn. The {toolCall.Name} tool call was IGNORED to prevent a duplicate. Stop calling tools. Reply with one short sentence acknowledging the change is done and then stop."));
                    yield return "\n\n";
                    continue;
                }

                // If the model already produced a successful mutation preview
                // earlier in this same turn, refuse to re-run another preview
                // (or any mutation). The model otherwise loops, emitting two
                // or three near-identical "Would you like me to confirm"
                // blocks before the user can answer.
                if (previewEmitted && isMutation)
                {
                    await _log.WriteToLogAsync($"ChatTurn blocked duplicate preview/mutation {toolCall.Name} confirmed={isConfirmed}");
                    messages.Add(new ChatMessage(ChatRole.Assistant, assistantText));
                    messages.Add(new ChatMessage(ChatRole.User,
                        $"A preview for this change was ALREADY shown earlier in this turn. The {toolCall.Name} tool call was IGNORED. Stop calling tools. Wait for the user to reply 'yes' to confirm. Do not produce another preview or another tool block in this turn."));
                    yield return "\n\n";
                    continue;
                }

                string toolResult = await InvokeToolAsync(toolCall.Name, toolCall.Args);
                bool isError = LooksLikeToolError(toolResult);
                if (isMutation && isConfirmed && !isError)
                {
                    appliedAnyUpdate = true;
                    // A confirmed mutation supersedes any previously-pending preview.
                    _pendingPreviews.TryRemove(sessionId, out _);
                    await _log.WriteToLogAsync($"ChatTurn confirmed mutation {toolCall.Name} - cleared pending preview, appliedAnyUpdate=true");
                }
                else if (isMutation && isConfirmed && isError)
                {
                    // Confirmed mutation failed. Capture the error so the user
                    // is shown a clear failure banner and the model is told NOT
                    // to claim success in its follow-up prose.
                    lastMutationFailure = ExtractErrorMessage(toolResult) ?? "unknown error";
                    await _log.WriteToLogAsync($"ChatTurn confirmed mutation {toolCall.Name} FAILED: {lastMutationFailure}");
                }
                else if (isMutation && !isConfirmed && !isError)
                {
                    // Successful preview - remember it so the next affirmative
                    // user reply can deterministically confirm the same call.
                    _pendingPreviews[sessionId] = new PendingPreview
                    {
                        ToolName = toolCall.Name,
                        Args = new Dictionary<string, object>(toolCall.Args ?? new Dictionary<string, object>())
                    };
                    previewEmitted = true;
                    await _log.WriteToLogAsync($"ChatTurn stored pending preview {toolCall.Name} for session={sessionId}");
                }
                await _log.WriteToLogAsync($"ChatTool {toolCall.Name} args={TruncateArgsForLog(toolCall.Args)} result_len={toolResult?.Length ?? 0} isMutation={isMutation} isConfirmed={isConfirmed} isError={isError}");

                messages.Add(new ChatMessage(ChatRole.Assistant, assistantText));
                string continuation;
                if (isMutation && isConfirmed && isError)
                {
                    continuation = $"Tool result for {toolCall.Name} (THIS MUTATION FAILED):\n```json\n{toolResult}\n```\nIMPORTANT: the change was NOT applied. Tell the user clearly that the operation failed and quote the error message. DO NOT claim success. DO NOT say 'I've added' or 'I've updated'. Do not call more tools.";
                }
                else if (isMutation && !isConfirmed && !isError)
                {
                    continuation = $"Tool result for {toolCall.Name}:\n```json\n{toolResult}\n```\nThis is a PREVIEW only — nothing has been changed yet. In ONE short paragraph (no bullet lists, no repetition), summarize what will be added/changed and ask the user to reply 'yes' to confirm. Do NOT call this tool again. Do NOT emit another tool block. Stop after the confirmation question.";
                }
                else
                {
                    continuation = $"Tool result for {toolCall.Name}:\n```json\n{toolResult}\n```\nPlease continue the response for the user based on this data.";
                }
                messages.Add(new ChatMessage(ChatRole.User, continuation));
                yield return "\n\n";
            }

            if (appliedAnyUpdate)
            {
                finalSoFar += ReloadNoticeBanner;
                yield return ReloadNoticeBanner;
            }
            else if (lastMutationFailure != null)
            {
                string failureBanner = BuildFailureBanner(lastMutationFailure);
                finalSoFar += failureBanner;
                yield return failureBanner;
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

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

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
                    // Treat explicit Success=false as an error.
                    var successToken = obj["Success"] ?? obj["success"];
                    if (successToken != null && successToken.Type == JTokenType.Boolean && !successToken.Value<bool>()) return true;
                    // Treat a non-empty Error field as an error. The MutationResult
                    // record always serializes Error (often as null/empty), so the
                    // mere presence of the property does not indicate failure.
                    var errToken = obj["Error"] ?? obj["error"];
                    if (errToken != null && errToken.Type != JTokenType.Null)
                    {
                        var errStr = errToken.ToString();
                        if (!string.IsNullOrWhiteSpace(errStr)) return true;
                    }
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

        // Recognises "first/last/Nth paragraph of chapter N" and
        // "first/last sentence of chapter N". The "which" group identifies
        // which paragraph (or, for sentence questions, the only paragraph
        // whose text we need to look up — the last one for "last sentence").
        private static readonly Regex ParagraphRequestRegex = new(
            @"\b(?<which>first|last|final|\d+|one|two|three|four|five|six|seven|eight|nine|ten)\b[^.\n]{0,40}?\b(?<unit>paragraph|sentence)\b[^.\n]{0,40}?\bchapter\s*(?<chap>\d+|one|two|three|four|five|six|seven|eight|nine|ten)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ParagraphRequestRegexAlt = new(
            @"\bchapter\s*(?<chap>\d+|one|two|three|four|five|six|seven|eight|nine|ten)\b[^.\n]{0,40}?\b(?<which>first|last|final|\d+|one|two|three|four|five|six|seven|eight|nine|ten)\b[^.\n]{0,40}?\b(?<unit>paragraph|sentence)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, int> WordToNumber = new(StringComparer.OrdinalIgnoreCase)
        {
            ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
            ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10
        };

        /// <summary>
        /// Deterministic pre-fetch for the most hallucination-prone class of
        /// question: "what is the {first|last|Nth} {paragraph|sentence} of
        /// chapter N". Resolves the chapter and paragraph via the graph query
        /// service and returns serialised JSON describing the paragraph, or
        /// null when the question does not match a known pattern, the chapter
        /// is not found, or the paragraph index is out of range.
        ///
        /// For "sentence" queries the entire containing paragraph is returned
        /// so the model can quote the exact sentence verbatim from the Text
        /// field — far more reliable than asking the model to recall it.
        /// </summary>
        private string TryPreFetchParagraphContext(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage)) return null;

            var match = ParagraphRequestRegex.Match(userMessage);
            if (!match.Success) match = ParagraphRequestRegexAlt.Match(userMessage);
            if (!match.Success) return null;

            int chapterSeq = ParseNumberToken(match.Groups["chap"].Value);
            if (chapterSeq <= 0) return null;

            IReadOnlyList<ChapterDto> chapters;
            try { chapters = _query.ListChapters(); }
            catch { return null; }
            if (chapters == null || chapters.Count == 0) return null;

            var chapter = chapters.FirstOrDefault(c => c.Sequence == chapterSeq);
            if (chapter == null) return null;
            if (chapter.ParagraphCount <= 0) return null;

            string whichToken = match.Groups["which"].Value;
            string unit = match.Groups["unit"].Value;
            int paragraphIndex;
            if (whichToken.Equals("last", StringComparison.OrdinalIgnoreCase) ||
                whichToken.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                paragraphIndex = chapter.ParagraphCount;
            }
            else if (whichToken.Equals("first", StringComparison.OrdinalIgnoreCase))
            {
                paragraphIndex = 1;
            }
            else
            {
                paragraphIndex = ParseNumberToken(whichToken);
                // For "first/last sentence" the unit-aware logic above already
                // chose the right paragraph; for "Nth sentence" we still need a
                // paragraph to look at and the user did not specify one — fall
                // back to the first paragraph as a best-effort hint to the model.
                if (paragraphIndex <= 0 && string.Equals(unit, "sentence", StringComparison.OrdinalIgnoreCase))
                {
                    paragraphIndex = 1;
                }
            }
            if (paragraphIndex <= 0 || paragraphIndex > chapter.ParagraphCount) return null;

            ParagraphTextDto text;
            try { text = _query.GetParagraphText(chapter.Name, paragraphIndex); }
            catch { return null; }
            if (text == null) return null;

            var payload = new
            {
                source = "GetParagraphText",
                chapter = chapter.Name,
                chapterSequence = chapter.Sequence,
                paragraphIndex,
                paragraphCount = chapter.ParagraphCount,
                text.Text,
                text.Location,
                text.Timeline,
                text.Characters
            };
            return JsonConvert.SerializeObject(payload, Formatting.Indented);
        }

        private static int ParseNumberToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return 0;
            if (int.TryParse(token, out var n)) return n;
            return WordToNumber.TryGetValue(token, out var v) ? v : 0;
        }

        private static readonly HashSet<string> AffirmativeReplies = new(StringComparer.OrdinalIgnoreCase)
        {
            "y", "yes", "yep", "yeah", "yup", "sure", "ok", "okay", "k",
            "do it", "go", "go ahead", "proceed", "confirm", "confirmed",
            "please do", "please proceed", "yes please", "do that", "make it so",
            "apply", "apply it", "apply the change", "apply the changes"
        };

        /// <summary>
        /// Returns true when the user's message is a short affirmative reply
        /// to a prior mutation preview. Strips trailing punctuation/whitespace.
        /// Long messages (more than a few words) are never treated as a bare
        /// affirmative \u2014 they likely contain an additional instruction the
        /// model should handle normally.
        /// </summary>
        private static bool IsAffirmative(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage)) return false;
            var trimmed = userMessage.Trim().TrimEnd('.', '!', '?').Trim();
            if (trimmed.Length == 0) return false;
            // Cap word count so \"yes, but change X\" is not auto-confirmed.
            int wordCount = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 4) return false;
            return AffirmativeReplies.Contains(trimmed);
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
