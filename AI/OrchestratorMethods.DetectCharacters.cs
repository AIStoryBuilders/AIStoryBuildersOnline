using AIStoryBuilders.Model;
using AIStoryBuilders.Models.JSON;
using AIStoryBuilders.Models;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<List<Models.Character>> DetectCharacters(Paragraph objParagraph)
        public async Task<List<Models.Character>> DetectCharacters(Paragraph objParagraph)
        {
            await EnsureSettingsLoaded();
            string GPTModel = SettingsService.AIModel;

            await LogService.WriteToLogAsync($"Detect Characters using {GPTModel} - Start");

            var values = new Dictionary<string, string>
            {
                { "ParagraphContent", objParagraph.ParagraphContent }
            };

            var messages = _promptService.BuildMessages(
                PromptTemplateService.DetectCharacters_System,
                PromptTemplateService.DetectCharacters_User,
                values);

            await LogService.WriteToLogAsync($"Prompt: {messages[0].Text}");

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, GPTModel);

            List<Models.Character> colCharacterOutput = await _llmCallHelper.CallLlmWithRetry<List<Models.Character>>(
                client, messages, options,
                jObj =>
                {
                    var characters = new List<Models.Character>();
                    if (jObj["characters"] is JArray charArray)
                    {
                        foreach (var character in charArray)
                        {
                            string CharacterName = character["name"]?.ToString() ?? "";
                            characters.Add(new Models.Character
                            {
                                CharacterName = CharacterName,
                                CharacterBackground = new List<CharacterBackground>()
                            });
                        }
                    }
                    return characters;
                });

            return colCharacterOutput ?? new List<Models.Character>();
        }
        #endregion
    }
}
