using AIStoryBuilders.Model;
using AIStoryBuilders.Models.JSON;
using AIStoryBuilders.Models;
using Microsoft.Extensions.AI;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<List<SimpleCharacterSelector>> DetectCharacterAttributes(Paragraph objParagraph, List<Models.Character> colCharacters, string objDetectionType)
        public async Task<List<SimpleCharacterSelector>> DetectCharacterAttributes(Paragraph objParagraph, List<Models.Character> colCharacters, string objDetectionType)
        {
            await EnsureSettingsLoaded();
            string GPTModel = SettingsService.AIModel;

            await LogService.WriteToLogAsync($"Detect Character Attributes using {GPTModel} - Start");

            // Serialize the Characters to JSON
            var SimpleCharacters = ProcessCharacters(colCharacters);
            string json = CharacterJsonSerializer.Serialize(SimpleCharacters);

            var values = new Dictionary<string, string>
            {
                { "ParagraphContent", objParagraph.ParagraphContent },
                { "CharacterJSON", json }
            };

            var messages = _promptService.BuildMessages(
                PromptTemplateService.DetectCharacterAttributes_System,
                PromptTemplateService.DetectCharacterAttributes_User,
                values);

            await LogService.WriteToLogAsync($"Prompt: {messages[0].Text}");

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, GPTModel);

            List<SimpleCharacterSelector> colCharacterOutput = await _llmCallHelper.CallLlmWithRetry<List<SimpleCharacterSelector>>(
                client, messages, options,
                jObj =>
                {
                    var result = new List<SimpleCharacterSelector>();
                    List<string> colAllowedTypes = new List<string> { "Appearance", "Goals", "History", "Aliases", "Facts" };

                    if (jObj["characters"] is JArray charArray)
                    {
                        foreach (var character in charArray)
                        {
                            string CharacterName = character["name"]?.ToString() ?? "";

                            // If the CharacterName is not in the list of colCharacters, skip
                            if (!colCharacters.Any(x => x.CharacterName == CharacterName))
                                continue;

                            // Add character element if in "New Character" mode
                            if (objDetectionType == "New Character")
                            {
                                result.Add(new SimpleCharacterSelector
                                {
                                    CharacterDisplay = $"Add Character - {CharacterName}",
                                    CharacterValue = $"{CharacterName}|{objDetectionType}||"
                                });
                            }

                            var descriptions = character["descriptions"] as JArray;
                            if (descriptions != null)
                            {
                                foreach (var description in descriptions)
                                {
                                    string description_type = description["description_type"]?.ToString() ?? "";
                                    string description_text = description["description"]?.ToString() ?? "";

                                    if (!colAllowedTypes.Contains(description_type))
                                        continue;

                                    result.Add(new SimpleCharacterSelector
                                    {
                                        CharacterDisplay = $"{CharacterName} - ({description_type}) {description_text}",
                                        CharacterValue = $"{CharacterName}|{objDetectionType}|{description_type}|{description_text}"
                                    });
                                }
                            }
                        }
                    }
                    return result;
                });

            return colCharacterOutput ?? new List<SimpleCharacterSelector>();
        }
        #endregion

        // Utility

        #region  public static class CharacterJsonSerializer
        public static class CharacterJsonSerializer
        {
            public static string Serialize(List<SimpleCharacter> characters)
            {
                return System.Text.Json.JsonSerializer.Serialize(characters, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                });
            }
        }
        #endregion

        #region public List<SimpleCharacter> ProcessCharacters(List<Models.Character> inputCharacters)
        public List<SimpleCharacter> ProcessCharacters(List<Models.Character> inputCharacters)
        {
            return inputCharacters.Select(character => new SimpleCharacter
            {
                CharacterName = character.CharacterName,
                CharacterBackground = character.CharacterBackground.Select(bg => new SimpleCharacterBackground
                {
                    Type = bg.Type,
                    Description = bg.Description
                }).ToList()
            }).ToList();
        }
        #endregion 
    }
}
