using AIStoryBuilders.Model;
using AIStoryBuilders.Models.JSON;
using OpenAI.Chat;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenAI.Moderations;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<string> CleanJSON(string JSON, string GPTModel)
        public async Task<string> CleanJSON(string JSON, string GPTModel)
        {
            await SettingsService.LoadSettingsAsync();
            string Organization = SettingsService.Organization;
            string ApiKey = SettingsService.ApiKey;
            string SystemMessage = "";

            await LogService.WriteToLogAsync($"Clean JSON using {GPTModel} - Start");

            // Create a new OpenAIClient object
            // with the provided API key and organization
            OpenAIClient api = await CreateOpenAIClient();

            // Create a colection of chatPrompts
            ChatResponse ChatResponseResult = new ChatResponse();
            List<Message> chatPrompts = new List<Message>();

            // Update System Message
            SystemMessage = CreateSystemMessageCleanJSON(JSON);

            await LogService.WriteToLogAsync($"Prompt: {SystemMessage}");

            chatPrompts = new List<Message>();

            chatPrompts.Insert(0,
            new Message(
                Role.System,
                SystemMessage
                )
            );

            ReadTextEvent?.Invoke(this, new ReadTextEventArgs($"Calling ChatGPT to clean JSON...", 20));

            // Get a response from ChatGPT 
            var FinalChatRequest = new ChatRequest(
                chatPrompts,
                model: GPTModel,
                topP: 1,
                frequencyPenalty: 0,
                presencePenalty: 0);

            ChatResponseResult = await api.ChatEndpoint.GetCompletionAsync(FinalChatRequest);

            // *****************************************************

            await LogService.WriteToLogAsync($"TotalTokens: {ChatResponseResult.Usage.TotalTokens} - ChatResponseResult - {ChatResponseResult.FirstChoice.Message.Content}");

            return ChatResponseResult.FirstChoice.Message.Content;
        }
        #endregion

        // Methods

        #region private string CreateSystemMessageCleanJSON(string paramJSON)
        private string CreateSystemMessageCleanJSON(string paramJSON)
        {
            return "Please correct this json to make it valid. Return only the valid json: \n" +
                    $"{paramJSON} \n";                    
        }
        #endregion
    }
}
