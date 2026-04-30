using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// IChatClient implementation that calls the Gemini REST endpoint directly
    /// using the DI-injected <see cref="HttpClient"/>. Avoids the
    /// <c>Mscc.GenerativeAI</c> SDK because that package's request pipeline
    /// touches <c>HttpClientHandler</c> properties that throw
    /// <see cref="PlatformNotSupportedException"/> on Blazor WebAssembly.
    /// </summary>
    public class GoogleAIChatClient : IChatClient, IDisposable
    {
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

        private readonly string _apiKey;
        private readonly string _modelId;
        private readonly HttpClient _httpClient;

        public GoogleAIChatClient(string apiKey, string modelId, HttpClient httpClient)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public ChatClientMetadata Metadata => new ChatClientMetadata("GoogleAIChatClient", null, _modelId);

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var body = BuildRequestBody(chatMessages, options);
            var url = $"{BaseUrl}/models/{Uri.EscapeDataString(_modelId)}:generateContent?key={Uri.EscapeDataString(_apiKey)}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Gemini generateContent failed ({(int)response.StatusCode} {response.ReasonPhrase}): {raw}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0)
            {
                string blockReason = null;
                if (root.TryGetProperty("promptFeedback", out var pf)
                    && pf.TryGetProperty("blockReason", out var br)
                    && br.ValueKind == JsonValueKind.String)
                {
                    blockReason = br.GetString();
                }
                throw new InvalidOperationException(
                    "Gemini returned no candidates" + (blockReason != null ? $" (blockReason={blockReason})" : "") + ".");
            }

            var sb = new StringBuilder();
            var first = candidates[0];
            if (first.TryGetProperty("content", out var content)
                && content.TryGetProperty("parts", out var parts)
                && parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(text.GetString());
                    }
                }
            }

            var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, sb.ToString()));

            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                chatResponse.Usage = new UsageDetails
                {
                    InputTokenCount = GetLongOrZero(usage, "promptTokenCount"),
                    OutputTokenCount = GetLongOrZero(usage, "candidatesTokenCount"),
                    TotalTokenCount = GetLongOrZero(usage, "totalTokenCount")
                };
            }

            return chatResponse;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // SSE streaming over BrowserHttpHandler is tricky; for now, fall back
            // to a single non-streaming call and yield the full text once. This
            // is still strictly better than the previous `yield break;`.
            var full = await GetResponseAsync(chatMessages, options, cancellationToken);
            var text = full?.Messages?.FirstOrDefault()?.Text;
            if (!string.IsNullOrEmpty(text))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, text);
            }
        }

        public object GetService(Type serviceType, object serviceKey = null)
        {
            if (serviceType == typeof(IChatClient))
                return this;
            return null;
        }

        public void Dispose()
        {
            // HttpClient is owned by DI.
        }

        private object BuildRequestBody(IEnumerable<ChatMessage> chatMessages, ChatOptions options)
        {
            string systemInstruction = null;
            var contents = new List<object>();

            foreach (var msg in chatMessages)
            {
                var text = msg.Text ?? "";
                if (msg.Role == ChatRole.System)
                {
                    systemInstruction = string.IsNullOrEmpty(systemInstruction)
                        ? text
                        : systemInstruction + "\n\n" + text;
                }
                else
                {
                    string role = msg.Role == ChatRole.Assistant ? "model" : "user";
                    contents.Add(new
                    {
                        role,
                        parts = new[] { new { text } }
                    });
                }
            }

            var generationConfig = new Dictionary<string, object>();
            if (options?.Temperature.HasValue == true && AICapabilities.SupportsCustomTemperature(_modelId))
                generationConfig["temperature"] = (float)options.Temperature.Value;
            if (options?.TopP.HasValue == true)
                generationConfig["topP"] = (float)options.TopP.Value;
            if (options?.ResponseFormat is ChatResponseFormatJson)
                generationConfig["responseMimeType"] = "application/json";

            // Gemini requires `contents` to be non-empty. If the caller only
            // supplied a system message (e.g. the Settings "Test Access"
            // probe), promote it to a user turn so the request validates.
            if (contents.Count == 0)
            {
                var seed = string.IsNullOrEmpty(systemInstruction) ? "Hello." : systemInstruction;
                contents.Add(new
                {
                    role = "user",
                    parts = new[] { new { text = seed } }
                });
                if (!string.IsNullOrEmpty(systemInstruction))
                {
                    // We've moved the only message into contents; clear the
                    // top-level systemInstruction to avoid duplicating it.
                    systemInstruction = null;
                }
            }

            var body = new Dictionary<string, object>
            {
                ["contents"] = contents
            };
            if (!string.IsNullOrEmpty(systemInstruction))
            {
                body["systemInstruction"] = new
                {
                    parts = new[] { new { text = systemInstruction } }
                };
            }
            if (generationConfig.Count > 0)
            {
                body["generationConfig"] = generationConfig;
            }
            return body;
        }

        private static long GetLongOrZero(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
            {
                if (v.TryGetInt64(out var l)) return l;
            }
            return 0;
        }
    }
}
