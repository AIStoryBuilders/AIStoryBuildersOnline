using AIStoryBuilders.Model;
using AIStoryBuilders.Models.JSON;
using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<string> CreateNewChapters(string JSONNewStory, string ChapterCount, string GPTModel)
        public async Task<string> CreateNewChapters(string JSONNewStory, string ChapterCount, string GPTModel)
        {
            await EnsureSettingsLoaded();

            await LogService.WriteToLogAsync($"CreateNewChapters using {GPTModel} - Start");

            var values = new Dictionary<string, string>
            {
                { "JSONNewStory", JSONNewStory },
                { "ChapterCount", ChapterCount }
            };

            var messages = _promptService.BuildMessages(
                PromptTemplateService.CreateNewChapters_System,
                PromptTemplateService.CreateNewChapters_User,
                values);

            await LogService.WriteToLogAsync($"Prompt: {messages[0].Text}");

            ReadTextEvent?.Invoke(this, new ReadTextEventArgs($"Calling AI...", 70));

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, GPTModel);

            string result = await _llmCallHelper.CallLlmWithRetry<string>(
                client, messages, options,
                jObj => jObj.ToString());

            return result ?? "";
        }
        #endregion
    }
}
