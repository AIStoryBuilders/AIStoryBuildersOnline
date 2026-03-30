using AIStoryBuilders.Model;
using AIStoryBuilders.Services;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region Manuscript Parsing Prompt Templates

        private const string ManuscriptParsing_ParseChapters_System = @"You are a function that identifies chapter boundaries in a block of text from a novel or manuscript.
Analyze the following text and identify where each chapter begins. Look for chapter headings like 'Chapter 1', 'Chapter One', 'CHAPTER I', or any clear chapter title/heading patterns.
For each chapter found, provide:
1. The chapter title as it appears in the text
2. The first sentence of that chapter (the first sentence AFTER the chapter heading)

If no chapter headings are found, return an empty chapters array.

Text to analyze:
{ChunkText}

Provide the results in the following JSON format:
{{
  ""chapters"": [
    {{
      ""title"": ""Chapter Title"",
      ""first_sentence"": ""The first sentence of this chapter...""
    }}
  ]
}}";

        private const string ManuscriptParsing_ParseChapters_User = "Please identify the chapter boundaries now.";

        private const string ManuscriptParsing_Summarize_System = @"You are a function that creates comprehensive summaries of novel chapters.
Please create a detailed summary of the following chapter text. Include key plot events, character actions, settings described, and any important dialogue or revelations.
The summary should be 200-400 words.

Chapter text:
{ChapterText}

Provide the results in the following JSON format:
{{
  ""summary"": ""[detailed chapter summary]""
}}";

        private const string ManuscriptParsing_Summarize_User = "Please summarize this chapter now.";

        private const string ManuscriptParsing_ExtractBeats_System = @"You are a function that extracts story beats from a chapter summary.
Given the following chapter summary, create a series of story beats that describe the key events in order.
Format each beat as: #Beat N - Description of what happens.

Chapter summary:
{ChapterSummary}

Provide the results in the following JSON format:
{{
  ""beats"": ""#Beat 1 - First event. #Beat 2 - Second event. #Beat 3 - Third event.""
}}";

        private const string ManuscriptParsing_ExtractBeats_User = "Please extract the story beats now.";

        private const string ManuscriptParsing_ExtractCharacters_System = @"You are a function that identifies characters from a chapter summary.
Analyze the following summary from the chapter titled '{ChapterTitle}' and identify all characters mentioned.
For each character, provide their name and a brief backstory or description based on what is mentioned.

Chapter summary:
{Summary}

Provide the results in the following JSON format:
{{
  ""characters"": [
    {{
      ""name"": ""Character Name"",
      ""backstory"": ""Brief description or backstory of this character based on the chapter.""
    }}
  ]
}}";

        private const string ManuscriptParsing_ExtractCharacters_User = "Please extract the characters now.";

        private const string ManuscriptParsing_ExtractLocations_System = @"You are a function that identifies locations from a chapter summary.
Analyze the following summary from the chapter titled '{ChapterTitle}' and identify all locations or settings mentioned.
For each location, provide its name and a brief description.

Chapter summary:
{Summary}

Provide the results in the following JSON format:
{{
  ""locations"": [
    {{
      ""name"": ""Location Name"",
      ""description"": ""Brief description of this location.""
    }}
  ]
}}";

        private const string ManuscriptParsing_ExtractLocations_User = "Please extract the locations now.";

        private const string ManuscriptParsing_ExtractTimelines_System = @"You are a function that identifies timeline events from a chapter summary.
Analyze the following summary from chapter {ChapterIndex} titled '{ChapterTitle}' and identify key chronological events or time periods.
For each timeline entry, provide a short name and description.

Chapter summary:
{Summary}

Provide the results in the following JSON format:
{{
  ""timelines"": [
    {{
      ""name"": ""Event or time period name"",
      ""description"": ""Brief description of this timeline event.""
    }}
  ]
}}";

        private const string ManuscriptParsing_ExtractTimelines_User = "Please extract the timeline events now.";

        private const string ManuscriptParsing_AssociateEntities_System = @"You are a function that associates paragraphs with characters, locations, and timelines.
Given the following list of paragraphs, characters, locations, and timelines from a chapter, determine which entities appear in or are relevant to each paragraph.

Paragraphs (by index):
{ParagraphList}

Available characters: {CharacterNames}
Available locations: {LocationNames}
Available timelines: {TimelineNames}

For each paragraph, identify:
1. The most relevant location (or empty string if none)
2. The most relevant timeline event (or empty string if none)
3. The characters that appear or are referenced

Provide the results in the following JSON format:
{{
  ""associations"": [
    {{
      ""index"": 0,
      ""location"": ""Location Name"",
      ""timeline"": ""Timeline Name"",
      ""characters"": [""Character1"", ""Character2""]
    }}
  ]
}}";

        private const string ManuscriptParsing_AssociateEntities_User = "Please associate the entities with each paragraph now.";

        #endregion

        #region Manuscript Parsing Methods

        public async Task<List<ManuscriptParsingService.ChapterBoundary>> ParseChaptersAsync(string chunkText)
        {
            await EnsureSettingsLoaded();
            await LogService.WriteToLogAsync("ParseChaptersAsync - Start");

            var values = new Dictionary<string, string>
            {
                { "ChunkText", chunkText }
            };

            var messages = _promptService.BuildMessages(
                ManuscriptParsing_ParseChapters_System,
                ManuscriptParsing_ParseChapters_User,
                values);

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, SettingsService.AIModel);

            var result = await _llmCallHelper.CallLlmWithRetry<List<ManuscriptParsingService.ChapterBoundary>>(
                client, messages, options,
                jObj =>
                {
                    var chapters = jObj["chapters"]?.ToObject<List<ChapterBoundaryEntry>>();
                    if (chapters == null) return new List<ManuscriptParsingService.ChapterBoundary>();

                    return chapters.Select(c => new ManuscriptParsingService.ChapterBoundary
                    {
                        Title = c.title,
                        FirstSentence = c.first_sentence
                    }).ToList();
                });

            return result ?? new List<ManuscriptParsingService.ChapterBoundary>();
        }

        public async Task<string> SummarizeChapterAsync(string chapterText)
        {
            await EnsureSettingsLoaded();
            await LogService.WriteToLogAsync("SummarizeChapterAsync - Start");

            // Trim to avoid token limits
            chapterText = TrimToMaxWords(chapterText, 8000);

            var values = new Dictionary<string, string>
            {
                { "ChapterText", chapterText }
            };

            var messages = _promptService.BuildMessages(
                ManuscriptParsing_Summarize_System,
                ManuscriptParsing_Summarize_User,
                values);

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, SettingsService.AIModel);

            var result = await _llmCallHelper.CallLlmWithRetry<string>(
                client, messages, options,
                jObj => jObj["summary"]?.ToString() ?? "");

            return result ?? "";
        }

        public async Task<string> ExtractBeatsAsync(string chapterSummary)
        {
            await EnsureSettingsLoaded();
            await LogService.WriteToLogAsync("ExtractBeatsAsync - Start");

            var values = new Dictionary<string, string>
            {
                { "ChapterSummary", chapterSummary }
            };

            var messages = _promptService.BuildMessages(
                ManuscriptParsing_ExtractBeats_System,
                ManuscriptParsing_ExtractBeats_User,
                values);

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, SettingsService.AIModel);

            var result = await _llmCallHelper.CallLlmWithRetry<string>(
                client, messages, options,
                jObj => jObj["beats"]?.ToString() ?? "");

            return result ?? "";
        }

        public async Task<List<ManuscriptParsingService.ParsedCharacter>> ExtractCharactersFromSummaryAsync(
            string summary, string chapterTitle)
        {
            await EnsureSettingsLoaded();
            await LogService.WriteToLogAsync("ExtractCharactersFromSummaryAsync - Start");

            var values = new Dictionary<string, string>
            {
                { "Summary", summary },
                { "ChapterTitle", chapterTitle }
            };

            var messages = _promptService.BuildMessages(
                ManuscriptParsing_ExtractCharacters_System,
                ManuscriptParsing_ExtractCharacters_User,
                values);

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, SettingsService.AIModel);

            var result = await _llmCallHelper.CallLlmWithRetry<List<ManuscriptParsingService.ParsedCharacter>>(
                client, messages, options,
                jObj =>
                {
                    var chars = jObj["characters"]?.ToObject<List<ParsedCharacterEntry>>();
                    if (chars == null) return new List<ManuscriptParsingService.ParsedCharacter>();

                    return chars.Select(c => new ManuscriptParsingService.ParsedCharacter
                    {
                        Name = c.name,
                        Backstory = c.backstory
                    }).ToList();
                });

            return result ?? new List<ManuscriptParsingService.ParsedCharacter>();
        }

        public async Task<List<ManuscriptParsingService.ParsedLocation>> ExtractLocationsFromSummaryAsync(
            string summary, string chapterTitle)
        {
            await EnsureSettingsLoaded();
            await LogService.WriteToLogAsync("ExtractLocationsFromSummaryAsync - Start");

            var values = new Dictionary<string, string>
            {
                { "Summary", summary },
                { "ChapterTitle", chapterTitle }
            };

            var messages = _promptService.BuildMessages(
                ManuscriptParsing_ExtractLocations_System,
                ManuscriptParsing_ExtractLocations_User,
                values);

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, SettingsService.AIModel);

            var result = await _llmCallHelper.CallLlmWithRetry<List<ManuscriptParsingService.ParsedLocation>>(
                client, messages, options,
                jObj =>
                {
                    var locs = jObj["locations"]?.ToObject<List<ParsedLocationEntry>>();
                    if (locs == null) return new List<ManuscriptParsingService.ParsedLocation>();

                    return locs.Select(l => new ManuscriptParsingService.ParsedLocation
                    {
                        Name = l.name,
                        Description = l.description
                    }).ToList();
                });

            return result ?? new List<ManuscriptParsingService.ParsedLocation>();
        }

        public async Task<List<ManuscriptParsingService.ParsedTimeline>> ExtractTimelinesFromSummaryAsync(
            string summary, string chapterTitle, int chapterIndex)
        {
            await EnsureSettingsLoaded();
            await LogService.WriteToLogAsync("ExtractTimelinesFromSummaryAsync - Start");

            var values = new Dictionary<string, string>
            {
                { "Summary", summary },
                { "ChapterTitle", chapterTitle },
                { "ChapterIndex", chapterIndex.ToString() }
            };

            var messages = _promptService.BuildMessages(
                ManuscriptParsing_ExtractTimelines_System,
                ManuscriptParsing_ExtractTimelines_User,
                values);

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, SettingsService.AIModel);

            var result = await _llmCallHelper.CallLlmWithRetry<List<ManuscriptParsingService.ParsedTimeline>>(
                client, messages, options,
                jObj =>
                {
                    var tls = jObj["timelines"]?.ToObject<List<ParsedTimelineEntry>>();
                    if (tls == null) return new List<ManuscriptParsingService.ParsedTimeline>();

                    return tls.Select(t => new ManuscriptParsingService.ParsedTimeline
                    {
                        Name = t.name,
                        Description = t.description
                    }).ToList();
                });

            return result ?? new List<ManuscriptParsingService.ParsedTimeline>();
        }

        public async Task<List<ManuscriptParsingService.ParagraphAssociation>> AssociateParagraphEntitiesAsync(
            List<string> paragraphs,
            List<ManuscriptParsingService.ParsedCharacter> characters,
            List<ManuscriptParsingService.ParsedLocation> locations,
            List<ManuscriptParsingService.ParsedTimeline> timelines)
        {
            await EnsureSettingsLoaded();
            await LogService.WriteToLogAsync("AssociateParagraphEntitiesAsync - Start");

            // Build paragraph list string (truncate each for token efficiency)
            var paraList = new System.Text.StringBuilder();
            for (int i = 0; i < paragraphs.Count; i++)
            {
                string truncated = TrimToMaxWords(paragraphs[i], 100);
                paraList.AppendLine($"[{i}]: {truncated}");
            }

            string characterNames = characters != null && characters.Count > 0
                ? string.Join(", ", characters.Select(c => c.Name))
                : "(none)";

            string locationNames = locations != null && locations.Count > 0
                ? string.Join(", ", locations.Select(l => l.Name))
                : "(none)";

            string timelineNames = timelines != null && timelines.Count > 0
                ? string.Join(", ", timelines.Select(t => t.Name))
                : "(none)";

            var values = new Dictionary<string, string>
            {
                { "ParagraphList", paraList.ToString() },
                { "CharacterNames", characterNames },
                { "LocationNames", locationNames },
                { "TimelineNames", timelineNames }
            };

            var messages = _promptService.BuildMessages(
                ManuscriptParsing_AssociateEntities_System,
                ManuscriptParsing_AssociateEntities_User,
                values);

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, SettingsService.AIModel);

            var result = await _llmCallHelper.CallLlmWithRetry<List<ManuscriptParsingService.ParagraphAssociation>>(
                client, messages, options,
                jObj =>
                {
                    var assocs = jObj["associations"]?.ToObject<List<ParagraphAssociationEntry>>();
                    if (assocs == null) return new List<ManuscriptParsingService.ParagraphAssociation>();

                    return assocs.Select(a => new ManuscriptParsingService.ParagraphAssociation
                    {
                        Index = a.index,
                        Location = a.location,
                        Timeline = a.timeline,
                        Characters = a.characters ?? new List<string>()
                    }).ToList();
                });

            return result ?? new List<ManuscriptParsingService.ParagraphAssociation>();
        }

        #endregion

        #region Manuscript Parsing DTOs

        private class ChapterBoundaryEntry
        {
            public string title { get; set; }
            public string first_sentence { get; set; }
        }

        private class ParsedCharacterEntry
        {
            public string name { get; set; }
            public string backstory { get; set; }
        }

        private class ParsedLocationEntry
        {
            public string name { get; set; }
            public string description { get; set; }
        }

        private class ParsedTimelineEntry
        {
            public string name { get; set; }
            public string description { get; set; }
        }

        private class ParagraphAssociationEntry
        {
            public int index { get; set; }
            public string location { get; set; }
            public string timeline { get; set; }
            public List<string> characters { get; set; }
        }

        #endregion
    }
}
