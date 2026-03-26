using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Centralises all LLM prompt templates. Templates use {Placeholder} tokens that are hydrated at call time.
    /// </summary>
    public class PromptTemplateService
    {
        // WriteParagraph templates
        public const string WriteParagraph_System = @"You are a function that will produce JSON that contains the contents of a single paragraph for a novel.
{SystemMessage}{StoryTitle}{StoryStyle}{StorySynopsis}{CurrentChapter}{PreviousParagraphs}{CurrentParagraph}{CurrentLocation}{CharacterList}{RelatedParagraphs}{PromptInstruction}
#### Only use information provided. Do not use any information not provided.
#### Write in the writing style of the provided content.
#### Insert a line break before a dialogue quote by a character the when they speak for the first time.
#### Produce a single paragraph that is {NumberOfWords} words maximum.
#### Provide the results in the following JSON format:
{{
""paragraph_content"": ""[paragraph_content]""
}}";

        public const string WriteParagraph_User = "Please write the paragraph now.";

        // ParseNewStory templates
        public const string ParseNewStory_System = @"Given a story titled:
[ {StoryTitle} ]
With the story text:
[ {StoryText} ]
Using only this information please identify:
#1 Locations mentioned in the story with a short description of each location.
#2 A short timeline's name and a short sentence description to identify specific chronological events of the story.
#3 Characters present in the story.
#4 For each Character a description_type with a description and the timeline_name from timelines.
Provide the results in the following JSON format:
{{
  ""locations"": [{{
    ""name"": ""name"",
    ""description"": ""description""
  }}],
  ""timelines"": [{{
    ""name"": ""name"",
    ""description"": ""description""
  }}],
  ""characters"": [{{
    ""name"": ""name"",
    ""descriptions"": [{{
      ""description_type"": ""description_type"",
      ""enum"": [""Appearance"", ""Goals"", ""History"", ""Aliases"", ""Facts""],
      ""description"": ""description"",
      ""timeline_name"": ""timeline_name""
    }}]
  }}]
}}";

        public const string ParseNewStory_User = "Please parse the story now.";

        // CreateNewChapters templates
        public const string CreateNewChapters_System = @"Given a story with the following structure:
[
{JSONNewStory}
]
Using only this information please:
#1 Create {ChapterCount} chapters in a format like this: Chapter1, Chapter2, Chapter3.
#2 A short chapter_synopsis description. Format this in story beats in a format like this: #Beat 1 - Something happens. #Beat 2 - The next things happens. #Beat 3 - Another thing happens.
#3 A short 200 word first paragraph on the first #Beat for each chapter.
#4 A single timeline_name for each paragraph.
#5 The list of character names that appear in each paragraph.
Output JSON nothing else.
Provide the results in the following JSON format:
{{
""chapter"": [
{{
""chapter_name"": chapter_name,
""chapter_synopsis"": chapter_synopsis,
""paragraphs"": [
{{
""contents"": contents,
""location_name"": location_name,
""timeline_name"": timeline_name,
""character_names"": [character_names]
}}
]
}}
]
}}";

        public const string CreateNewChapters_User = "Please create the chapters now.";

        // DetectCharacters templates
        public const string DetectCharacters_System = @"You are a function that will produce only JSON.
Please analyze a paragraph of text (given as #paramParagraphContent).
#1 Identify all characters, by name, mentioned in the paragraph.
### This is the content of #paramParagraphContent: {ParagraphContent}
Provide the results in the following JSON format:
{{
""characters"": [
{{
""name"": ""[Name]""
}}
]
}}";

        public const string DetectCharacters_User = "Please detect the characters now.";

        // DetectCharacterAttributes templates
        public const string DetectCharacterAttributes_System = @"You are a function that will produce only JSON.
#1 Please analyze a paragraph of text (given as #paramParagraphContent) and a JSON string representing a list of characters and their current descriptions (given as #CharacterJSON).
#2 Output a new JSON containing only new descriptions.
#3 Do not output CharacterName not present in #CharacterJSON.
#4 Identify any new descriptions for each character in #CharacterJSON, mentioned in #paramParagraphContent that are not already present for the CharacterName in #CharacterJSON.
#5 Only output each character once in the JSON.
#6 Do not output any descriptions for any CharacterName that is already in #CharacterJSON for that CharacterName.
### This is the content of #paramParagraphContent: {ParagraphContent}
### This is the content of #CharacterJSON: {CharacterJSON}
Provide the results in the following JSON format:
{{
""characters"": [
{{
""name"": ""[Name]"",
""descriptions"": [
{{
""description_type"": ""[DescriptionType]"",
""enum"": [""Appearance"",""Goals"",""History"",""Aliases"",""Facts""],
""description"": ""[Description]""
}}
]
}}
]
}}";

        public const string DetectCharacterAttributes_User = "Please detect character attributes now.";

        // GetStoryBeats templates
        public const string GetStoryBeats_System = @"You are a function that will produce only simple text.
Please analyze a paragraph of text (given as #paramParagraphContent).
#1 Create story beats for the paragraph.
#2 Output only the story beats, nothing else.
### This is the content of #paramParagraphContent: {ParagraphContent}";

        public const string GetStoryBeats_User = "Please create the story beats now.";

        /// <summary>
        /// Hydrates a template by replacing {Placeholder} tokens with provided values.
        /// </summary>
        public string Hydrate(string template, Dictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template))
                return template;

            foreach (var kvp in values)
            {
                template = template.Replace($"{{{kvp.Key}}}", kvp.Value ?? "");
            }

            return template;
        }

        /// <summary>
        /// Builds a list of ChatMessages from system and user templates with placeholder values.
        /// </summary>
        public List<ChatMessage> BuildMessages(string systemTemplate, string userTemplate, Dictionary<string, string> values)
        {
            var hydratedSystem = Hydrate(systemTemplate, values);
            var hydratedUser = Hydrate(userTemplate, values);

            return new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, hydratedSystem),
                new ChatMessage(ChatRole.User, hydratedUser)
            };
        }
    }
}
