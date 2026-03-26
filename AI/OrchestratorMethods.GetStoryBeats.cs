using AIStoryBuilders.Model;
using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<string> GetStoryBeats(string paramParagraph)
        public async Task<string> GetStoryBeats(string paramParagraph)
        {
            await EnsureSettingsLoaded();
            string GPTModel = SettingsService.AIModel;

            await LogService.WriteToLogAsync($"GetStoryBeats using {GPTModel} - Start");

            var values = new Dictionary<string, string>
            {
                { "ParagraphContent", paramParagraph }
            };

            var messages = _promptService.BuildMessages(
                PromptTemplateService.GetStoryBeats_System,
                PromptTemplateService.GetStoryBeats_User,
                values);

            await LogService.WriteToLogAsync($"Prompt: {messages[0].Text}");

            IChatClient client = CreateChatClient();
            // Use text options (no JSON mode) — this returns plain text
            var options = ChatOptionsFactory.CreateTextOptions(SettingsService.AIType, GPTModel);

            string result = await _llmCallHelper.CallLlmForText(client, messages, options);

            return result ?? "";
        }
        #endregion
    }
}