using AIStoryBuilders.Models;
using Blazored.LocalStorage;
using System.Security.Cryptography;
using System.Text;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Dynamic model listing service that fetches available models from AI providers
    /// and caches them in Blazored.LocalStorage.
    /// </summary>
    public class AIModelService
    {
        private readonly ILocalStorageService _localStorage;
        private readonly HttpClient _httpClient;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        public AIModelService(
            ILocalStorageService localStorage,
            HttpClient httpClient)
        {
            _localStorage = localStorage;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Gets available models for the given AI type. Returns cached results if fresh enough.
        /// </summary>
        public async Task<List<string>> GetModelsAsync(string aiType, string apiKey, string endpoint = null)
        {
            if (string.IsNullOrEmpty(apiKey))
                return GetDefaultModels(aiType);

            string cacheKey = GetCacheKey(aiType, apiKey);

            // Check cache
            var cached = await LoadFromCache(cacheKey);
            if (cached != null)
                return cached;

            // Fetch from provider
            var models = await FetchModels(aiType, apiKey, endpoint);

            // Save to cache
            await SaveToCache(cacheKey, models);

            return models;
        }

        /// <summary>
        /// Forces a refresh of the model list, bypassing cache.
        /// </summary>
        public async Task<List<string>> RefreshModelsAsync(string aiType, string apiKey, string endpoint = null)
        {
            var models = await FetchModels(aiType, apiKey, endpoint);
            string cacheKey = GetCacheKey(aiType, apiKey);
            await SaveToCache(cacheKey, models);
            return models;
        }

        private async Task<List<string>> FetchModels(string aiType, string apiKey, string endpoint)
        {
            try
            {
                return aiType switch
                {
                    "OpenAI" => await FetchFromOpenAI(apiKey),
                    "Azure OpenAI" => await FetchFromAzure(apiKey, endpoint),
                    "Anthropic" => await FetchFromAnthropic(apiKey),
                    "Google AI" => await FetchFromGoogle(apiKey),
                    _ => GetDefaultModels(aiType)
                };
            }
            catch
            {
                // On failure, return defaults
                return GetDefaultModels(aiType);
            }
        }

        private async Task<List<string>> FetchFromOpenAI(string apiKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return GetDefaultModels("OpenAI");

            var content = await response.Content.ReadAsStringAsync();
            var json = Newtonsoft.Json.Linq.JObject.Parse(content);
            var models = new List<string>();

            if (json["data"] is Newtonsoft.Json.Linq.JArray data)
            {
                foreach (var model in data)
                {
                    var id = model["id"]?.ToString() ?? "";
                    // Filter to chat models
                    if (id.Contains("gpt") || id.Contains("o1") || id.Contains("o3") || id.Contains("o4"))
                    {
                        models.Add(id);
                    }
                }
            }

            models.Sort();
            return models.Count > 0 ? models : GetDefaultModels("OpenAI");
        }

        private async Task<List<string>> FetchFromAzure(string apiKey, string endpoint)
        {
            // For Azure OpenAI, model names are deployment names — user typically enters them manually
            // Return a basic list
            return await Task.FromResult(GetDefaultModels("Azure OpenAI"));
        }

        private List<string> GetAnthropicModels()
        {
            // Fallback list used when the Anthropic models API can't be reached.
            return new List<string>
            {
                "claude-sonnet-4-20250514",
                "claude-3-5-haiku-20241022",
                "claude-3-5-sonnet-20241022",
                "claude-3-opus-20240229",
                "claude-3-haiku-20240307"
            };
        }

        private async Task<List<string>> FetchFromAnthropic(string apiKey)
        {
            try
            {
                var models = new List<string>();
                string url = "https://api.anthropic.com/v1/models?limit=1000";

                // Page through results in case there are more than the page size.
                while (!string.IsNullOrEmpty(url))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                    request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                    request.Headers.TryAddWithoutValidation("anthropic-dangerous-direct-browser-access", "true");

                    var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                        return models.Count > 0 ? models : GetAnthropicModels();

                    var content = await response.Content.ReadAsStringAsync();
                    var json = Newtonsoft.Json.Linq.JObject.Parse(content);

                    if (json["data"] is Newtonsoft.Json.Linq.JArray data)
                    {
                        foreach (var model in data)
                        {
                            var id = model["id"]?.ToString();
                            if (!string.IsNullOrEmpty(id))
                                models.Add(id);
                        }
                    }

                    var hasMore = json["has_more"]?.ToObject<bool>() ?? false;
                    var lastId = json["last_id"]?.ToString();
                    if (hasMore && !string.IsNullOrEmpty(lastId))
                        url = $"https://api.anthropic.com/v1/models?limit=1000&after_id={Uri.EscapeDataString(lastId)}";
                    else
                        url = null;
                }

                // De-duplicate and sort newest-first (Anthropic IDs sort reasonably alphabetically; reverse for newest first).
                models = models.Distinct().OrderByDescending(m => m, StringComparer.Ordinal).ToList();
                return models.Count > 0 ? models : GetAnthropicModels();
            }
            catch
            {
                return GetAnthropicModels();
            }
        }

        private async Task<List<string>> FetchFromGoogle(string apiKey)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}&pageSize=200");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return GetDefaultModels("Google AI");

                var content = await response.Content.ReadAsStringAsync();
                var json = Newtonsoft.Json.Linq.JObject.Parse(content);
                var models = new List<string>();

                if (json["models"] is Newtonsoft.Json.Linq.JArray modelsArray)
                {
                    foreach (var model in modelsArray)
                    {
                        var name = model["name"]?.ToString() ?? "";
                        var supportedMethods = model["supportedGenerationMethods"]?.ToObject<List<string>>() ?? new List<string>();

                        // Only include models that support generateContent
                        if (supportedMethods.Contains("generateContent"))
                        {
                            // Strip "models/" prefix
                            if (name.StartsWith("models/"))
                                name = name.Substring(7);
                            models.Add(name);
                        }
                    }
                }

                models.Sort();
                return models.Count > 0 ? models : GetDefaultModels("Google AI");
            }
            catch
            {
                return GetDefaultModels("Google AI");
            }
        }

        public List<string> GetDefaultModels(string aiType)
        {
            return aiType switch
            {
                "OpenAI" => new List<string> { "gpt-5", "gpt-5-mini", "gpt-4o", "gpt-4o-mini", "gpt-3.5-turbo" },
                "Azure OpenAI" => new List<string>(),
                "Anthropic" => GetAnthropicModels(),
                "Google AI" => new List<string> { "gemini-2.0-flash", "gemini-2.0-pro", "gemini-1.5-flash", "gemini-1.5-pro" },
                _ => new List<string>()
            };
        }

        private string GetCacheKey(string aiType, string apiKey)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(apiKey ?? ""));
            var hashString = Convert.ToBase64String(hash).Substring(0, 8);
            return $"ModelCache_{aiType}_{hashString}";
        }

        private async Task<List<string>> LoadFromCache(string cacheKey)
        {
            try
            {
                var entry = await _localStorage.GetItemAsync<ModelCacheEntry>(cacheKey);
                if (entry != null && (DateTime.UtcNow - entry.CachedAt) < CacheDuration)
                {
                    return entry.Models;
                }
            }
            catch
            {
                // Cache miss
            }
            return null;
        }

        private async Task SaveToCache(string cacheKey, List<string> models)
        {
            try
            {
                await _localStorage.SetItemAsync(cacheKey, new ModelCacheEntry
                {
                    Models = models,
                    CachedAt = DateTime.UtcNow
                });
            }
            catch
            {
                // Silently fail cache writes
            }
        }
    }
}
