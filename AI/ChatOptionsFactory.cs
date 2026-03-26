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
                Temperature = 0.0f,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
            };

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
            return new ChatOptions
            {
                ModelId = model,
                Temperature = 0.0f,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
            };
        }
    }
}
