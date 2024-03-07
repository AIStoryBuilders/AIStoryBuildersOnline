using AIStoryBuilders.Models;
using AIStoryBuilders.Models.JSON;
using Newtonsoft.Json;
using System.Text.Json;

namespace AIStoryBuilders.Services
{
    public partial class AIStoryBuildersService
    {
        #region public async Task RestructureParagraphs(Chapter objChapter, int ParagraphNumber, RestructureType RestructureType)
        public async Task RestructureParagraphs(Chapter objChapter, int ParagraphNumber, RestructureType RestructureType)
        {
            try
            {
                string OldParagraphPath = "";
                string NewParagraphPath = "";
                var ChapterNameParts = objChapter.ChapterName.Split(' ');
                string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];
                var AIStoryBuildersParagraphsPath = $"{BasePath}/{objChapter.Story.Title}/Chapters/{ChapterName}";
                int CountOfParagraphs = await CountParagraphs(objChapter);

                // Loop through all remaining paragraphs and rename them
                if (RestructureType == RestructureType.Add)
                {
                    for (int i = CountOfParagraphs; ParagraphNumber <= i; i--)
                    {
                        OldParagraphPath = $"{AIStoryBuildersParagraphsPath}/Paragraph{i}.txt";
                        NewParagraphPath = $"{AIStoryBuildersParagraphsPath}/Paragraph{i + 1}.txt";

                        // Rename file
                        System.IO.File.Move(OldParagraphPath, NewParagraphPath);
                    }
                }
                else if (RestructureType == RestructureType.Delete)
                {
                    for (int i = ParagraphNumber; i <= CountOfParagraphs; i++)
                    {
                        OldParagraphPath = $"{AIStoryBuildersParagraphsPath}/Paragraph{i + 1}.txt";
                        NewParagraphPath = $"{AIStoryBuildersParagraphsPath}/Paragraph{i}.txt";

                        // Rename file
                        System.IO.File.Move(OldParagraphPath, NewParagraphPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("RestructureParagraphs: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }
        #endregion

        #region public async Task RestructureChapters(Chapter objChapter, RestructureType RestructureType)
        public async Task RestructureChapters(Chapter objChapter, RestructureType RestructureType)
        {
            try
            {
                await AIStoryBuildersChaptersService.LoadAIStoryBuildersChaptersAsync(objChapter.Story.Title);
                var AllChapters = AIStoryBuildersChaptersService.Chapters;

                int CountOfChapters = await CountChapters(objChapter.Story);

                if (RestructureType == RestructureType.Add)
                {
                    for (int i = CountOfChapters; objChapter.Sequence <= i; i--)
                    {
                        var ChapterName = "Chapter" + i.ToString();

                        // Get current Chapter
                        var objCurrentChapter = AllChapters.Where(x => x.chapter_name == ChapterName).FirstOrDefault();

                        if (objCurrentChapter == null)
                        {
                            continue;
                        }

                        // Delete Chapter
                        await AIStoryBuildersChaptersService.DeleteChapterAsync(objChapter.Story.Title, objCurrentChapter);

                        // Add 1 to the sequence
                        objCurrentChapter.sequence = objCurrentChapter.sequence + 1;

                        // Set new Chapter name
                        objCurrentChapter.chapter_name = "Chapter" + objCurrentChapter.sequence;

                        // Add Chapter
                        await AIStoryBuildersChaptersService.AddChapterAsync(objChapter.Story.Title, objCurrentChapter);
                    }
                }
                else if (RestructureType == RestructureType.Delete)
                {
                    for (int i = objChapter.Sequence; i <= CountOfChapters; i++)
                    {
                        var ChapterName = "Chapter" + (i + 1).ToString();

                        // Get current Chapter
                        var objCurrentChapter = AllChapters.Where(x => x.chapter_name == ChapterName).FirstOrDefault();

                        if (objCurrentChapter == null)
                        {
                            continue;
                        }

                        // Delete Chapter
                        await AIStoryBuildersChaptersService.DeleteChapterAsync(objChapter.Story.Title, objCurrentChapter);

                        // Add 1 to the sequence
                        objCurrentChapter.sequence = objCurrentChapter.sequence;

                        // Set new Chapter name
                        objCurrentChapter.chapter_name = "Chapter" + objCurrentChapter.sequence;

                        // Add Chapter
                        await AIStoryBuildersChaptersService.AddChapterAsync(objChapter.Story.Title, objCurrentChapter);

                    }
                }
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("RestructureChapters: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }
        #endregion
    }
}
