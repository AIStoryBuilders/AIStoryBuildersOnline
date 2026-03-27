using Microsoft.Extensions.AI;
using Mscc.GenerativeAI;
using GoogleContent = Mscc.GenerativeAI.Types.Content;
using GoogleGenerationConfig = Mscc.GenerativeAI.Types.GenerationConfig;
using System.Runtime.CompilerServices;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// IChatClient implementation that wraps the Google Generative AI (Mscc.GenerativeAI) SDK.
    /// </summary>
    public class GoogleAIChatClient : IChatClient, IDisposable
    {
        private readonly string _apiKey;
        private readonly string _modelId;
        private readonly GoogleAI _googleAI;

        public GoogleAIChatClient(string apiKey, string modelId)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
            _googleAI = new GoogleAI(apiKey);
        }

        public ChatClientMetadata Metadata => new ChatClientMetadata("GoogleAIChatClient", null, _modelId);

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // Extract system instruction and build conversation history
            string systemInstruction = null;
            var conversationParts = new List<string>();

            foreach (var msg in chatMessages)
            {
                if (msg.Role == ChatRole.System)
                {
                    systemInstruction = msg.Text;
                }
                else
                {
                    // Build a combined prompt from user/assistant messages
                    var roleLabel = msg.Role == ChatRole.User ? "User" : "Assistant";
                    conversationParts.Add($"{roleLabel}: {msg.Text}");
                }
            }

            // Create model with system instruction if present
            GenerativeModel model;
            if (!string.IsNullOrEmpty(systemInstruction))
            {
                model = _googleAI.GenerativeModel(
                    model: _modelId,
                    systemInstruction: new GoogleContent(systemInstruction));
            }
            else
            {
                model = _googleAI.GenerativeModel(model: _modelId);
            }

            // Configure generation settings
            var generationConfig = new GoogleGenerationConfig();

            if (options?.ResponseFormat is ChatResponseFormatJson)
            {
                generationConfig.ResponseMimeType = "application/json";
            }

            if (options?.Temperature.HasValue == true)
                generationConfig.Temperature = (float)options.Temperature.Value;
            if (options?.TopP.HasValue == true)
                generationConfig.TopP = (float)options.TopP.Value;

            // Build the prompt from conversation parts
            var prompt = string.Join("\n\n", conversationParts);

            var response = await model.GenerateContent(prompt, generationConfig: generationConfig);

            var responseText = response?.Text ?? "";

            var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText));
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = response?.UsageMetadata?.PromptTokenCount ?? 0,
                OutputTokenCount = response?.UsageMetadata?.CandidatesTokenCount ?? 0,
                TotalTokenCount = response?.UsageMetadata?.TotalTokenCount ?? 0
            };

            return chatResponse;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }

        public object GetService(Type serviceType, object serviceKey = null)
        {
            if (serviceType == typeof(IChatClient))
                return this;
            return null;
        }

        public void Dispose()
        {
            // No unmanaged resources to dispose
        }
    }
}
