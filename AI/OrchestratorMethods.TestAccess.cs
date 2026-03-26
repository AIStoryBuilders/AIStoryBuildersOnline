using AIStoryBuilders.Model;
using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<bool> TestAccess(string GPTModel)
        public async Task<bool> TestAccess(string GPTModel)
        {
            await EnsureSettingsLoaded();

            await LogService.WriteToLogAsync($"TestAccess using {GPTModel} - Start");

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System,
                    "Please return the following as json: \"This is successful\" in this format { \"message\": message }")
            };

            ReadTextEvent?.Invoke(this, new ReadTextEventArgs($"Calling AI to test access...", 5));

            IChatClient client = CreateChatClient();
            var options = new ChatOptions
            {
                ModelId = GPTModel,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
            };

            var response = await client.GetResponseAsync(messages, options);

            await LogService.WriteToLogAsync(
                $"TotalTokens: {response.Usage?.TotalTokenCount} - ChatResponseResult - {response.Text}");

            // Test the local BrowserEmbeddingGenerator
            try
            {
                await EmbeddingGenerator.InitializeAsync();
                float[] testEmbedding =
                    await EmbeddingGenerator.GenerateEmbeddingAsync("test embedding");

                if (testEmbedding == null || testEmbedding.Length != BrowserEmbeddingGenerator.VectorDimension)
                {
                    throw new Exception($"Local embedding model failed to produce {BrowserEmbeddingGenerator.VectorDimension}-d vector.");
                }

                await LogService.WriteToLogAsync(
                    $"Local embedding test passed — {testEmbedding.Length}-d vector produced.");
            }
            catch (Exception ex)
            {
                await LogService.WriteToLogAsync($"Local embedding test — Error: {ex.Message}");
                throw new Exception("Error: Local embedding model failed. Check browser console for details.");
            }

            return true;
        }
        #endregion
    }
}
