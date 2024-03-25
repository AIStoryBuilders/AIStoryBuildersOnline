using AIStoryBuilders.Models.JSON;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace AIStoryBuilders.Services
{
    public partial class AIStoryBuildersService
    {
        #region *** JSONNewStory ***
        public async Task<JSONStory> ParseJSONNewStory(string RawJSON)
        {
            JSONStory ParsedNewStory = new JSONStory
            {
                characters = Array.Empty<Character>(),
                locations = Array.Empty<Locations>(),
                timelines = Array.Empty<Timelines>()
            };

            try
            {
                JObject parsedJSON = JObject.Parse(RawJSON);

                // Assuming your JSONStory and related classes are well defined
                // This will automatically map the JSON to your classes
                ParsedNewStory = parsedJSON.ToObject<JSONStory>();

                return ParsedNewStory;
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("ParseJSONNewStory: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException?.StackTrace ?? "");

                return ParsedNewStory;
            }
        }
        #endregion

        #region *** JSONNewChapters ***
        public async Task<JSONChapters> ParseJSONNewChapters(string RawJSON)
        {
            try
            {
                // Parse the JSON as a dynamic object
                dynamic ParsedJSON = JsonConvert.DeserializeObject(RawJSON);

                int i = 0;
                int ii = 0;

                JSONChapters ParsedNewChapters = new JSONChapters();

                int chapterCount = 1;

                if (ParsedJSON != null)
                {
                    if (ParsedJSON.Count == null)
                    {
                        // All three chapters have been returned as one element
                        if (ParsedJSON.chapter != null)
                        {
                            // Sometimes it comes back as chapter
                            ParsedJSON = ParsedJSON.chapter;
                        }
                        else
                        {
                            // Sometimes it comes back as chapters
                            ParsedJSON = ParsedJSON.chapters;
                        }

                        chapterCount = ParsedJSON.Count;

                        ParsedNewChapters.chapter = new JSONChapter[chapterCount];

                        foreach (dynamic chapter in ParsedJSON)
                        {
                            // Add the chapter to the new story
                            ParsedNewChapters.chapter[i] = new JSONChapter();

                            if (chapter != null)
                            {
                                ParsedNewChapters.chapter[i].chapter_name = chapter.chapter_name;
                                ParsedNewChapters.chapter[i].chapter_synopsis = chapter.chapter_synopsis;

                                if (chapter.paragraphs != null)
                                {
                                    // See if there is more than one paragraph
                                    if (chapter.paragraphs.Count != null)
                                    {
                                        // Loop through the paragraphs
                                        ii = 0;
                                        ParsedNewChapters.chapter[i].paragraphs = new JSONParagraphs[chapter.paragraphs.Count];

                                        foreach (dynamic paragraph in chapter.paragraphs)
                                        {
                                            // Add the paragraph to the chapter
                                            ParsedNewChapters.chapter[i].paragraphs[ii] = new JSONParagraphs();
                                            ParsedNewChapters.chapter[i].paragraphs[ii].contents = paragraph.contents;
                                            ParsedNewChapters.chapter[i].paragraphs[ii].location_name = paragraph.location_name;
                                            ParsedNewChapters.chapter[i].paragraphs[ii].timeline_name = paragraph.timeline_name;
                                            ParsedNewChapters.chapter[i].paragraphs[ii].sequence = (ii + 1);

                                            if (paragraph.character_names != null)
                                            {
                                                ParsedNewChapters.chapter[i].paragraphs[ii].character_names = new string[paragraph.character_names.Count];

                                                // See if there is more than one character
                                                if (paragraph.character_names.Count > 1)
                                                {
                                                    // Loop through the characters
                                                    int iii = 0;
                                                    foreach (dynamic character in paragraph.character_names)
                                                    {
                                                        // Add the character to the paragraph
                                                        ParsedNewChapters.chapter[i].paragraphs[ii].character_names[iii] = character;
                                                        iii++;
                                                    }
                                                }
                                                else
                                                {
                                                    // Add the character to the paragraph
                                                    ParsedNewChapters.chapter[i].paragraphs[ii].character_names[0] = paragraph.character_names[0];
                                                }
                                            }

                                            ii++;
                                        }
                                    }
                                    else
                                    {
                                        // Add the paragraph to the chapter
                                        ParsedNewChapters.chapter[i].paragraphs = new JSONParagraphs[1];
                                        ParsedNewChapters.chapter[i].paragraphs[0] = new JSONParagraphs();
                                        ParsedNewChapters.chapter[i].paragraphs[0].contents = chapter[i].paragraphs.contents;
                                        ParsedNewChapters.chapter[i].paragraphs[0].location_name = chapter[i].paragraphs.location_name;
                                        ParsedNewChapters.chapter[i].paragraphs[0].timeline_name = chapter[i].paragraphs.timeline_name;
                                        ParsedNewChapters.chapter[i].paragraphs[0].sequence = 1;

                                        if (chapter[i].paragraphs.character_names != null)
                                        {
                                            ParsedNewChapters.chapter[i].paragraphs[0].character_names = new string[chapter[i].paragraphs.character_names.Count];

                                            // See if there is more than one character
                                            if (chapter[i].paragraphs.character_names.Count != null)
                                            {
                                                // Loop through the characters
                                                int iii = 0;
                                                foreach (dynamic character in chapter[i].paragraphs.character_names)
                                                {
                                                    // Add the character to the paragraph
                                                    ParsedNewChapters.chapter[i].paragraphs[ii].character_names[iii] = character;
                                                    iii++;
                                                }
                                            }
                                            else
                                            {
                                                // Add the character to the paragraph
                                                ParsedNewChapters.chapter[i].paragraphs[ii].character_names[0] = chapter[i].paragraphs.character_names[0];
                                            }
                                        }
                                    }
                                }
                            }
                            i++;
                        }
                    }
                    else
                    {
                        chapterCount = ParsedJSON.Count;

                        ParsedNewChapters.chapter = new JSONChapter[chapterCount];

                        foreach (dynamic chapter in ParsedJSON)
                        {
                            // Add the chapter to the new story
                            ParsedNewChapters.chapter[i] = new JSONChapter();

                            if (chapter.chapter != null)
                            {
                                ParsedNewChapters.chapter[i].chapter_name = chapter.chapter.chapter_name;
                                ParsedNewChapters.chapter[i].chapter_synopsis = chapter.chapter.chapter_synopsis;

                                if (chapter.chapter.paragraphs != null)
                                {
                                    // See if there is more than one paragraph
                                    if (chapter.chapter.paragraphs.Count != null)
                                    {
                                        // Loop through the paragraphs
                                        ii = 0;
                                        ParsedNewChapters.chapter[i].paragraphs = new JSONParagraphs[chapter.chapter.paragraphs.Count];

                                        foreach (dynamic paragraph in chapter.chapter.paragraphs)
                                        {
                                            // Add the paragraph to the chapter
                                            ParsedNewChapters.chapter[i].paragraphs[ii] = new JSONParagraphs();
                                            ParsedNewChapters.chapter[i].paragraphs[ii].contents = paragraph[ii].contents;
                                            ParsedNewChapters.chapter[i].paragraphs[ii].location_name = paragraph[ii].location_name;
                                            ParsedNewChapters.chapter[i].paragraphs[ii].timeline_name = paragraph[ii].timeline_name;
                                            ParsedNewChapters.chapter[i].paragraphs[ii].sequence = (ii + 1);

                                            if (paragraph[ii].character_names != null)
                                            {
                                                ParsedNewChapters.chapter[i].paragraphs[ii].character_names = new string[paragraph[ii].character_names.Count];

                                                // See if there is more than one character
                                                if (paragraph[ii].character_names.Count > 1)
                                                {
                                                    // Loop through the characters
                                                    int iii = 0;
                                                    foreach (dynamic character in paragraph[iii].character_names)
                                                    {
                                                        // Add the character to the paragraph
                                                        ParsedNewChapters.chapter[i].paragraphs[ii].character_names[iii] = character;
                                                        iii++;
                                                    }
                                                }
                                                else
                                                {
                                                    // Add the character to the paragraph
                                                    ParsedNewChapters.chapter[i].paragraphs[ii].character_names[0] = paragraph[ii].character_names[0];
                                                }
                                            }

                                            ii++;
                                        }
                                    }
                                    else
                                    {
                                        // Add the paragraph to the chapter
                                        ParsedNewChapters.chapter[i].paragraphs = new JSONParagraphs[1];
                                        ParsedNewChapters.chapter[i].paragraphs[0] = new JSONParagraphs();
                                        ParsedNewChapters.chapter[i].paragraphs[0].contents = chapter.chapter.paragraphs.contents;
                                        ParsedNewChapters.chapter[i].paragraphs[0].location_name = chapter.chapter.paragraphs.location_name;
                                        ParsedNewChapters.chapter[i].paragraphs[0].timeline_name = chapter.chapter.paragraphs.timeline_name;
                                        ParsedNewChapters.chapter[i].paragraphs[0].sequence = 1;

                                        if (chapter.chapter.paragraphs.character_names != null)
                                        {
                                            ParsedNewChapters.chapter[i].paragraphs[0].character_names = new string[chapter.chapter.paragraphs.character_names.Count];

                                            // See if there is more than one character
                                            if (chapter.chapter.paragraphs.character_names.Count != null)
                                            {
                                                // Loop through the characters
                                                int iii = 0;
                                                foreach (dynamic character in chapter.chapter.paragraphs.character_names)
                                                {
                                                    // Add the character to the paragraph
                                                    ParsedNewChapters.chapter[i].paragraphs[ii].character_names[iii] = character;
                                                    iii++;
                                                }
                                            }
                                            else
                                            {
                                                // Add the character to the paragraph
                                                ParsedNewChapters.chapter[i].paragraphs[ii].character_names[0] = chapter.chapter.paragraphs.character_names[0];
                                            }
                                        }
                                    }
                                }
                            }
                            i++;
                        }
                    }
                }

                return ParsedNewChapters;
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("ParseJSONNewChapters: " + ex.Message);

                JSONChapters ParsedNewChapters = new JSONChapters();
                ParsedNewChapters.chapter = new JSONChapter[0];

                return ParsedNewChapters;
            }
        }
        #endregion
    }
}
