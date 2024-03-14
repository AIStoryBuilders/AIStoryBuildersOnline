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

                var ChapterName = objChapter.ChapterName.Replace(" ", "");
                var objCurrentChapter = AllChapters.Where(x => x.chapter_name == ChapterName).FirstOrDefault();

                // Get all paragraphs
                var ColParagraphs = objCurrentChapter.paragraphs;

                int CountOfParagraphs = ColParagraphs.Count;

                List<Paragraphs> lstParagraphs = new List<Paragraphs>();

                // Loop through all remaining paragraphs and add 1 to the sequence above the current paragraph
                if (RestructureType == RestructureType.Add)
                {
                    foreach (Paragraphs objCurrentParagraph in ColParagraphs)
                    {
                        Paragraphs objParagraphs = new Paragraphs();

                        if (objCurrentParagraph.sequence >= ParagraphNumber)
                        {
                            objParagraphs.sequence = (objCurrentParagraph.sequence + 1);
                        }
                        else
                        {
                            objParagraphs.sequence = objCurrentParagraph.sequence;
                        }

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
                    foreach (Paragraphs objCurrentParagraph in ColParagraphs)
                    {
                        Paragraphs objParagraphs = new Paragraphs();

                        if (objCurrentParagraph.sequence >= ParagraphNumber)
                        {
                            objParagraphs.sequence = (objCurrentParagraph.sequence - 1);
                        }
                        else
                        {
                            objParagraphs.sequence = objCurrentParagraph.sequence;
                        }

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
                // Final List of Chapters
                List<Models.LocalStorage.Chapter> lstChapters = new List<Models.LocalStorage.Chapter>();

                // Load chapters
                await AIStoryBuildersChaptersService.LoadAIStoryBuildersChaptersAsync(objChapter.Story.Title);
                var AllChapters = AIStoryBuildersChaptersService.Chapters.ToList();

                // Adjust the sequence of chapters based on RestructureType
                if (RestructureType == RestructureType.Add)
                {
                    // Increase sequence number for chapters after the current chapter
                    foreach (var chapter in AllChapters)
                    {
                        if (chapter.sequence >= objChapter.Sequence)
                        {
                            chapter.sequence++;
                            chapter.chapter_name = "Chapter" + chapter.sequence;
                            lstChapters.Add(chapter);
                        }
                        else
                        {
                            lstChapters.Add(chapter);
                        }
                    }
                }
                else if (RestructureType == RestructureType.Delete)
                {
                    // Decrease sequence number for chapters following the current chapter
                    foreach (var chapter in AllChapters)
                    {
                        if (chapter.sequence > objChapter.Sequence)
                        {
                            chapter.sequence--;
                            chapter.chapter_name = "Chapter" + chapter.sequence;
                            lstChapters.Add(chapter);
                        }
                        else
                        {
                            lstChapters.Add(chapter);
                        }
                    }
                }

                await AIStoryBuildersChaptersService.SaveDatabaseAsync(objChapter.Story.Title, lstChapters);
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
