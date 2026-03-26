using Microsoft.Extensions.AI;
using AIStoryBuilders.Model;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<string> ParseNewStory(string paramStoryTitle, string paramStoryText, string GPTModel)
        public async Task<string> ParseNewStory(string paramStoryTitle, string paramStoryText, string GPTModel)
        {
            await EnsureSettingsLoaded();

            await LogService.WriteToLogAsync($"ParseNewStory using {GPTModel} - Start");

            // Trim paramStoryText to 10000 words (so we don't run out of tokens)
            paramStoryText = OrchestratorMethods.TrimToMaxWords(paramStoryText, 10000);

            var values = new Dictionary<string, string>
            {
                { "StoryTitle", paramStoryTitle },
                { "StoryText", paramStoryText }
            };

            var messages = _promptService.BuildMessages(
                PromptTemplateService.ParseNewStory_System,
                PromptTemplateService.ParseNewStory_User,
                values);

            await LogService.WriteToLogAsync($"Prompt: {messages[0].Text}");

            ReadTextEvent?.Invoke(this, new ReadTextEventArgs($"Calling AI to Parse new Story...", 30));

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, GPTModel);

            // Use CallLlmWithRetry to get raw JSON string
            string result = await _llmCallHelper.CallLlmWithRetry<string>(
                client, messages, options,
                jObj => jObj.ToString());

            return result ?? "";
        }
        #endregion
    }
}
