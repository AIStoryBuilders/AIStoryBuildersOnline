using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Estimates token counts using a character-based heuristic and provides
    /// context window size lookups for various models.
    /// </summary>
    public class TokenEstimator
    {
        private const float BudgetRatio = 0.75f;

        /// <summary>
        /// Estimates the number of tokens in a text string.
        /// Heuristic: tokens ≈ characterCount / 4.0
        /// </summary>
        public int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return (int)Math.Ceiling(text.Length / 4.0);
        }

        /// <summary>
        /// Estimates the total number of tokens across a list of chat messages.
        /// </summary>
        public int EstimateTokens(IEnumerable<ChatMessage> messages)
        {
            if (messages == null)
                return 0;

            int total = 0;
            foreach (var msg in messages)
            {
                total += EstimateTokens(msg.Text ?? "");
                total += 4; // overhead per message (role tokens, formatting)
            }
            return total;
        }

        /// <summary>
        /// Returns the maximum number of tokens that should be used for the prompt,
        /// based on the model's context window and the budget ratio.
        /// </summary>
        public int GetMaxPromptTokens(string modelId)
        {
            int contextWindow = GetContextWindowSize(modelId);
            return (int)(contextWindow * BudgetRatio);
        }

        /// <summary>
        /// Returns the context window size for a given model.
        /// </summary>
        public int GetContextWindowSize(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return 8192;

            string lower = modelId.ToLowerInvariant();

            if (lower.Contains("gpt-5-mini"))
                return 128_000;
            if (lower.Contains("gpt-5"))
                return 128_000;
            if (lower.Contains("gpt-4o"))
                return 128_000;
            if (lower.Contains("gpt-4-turbo"))
                return 128_000;
            if (lower.Contains("gpt-4"))
                return 8_192;
            if (lower.Contains("gpt-3.5"))
                return 16_384;
            if (lower.StartsWith("claude"))
                return 200_000;
            if (lower.StartsWith("gemini"))
                return 1_000_000;

            return 8_192; // Default
        }
    }
}
