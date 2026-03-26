using AIStoryBuilders.Model;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Provides retry logic with structured error recovery for LLM calls.
    /// </summary>
    public class LlmCallHelper
    {
        private const int MaxRetries = 3;
        private readonly LogService _logService;

        public LlmCallHelper(LogService logService)
        {
            _logService = logService;
        }

        /// <summary>
        /// Calls the LLM with retry logic. On failure, appends the error message as a user message
        /// so the LLM can self-correct. Uses JsonRepairUtility for deterministic JSON repair.
        /// </summary>
        /// <typeparam name="T">The type to map the JSON response to.</typeparam>
        /// <param name="client">The IChatClient to use.</param>
        /// <param name="messages">The initial list of chat messages.</param>
        /// <param name="options">Chat options (model, temperature, etc.).</param>
        /// <param name="mapFn">A function that maps the parsed JObject to T.</param>
        /// <returns>The mapped result, or default(T) if all retries are exhausted.</returns>
        public async Task<T> CallLlmWithRetry<T>(
            IChatClient client,
            List<ChatMessage> messages,
            ChatOptions options,
            Func<JObject, T> mapFn)
        {
            // Work with a copy so we don't mutate the caller's list on retries
            var workingMessages = new List<ChatMessage>(messages);

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await _logService.WriteToLogAsync($"LlmCallHelper: Attempt {attempt}/{MaxRetries}");

                    var response = await client.GetResponseAsync(workingMessages, options);

                    // Log token usage
                    if (response.Usage != null)
                    {
                        await _logService.WriteToLogAsync(
                            $"Tokens — Input: {response.Usage.InputTokenCount}, " +
                            $"Output: {response.Usage.OutputTokenCount}, " +
                            $"Total: {response.Usage.TotalTokenCount}");
                    }

                    var rawText = response.Text ?? "";
                    await _logService.WriteToLogAsync($"LLM Response: {rawText}");

                    // Apply JSON repair
                    var cleanJson = JsonRepairUtility.ExtractAndRepair(rawText);

                    // Parse and map
                    var jObj = JObject.Parse(cleanJson);
                    T result = mapFn(jObj);

                    return result;
                }
                catch (Exception ex)
                {
                    await _logService.WriteToLogAsync(
                        $"LlmCallHelper: Attempt {attempt} failed — {ex.Message}");

                    if (attempt < MaxRetries)
                    {
                        // Append error context so the LLM can self-correct
                        workingMessages.Add(new ChatMessage(ChatRole.User,
                            $"The previous response was invalid. Error: {ex.Message}. " +
                            "Please try again and ensure the output is valid JSON."));
                    }
                }
            }

            await _logService.WriteToLogAsync("LlmCallHelper: Max retries exceeded, returning default.");
            return default;
        }

        /// <summary>
        /// Simplified variant for plain-text responses (no JSON parsing).
        /// Used for operations like GetStoryBeats.
        /// </summary>
        public async Task<string> CallLlmForText(
            IChatClient client,
            List<ChatMessage> messages,
            ChatOptions options)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await _logService.WriteToLogAsync($"LlmCallHelper (text): Attempt {attempt}/{MaxRetries}");

                    var response = await client.GetResponseAsync(messages, options);

                    // Log token usage
                    if (response.Usage != null)
                    {
                        await _logService.WriteToLogAsync(
                            $"Tokens — Input: {response.Usage.InputTokenCount}, " +
                            $"Output: {response.Usage.OutputTokenCount}, " +
                            $"Total: {response.Usage.TotalTokenCount}");
                    }

                    return response.Text ?? "";
                }
                catch (Exception ex)
                {
                    await _logService.WriteToLogAsync(
                        $"LlmCallHelper (text): Attempt {attempt} failed — {ex.Message}");

                    if (attempt < MaxRetries)
                    {
                        messages.Add(new ChatMessage(ChatRole.User,
                            $"The previous attempt failed with error: {ex.Message}. Please try again."));
                    }
                }
            }

            await _logService.WriteToLogAsync("LlmCallHelper (text): Max retries exceeded, returning empty.");
            return "";
        }
    }
}
