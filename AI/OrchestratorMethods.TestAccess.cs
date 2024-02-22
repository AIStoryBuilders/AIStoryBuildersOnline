using AIStoryBuilders.Model;
using AIStoryBuilders.Models.JSON;
using OpenAI.Chat;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<bool> TestAccess(string GPTModel)
        public async Task<bool> TestAccess(string GPTModel)
        {
            await SettingsService.LoadSettingsAsync();
            string Organization = SettingsService.Organization;
            string ApiKey = SettingsService.ApiKey;
            string SystemMessage = "";

            await LogService.WriteToLogAsync($"TestAccess using {GPTModel} - Start");

            HttpClient.Timeout = TimeSpan.FromSeconds(520);
            var api = new OpenAIClient(new OpenAIAuthentication(ApiKey), client: HttpClient);

            // Create a colection of chatPrompts
            ChatResponse ChatResponseResult = new ChatResponse();
            List<Message> chatPrompts = new List<Message>();

            // Update System Message
            SystemMessage = "Please return the following as json: \"This is successful\" in this format {\r\n  'message': message\r\n}";

            await LogService.WriteToLogAsync($"Prompt: {SystemMessage}");

            chatPrompts = new List<Message>();

            chatPrompts.Insert(0,
            new Message(
                Role.System,
                SystemMessage
                )
            );

            ReadTextEvent?.Invoke(this, new ReadTextEventArgs($"Calling ChatGPT to test access...", 5));

            // Get a response from ChatGPT 
            var FinalChatRequest = new ChatRequest(
                chatPrompts,
                model: GPTModel,
                temperature: 0.0,
                topP: 1,
                frequencyPenalty: 0,
                presencePenalty: 0);

            ChatResponseResult = await api.ChatEndpoint.GetCompletionAsync(FinalChatRequest);

            // *****************************************************

            await LogService.WriteToLogAsync($"TotalTokens: {ChatResponseResult.Usage.TotalTokens} - ChatResponseResult - {ChatResponseResult.FirstChoice.Message.Content}");

            return true;
        }
        #endregion
    }
}
