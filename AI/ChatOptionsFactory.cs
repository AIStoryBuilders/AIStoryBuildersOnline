using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Factory for creating ChatOptions with appropriate JSON response format settings per AI provider.
    /// </summary>
    public static class ChatOptionsFactory
    {
        /// <summary>
        /// Creates ChatOptions configured for JSON response format, appropriate for the given AI provider.
        /// </summary>
        public static ChatOptions CreateJsonOptions(string aiType, string model)
        {
            var options = new ChatOptions
            {
                ModelId = model,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
            };

            if (AICapabilities.SupportsCustomTemperature(model))
            {
                options.Temperature = 0.0f;
            }

            switch (aiType)
            {
                case "OpenAI":
                case "Azure OpenAI":
                    options.ResponseFormat = new ChatResponseFormatJson(null);
                    break;
                case "Anthropic":
                    // Anthropic doesn't have a native JSON mode toggle;
                    // JSON instruction is appended to the system prompt by the caller.
                    break;
                case "Google AI":
                    options.ResponseFormat = new ChatResponseFormatJson(null);
                    break;
            }

            return options;
        }

        /// <summary>
        /// Creates ChatOptions for plain text responses (no JSON mode).
        /// </summary>
        public static ChatOptions CreateTextOptions(string aiType, string model)
        {
            var options = new ChatOptions
            {
                ModelId = model,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
            };

            if (AICapabilities.SupportsCustomTemperature(model))
            {
                options.Temperature = 0.0f;
            }

            return options;
        }

        /// <summary>
        /// Creates ChatOptions for native tool-calling. For Gemini, parameter
        /// schemas are rebuilt into the minimal subset the Gemini validator
        /// accepts (see <see cref="GeminiToolSanitizer"/>).
        /// </summary>
        public static ChatOptions CreateToolOptions(string aiType, string model, IList<AITool> tools)
        {
            var safeTools = AICapabilities.IsGemini(aiType)
                ? GeminiToolSanitizer.SanitizeForGemini(tools)
                : tools;

            var options = new ChatOptions
            {
                ModelId = model,
                Tools = safeTools,
                TopP = 1.0f
            };

            if (AICapabilities.SupportsCustomTemperature(model))
            {
                options.Temperature = 0.7f;
            }

            return options;
        }
    }
}
