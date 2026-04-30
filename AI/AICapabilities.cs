namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Centralised "does this provider/model support X" rules.
    /// This is the single place that names specific model-id prefixes.
    /// </summary>
    internal static class AICapabilities
    {
        public static bool IsGemini(string aiType) =>
            string.Equals(aiType, "Google AI", StringComparison.OrdinalIgnoreCase);

        public static bool IsAnthropic(string aiType) =>
            string.Equals(aiType, "Anthropic", StringComparison.OrdinalIgnoreCase);

        public static bool IsOpenAI(string aiType) =>
            string.Equals(aiType, "OpenAI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(aiType, "Azure OpenAI", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// OpenAI GPT-5 and o-series reasoning models reject any explicit
        /// temperature (must be the provider default of 1.0).
        /// </summary>
        public static bool SupportsCustomTemperature(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return true;
            var id = modelId.Trim().ToLowerInvariant();
            if (id.StartsWith("gpt-5")) return false;
            if (id.StartsWith("o1") || id.StartsWith("o3") || id.StartsWith("o4")) return false;
            return true;
        }

        /// <summary>
        /// Newer Claude models (Opus 4.x and later) have deprecated the
        /// <c>temperature</c> request parameter and reject any explicit value.
        /// </summary>
        public static bool AnthropicSupportsTemperature(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return true;
            var id = modelId.Trim().ToLowerInvariant();
            // claude-opus-4, claude-opus-4-7, claude-sonnet-4-5, etc.
            if (id.StartsWith("claude-opus-4")) return false;
            if (id.StartsWith("claude-sonnet-4")) return false;
            return true;
        }

        /// <summary>
        /// Whether the provider needs the "run tools locally and re-ask without
        /// Tools" workaround for fragile second-turn tool calling on Gemini.
        /// </summary>
        public static bool RequiresGeminiToolWorkaround(string aiType) => IsGemini(aiType);
    }
}
