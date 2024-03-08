using AIStoryBuilders.Models;
using AIStoryBuilders.Models.JSON;
using AIStoryBuilders.Models.LocalStorage;
using Newtonsoft.Json;
using System.Text.Json;

namespace AIStoryBuilders.Services
{
    public partial class AIStoryBuildersService
    {
        #region public async Task RestructureParagraphs(Chapter objChapter, int ParagraphNumber, RestructureType RestructureType)
        public async Task RestructureParagraphs(Models.Chapter objChapter, int ParagraphNumber, RestructureType RestructureType)
        {
            try
            {
                // Get current Chapter
                await AIStoryBuildersChaptersService.LoadAIStoryBuildersChaptersAsync(objChapter.Story.Title);
                var AllChapters = AIStoryBuildersChaptersService.Chapters;

                var ChapterName = objChapter.ChapterName.Replace(" ","");
                var objCurrentChapter = AllChapters.Where(x => x.chapter_name == ChapterName).FirstOrDefault();

                // Get all paragraphs
                var ColParagraphs = objCurrentChapter.paragraphs;

                int CountOfParagraphs = ColParagraphs.Count;

                List<Paragraphs> lstParagraphs = new List<Paragraphs>();

                // Loop through all remaining paragraphs and rename them
                if (RestructureType == RestructureType.Add)
                {
                    for (int i = CountOfParagraphs; ParagraphNumber <= i; i--)
                    {
                        Paragraphs objParagraphs = new Paragraphs();

                        // Get current Paragraph
                        var objCurrentParagraph = ColParagraphs.Where(x => x.sequence == i).FirstOrDefault();

                        if (objCurrentParagraph == null)
                        {
                            continue;
                        }

                        objParagraphs.sequence = objCurrentParagraph.sequence + 1;
                        objParagraphs.character_names = objCurrentParagraph.character_names;
                        objParagraphs.contents = objCurrentParagraph.contents;
                        objParagraphs.timeline_name = objCurrentParagraph.timeline_name;
                        objParagraphs.location_name = objCurrentParagraph.location_name;
                        objParagraphs.embedding = objCurrentParagraph.embedding;

                        lstParagraphs.Add(objParagraphs);
                    }
                }
                else if (RestructureType == RestructureType.Delete)
                {
                    for (int i = ParagraphNumber; i <= CountOfParagraphs; i++)
                    {
                        Paragraphs objParagraphs = new Paragraphs();

                        // Get current Paragraph
                        var objCurrentParagraph = ColParagraphs.Where(x => x.sequence == (i + 1)).FirstOrDefault();

                        if (objCurrentParagraph == null)
                        {
                            continue;
                        }

                        objParagraphs.sequence = objCurrentParagraph.sequence;
                        objParagraphs.character_names = objCurrentParagraph.character_names;
                        objParagraphs.contents = objCurrentParagraph.contents;
                        objParagraphs.timeline_name = objCurrentParagraph.timeline_name;
                        objParagraphs.location_name = objCurrentParagraph.location_name;
                        objParagraphs.embedding = objCurrentParagraph.embedding;

                        lstParagraphs.Add(objParagraphs);
                    }
                }

                // Delete Chapter
                await AIStoryBuildersChaptersService.DeleteChapterAsync(objChapter.Story.Title, objCurrentChapter);

                // Update Chapter
                objCurrentChapter.paragraphs = lstParagraphs;

                // Add Chapter
                await AIStoryBuildersChaptersService.AddChapterAsync(objChapter.Story.Title, objCurrentChapter);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("RestructureParagraphs: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }
        #endregion

        #region public async Task RestructureChapters(Chapter objChapter, RestructureType RestructureType)
        public async Task RestructureChapters(Models.Chapter objChapter, RestructureType RestructureType)
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
