using AIStoryBuilders.AI;
using AIStoryBuilders.Models.JSON;
using System.Text.Json;

namespace AIStoryBuilders.Services
{
    /// <summary>
    /// Handles context-window budget trimming for WriteParagraph.
    /// Trims PreviousParagraphs and RelatedParagraphs to fit within the model's context window.
    /// </summary>
    public static class MasterStoryBuilder
    {
        /// <summary>
        /// Trims the master story object so the total prompt fits within the model's token budget.
        /// </summary>
        public static JSONMasterStory TrimToFit(
            JSONMasterStory masterStory,
            string systemPrompt,
            string userTemplate,
            string modelId)
        {
            var estimator = new TokenEstimator();
            int maxTokens = estimator.GetMaxPromptTokens(modelId);

            // Calculate base token cost: system prompt + story metadata
            int baseTokens = estimator.EstimateTokens(systemPrompt);
            baseTokens += estimator.EstimateTokens(userTemplate);
            baseTokens += estimator.EstimateTokens(masterStory.StoryTitle ?? "");
            baseTokens += estimator.EstimateTokens(masterStory.StoryStyle ?? "");
            baseTokens += estimator.EstimateTokens(masterStory.StorySynopsis ?? "");
            baseTokens += estimator.EstimateTokens(masterStory.SystemMessage ?? "");

            // Current paragraph and location
            if (masterStory.CurrentParagraph != null)
            {
                baseTokens += estimator.EstimateTokens(masterStory.CurrentParagraph.contents ?? "");
            }

            if (masterStory.CurrentLocation != null)
            {
                baseTokens += estimator.EstimateTokens(
                    JsonSerializer.Serialize(masterStory.CurrentLocation));
            }

            if (masterStory.CharacterList != null)
            {
                baseTokens += estimator.EstimateTokens(
                    JsonSerializer.Serialize(masterStory.CharacterList));
            }

            if (masterStory.CurrentChapter != null)
            {
                baseTokens += estimator.EstimateTokens(
                    masterStory.CurrentChapter.chapter_name ?? "");
                baseTokens += estimator.EstimateTokens(
                    masterStory.CurrentChapter.chapter_synopsis ?? "");
            }

            if (!string.IsNullOrEmpty(masterStory.TimelineSummary))
            {
                baseTokens += estimator.EstimateTokens(masterStory.TimelineSummary);
            }

            int remaining = maxTokens - baseTokens;
            if (remaining < 0) remaining = 0;

            // Trim PreviousParagraphs — keep the most-recent paragraphs that fit
            // (iterate from the end so we preserve recent context)
            if (masterStory.PreviousParagraphs != null && masterStory.PreviousParagraphs.Count > 0)
            {
                var trimmedPrevious = new List<JSONParagraphs>();
                int usedTokens = 0;
                int budget = remaining / 2; // Reserve half for related

                for (int i = masterStory.PreviousParagraphs.Count - 1; i >= 0; i--)
                {
                    var paragraph = masterStory.PreviousParagraphs[i];
                    int paragraphTokens = estimator.EstimateTokens(paragraph.contents ?? "");
                    if (usedTokens + paragraphTokens <= budget)
                    {
                        trimmedPrevious.Insert(0, paragraph); // maintain original order
                        usedTokens += paragraphTokens;
                    }
                    else
                    {
                        break;
                    }
                }

                masterStory.PreviousParagraphs = trimmedPrevious;
                remaining -= usedTokens;
            }

            // Trim RelatedParagraphs — keep the most-recent entries that fit
            if (masterStory.RelatedParagraphs != null && masterStory.RelatedParagraphs.Count > 0)
            {
                var trimmedRelated = new List<JSONParagraphs>();
                int usedTokens = 0;

                for (int i = masterStory.RelatedParagraphs.Count - 1; i >= 0; i--)
                {
                    var paragraph = masterStory.RelatedParagraphs[i];
                    int paragraphTokens = estimator.EstimateTokens(paragraph.contents ?? "");
                    if (usedTokens + paragraphTokens <= remaining)
                    {
                        trimmedRelated.Insert(0, paragraph); // maintain original order
                        usedTokens += paragraphTokens;
                    }
                    else
                    {
                        break;
                    }
                }

                masterStory.RelatedParagraphs = trimmedRelated;
            }

            return masterStory;
        }
    }
}
