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
            string SystemMessage = "";

            await LogService.WriteToLogAsync($"TestAccess using {GPTModel} - Start");

            OpenAIClient api = await CreateOpenAIClient();

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
                topP: 1,
                frequencyPenalty: 0,
                presencePenalty: 0);

            ChatResponseResult = await api.ChatEndpoint.GetCompletionAsync(FinalChatRequest);

            // *****************************************************

            await LogService.WriteToLogAsync($"TotalTokens: {ChatResponseResult.Usage.TotalTokens} - ChatResponseResult - {ChatResponseResult.FirstChoice.Message.Content}");

            if (SettingsService.AIType != "OpenAI")
            {
                try
                {
                    // Azure OpenAI - Test the embedding model
                    string VectorEmbedding = await GetVectorEmbedding("This is a test for embedding", false);
                }
                catch (Exception ex)
                {
                    await LogService.WriteToLogAsync($"Azure OpenAI - Test the embedding model - Error: {ex.Message}");

                    throw new Exception("Error: You must set a proper Azure OpenAI embedding model");
                }
            }

            return true;
        }
        #endregion
    }
}
