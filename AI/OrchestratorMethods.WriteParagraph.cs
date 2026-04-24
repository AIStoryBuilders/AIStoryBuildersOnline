using AIStoryBuilders.Model;
using AIStoryBuilders.Models.JSON;
using AIStoryBuilders.Models;
using AIStoryBuilders.Services;
using Microsoft.Extensions.AI;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace AIStoryBuilders.AI
{
    public partial class OrchestratorMethods
    {
        #region public async Task<string> WriteParagraph(JSONMasterStory objJSONMasterStory, AIPrompt paramAIPrompt, string GPTModel)
        public async Task<string> WriteParagraph(JSONMasterStory objJSONMasterStory, AIPrompt paramAIPrompt, string GPTModel)
        {
            await EnsureSettingsLoaded();

            await LogService.WriteToLogAsync($"WriteParagraph using {GPTModel} - Start");

            // Apply token budget trimming
            objJSONMasterStory = MasterStoryBuilder.TrimToFit(
                objJSONMasterStory,
                PromptTemplateService.WriteParagraph_System,
                PromptTemplateService.WriteParagraph_User,
                GPTModel);

            // Build prompt template values
            var values = BuildWriteParagraphValues(objJSONMasterStory, paramAIPrompt);

            var messages = _promptService.BuildMessages(
                PromptTemplateService.WriteParagraph_System,
                PromptTemplateService.WriteParagraph_User,
                values);

            await LogService.WriteToLogAsync($"Prompt: {messages[0].Text}");

            IChatClient client = CreateChatClient();
            var options = ChatOptionsFactory.CreateJsonOptions(SettingsService.AIType, GPTModel);

            string strParagraphOutput = await _llmCallHelper.CallLlmWithRetry<string>(
                client, messages, options,
                jObj => jObj["paragraph_content"]?.ToString() ?? "");

            return strParagraphOutput ?? "";
        }
        #endregion

        // Methods

        #region private Dictionary<string, string> BuildWriteParagraphValues(JSONMasterStory paramJSONMasterStory, AIPrompt paramAIPrompt)
        private Dictionary<string, string> BuildWriteParagraphValues(JSONMasterStory paramJSONMasterStory, AIPrompt paramAIPrompt)
        {
            var values = new Dictionary<string, string>();

            // SystemMessage
            values["SystemMessage"] = !string.IsNullOrEmpty(paramJSONMasterStory.SystemMessage)
                ? $"#### Please follow all these directions when creating the paragraph: {paramJSONMasterStory.SystemMessage.Trim()}. \n"
                : "";

            // StoryTitle
            values["StoryTitle"] = !string.IsNullOrEmpty(paramJSONMasterStory.StoryTitle)
                ? $"#### The story title is {paramJSONMasterStory.StoryTitle.Trim()}. \n"
                : "";

            // StoryStyle
            values["StoryStyle"] = !string.IsNullOrEmpty(paramJSONMasterStory.StoryStyle)
                ? $"#### The story style is {paramJSONMasterStory.StoryStyle.Trim()}. \n"
                : "";

            // StorySynopsis
            values["StorySynopsis"] = !string.IsNullOrEmpty(paramJSONMasterStory.StorySynopsis)
                ? $"#### The story synopsis is {paramJSONMasterStory.StorySynopsis.Trim()}. \n"
                : "";

            // CurrentChapter
            string currentChapterBlock = "";
            if (paramJSONMasterStory.CurrentChapter != null)
            {
                string ChapterSequence = paramJSONMasterStory.CurrentChapter.chapter_name?.Split(' ').LastOrDefault() ?? "1";
                currentChapterBlock += $"#### This is chapter number {ChapterSequence} in the story. \n";
                currentChapterBlock += $"#### This is the synopsis of chapter {ChapterSequence}: {paramJSONMasterStory.CurrentChapter.chapter_synopsis}. \n";
            }
            values["CurrentChapter"] = currentChapterBlock;

            // PreviousParagraphs
            string prevParagraphs = "";
            if (paramJSONMasterStory.PreviousParagraphs != null && paramJSONMasterStory.CurrentChapter != null)
            {
                string ChapterSequence = paramJSONMasterStory.CurrentChapter.chapter_name?.Split(' ').LastOrDefault() ?? "1";
                var jsonPrev = JsonSerializer.Serialize(paramJSONMasterStory.PreviousParagraphs);
                prevParagraphs = $"#### This is the JSON representation of the previous paragraphs in chapter {ChapterSequence}: {jsonPrev}. \n";
            }
            values["PreviousParagraphs"] = prevParagraphs;

            // CurrentParagraph, CurrentLocation, CharacterList, RelatedParagraphs, PromptInstruction
            string currentParagraphBlock = "";
            if (paramJSONMasterStory.CurrentParagraph != null && !string.IsNullOrEmpty(paramJSONMasterStory.CurrentParagraph.contents))
            {
                currentParagraphBlock += "#### This is the current contents of the next paragraph in the chapter: \n" +
                    paramJSONMasterStory.CurrentParagraph.contents + "\n";

                if (paramJSONMasterStory.CurrentLocation != null)
                {
                    var jsonLoc = JsonSerializer.Serialize(paramJSONMasterStory.CurrentLocation);
                    currentParagraphBlock += $"#### This is the JSON representation of the location description of the paragraph: {jsonLoc}. \n";
                }

                if (paramJSONMasterStory.CharacterList != null)
                {
                    var jsonChars = JsonSerializer.Serialize(paramJSONMasterStory.CharacterList);
                    currentParagraphBlock += $"#### This is the JSON representation of the characters in the paragraph and their descriptions: {jsonChars}. \n";
                }

                if (paramJSONMasterStory.RelatedParagraphs != null)
                {
                    var jsonRelated = JsonSerializer.Serialize(paramJSONMasterStory.RelatedParagraphs);
                    currentParagraphBlock += $"#### This is the JSON representation of related paragraphs that occur in previous chapters: {jsonRelated}. \n";
                }

                if (!string.IsNullOrWhiteSpace(paramAIPrompt.AIPromptText))
                {
                    currentParagraphBlock += "#### Use the following instructions in re-writing the paragraph: \n" +
                        paramAIPrompt.AIPromptText.Trim() + "\n";
                }
                else
                {
                    currentParagraphBlock += "#### Use the following instructions in writing the next paragraph in the chapter: \n" +
                        "Continue from the last paragraph. \n";
                }
            }
            else
            {
                if (paramJSONMasterStory.CurrentLocation != null)
                {
                    var jsonLoc = JsonSerializer.Serialize(paramJSONMasterStory.CurrentLocation);
                    currentParagraphBlock += $"#### This is the JSON representation of the location description of the paragraph: {jsonLoc}. \n";
                }

                if (paramJSONMasterStory.CharacterList != null)
                {
                    var jsonChars = JsonSerializer.Serialize(paramJSONMasterStory.CharacterList);
                    currentParagraphBlock += $"#### This is the JSON representation of the characters in the paragraph and their descriptions: {jsonChars}. \n";
                }

                if (paramJSONMasterStory.RelatedParagraphs != null)
                {
                    var jsonRelated = JsonSerializer.Serialize(paramJSONMasterStory.RelatedParagraphs);
                    currentParagraphBlock += $"#### This is the JSON representation of related paragraphs that occur in previous chapters: {jsonRelated}. \n";
                }

                if (!string.IsNullOrWhiteSpace(paramAIPrompt.AIPromptText))
                {
                    currentParagraphBlock += "#### Use the following instructions in writing the next paragraph in the chapter: \n" +
                        paramAIPrompt.AIPromptText.Trim() + "\n";
                }
                else
                {
                    currentParagraphBlock += "#### Use the following instructions in writing the next paragraph in the chapter: \n" +
                        "Write the next paragraph in the chapter. \n";
                }
            }
            values["CurrentParagraph"] = currentParagraphBlock;
            values["CurrentLocation"] = "";
            values["CharacterList"] = "";
            values["RelatedParagraphs"] = "";
            values["PromptInstruction"] = "";
            values["TimelineSummary"] = !string.IsNullOrWhiteSpace(paramJSONMasterStory.TimelineSummary)
                ? $"\n<timeline_summary>\n{paramJSONMasterStory.TimelineSummary.Trim()}\n</timeline_summary>\n"
                : "";
            values["NumberOfWords"] = paramAIPrompt.NumberOfWords.ToString();

            return values;
        }
        #endregion
    }
}
