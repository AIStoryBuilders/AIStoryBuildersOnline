using Microsoft.Extensions.AI;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// IChatClient implementation that calls the Anthropic REST API directly.
    /// Uses HttpClient instead of the Anthropic SDK to avoid SocketsHttpHandler
    /// and CORS issues in Blazor WebAssembly.
    /// </summary>
    public class AnthropicChatClient : IChatClient, IDisposable
    {
        private readonly string _apiKey;
        private readonly string _modelId;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";

        public AnthropicChatClient(string apiKey, string modelId, HttpClient httpClient = null)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));

            // Create a dedicated HttpClient for Anthropic API calls
            // to avoid BaseAddress conflicts with the DI HttpClient
            _httpClient = new HttpClient();
            _ownsHttpClient = true;
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.DefaultRequestHeaders.Add("anthropic-dangerous-direct-browser-access", "true");
        }

        public ChatClientMetadata Metadata => new ChatClientMetadata("AnthropicChatClient", null, _modelId);

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // Build the request body
            var systemText = "";
            var messages = new List<object>();

            foreach (var msg in chatMessages)
            {
                if (msg.Role == ChatRole.System)
                {
                    systemText = msg.Text;
                }
                else if (msg.Role == ChatRole.User)
                {
                    messages.Add(new { role = "user", content = msg.Text });
                }
                else if (msg.Role == ChatRole.Assistant)
                {
                    messages.Add(new { role = "assistant", content = msg.Text });
                }
            }

            // Anthropic requires at least one user message.
            // If only a system message was provided, send it as a user message instead.
            if (messages.Count == 0 && !string.IsNullOrEmpty(systemText))
            {
                messages.Add(new { role = "user", content = systemText });
                systemText = "";
            }

            var requestBody = new Dictionary<string, object>
            {
                ["model"] = _modelId,
                ["max_tokens"] = 4096,
                ["messages"] = messages
            };

            if (!string.IsNullOrEmpty(systemText))
            {
                requestBody["system"] = systemText;
            }

            if (options?.Temperature.HasValue == true)
                requestBody["temperature"] = options.Temperature.Value;
            if (options?.TopP.HasValue == true)
                requestBody["top_p"] = options.TopP.Value;

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Anthropic API error ({httpResponse.StatusCode}): {responseJson}");
            }

            // Parse the response
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var responseText = "";
            if (root.TryGetProperty("content", out var contentArray))
            {
                foreach (var block in contentArray.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var type) && type.GetString() == "text")
                    {
                        if (block.TryGetProperty("text", out var text))
                        {
                            responseText += text.GetString();
                        }
                    }
                }
            }

            var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText));

            if (root.TryGetProperty("usage", out var usage))
            {
                var inputTokens = usage.TryGetProperty("input_tokens", out var inp) ? inp.GetInt32() : 0;
                var outputTokens = usage.TryGetProperty("output_tokens", out var outp) ? outp.GetInt32() : 0;
                chatResponse.Usage = new UsageDetails
                {
                    InputTokenCount = inputTokens,
                    OutputTokenCount = outputTokens,
                    TotalTokenCount = inputTokens + outputTokens
                };
            }

            return chatResponse;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Streaming is not supported by AnthropicChatClient.");
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
            if (_ownsHttpClient)
            {
                _httpClient?.Dispose();
            }
        }
    }
}
