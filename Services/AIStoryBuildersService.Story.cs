using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using AIStoryBuilders.Models.JSON;
using AIStoryBuilders.Models.LocalStorage;
using Markdig.Extensions.TaskLists;
using static AIStoryBuilders.AI.OrchestratorMethods;
using Character = AIStoryBuilders.Models.Character;

namespace AIStoryBuilders.Services
{
    public partial class AIStoryBuildersService
    {
        #region *** Story ***
        public async Task<List<Models.Story>> GetStorysAsync()
        {
            List<Models.Story> stories = new List<Models.Story>();

            try
            {
                await AIStoryBuildersStoryService.LoadAIStoryBuildersStoriesAsync();

                var AIStoryBuildersStoriesContent = AIStoryBuildersStoryService.colAIStoryBuildersStory;

                foreach (var story in AIStoryBuildersStoriesContent.OrderBy(x => x.Title))
                {
                    Models.Story objStr = new Models.Story();

                    objStr.Id = story.Id;
                    objStr.Title = story.Title;
                    objStr.Style = story.Style;
                    objStr.Theme = story.Theme;
                    objStr.Synopsis = story.Synopsis;

                    stories.Add(objStr);
                }
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("GetStorys: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return new List<Models.Story>();
            }

            return stories;
        }

        public async Task AddStory(Models.Story story, string GPTModelId)
        {
            // Create Characters, Chapters, Timelines, and Locations sub folders

            string StoryPath = $"{story.Title}";
            string CharactersPath = $"{StoryPath}/Characters";
            string ChaptersPath = $"{StoryPath}/Chapters";
            string LocationsPath = $"{StoryPath}/Locations";

            //  ********** Call the LLM to Parse the Story to create the files **********
            OpenAI.Chat.Message ParsedStoryJSON = await OrchestratorMethods.ParseNewStory(story.Title, story.Synopsis, GPTModelId);

            // Add Story to file
            await AIStoryBuildersStoryService.AddStoryAsync(new AIStoryBuildersStory
            {
                Title = story.Title,
                Style = story.Style,
                Theme = story.Theme,
                Synopsis = story.Synopsis
            });

            // Log
            await LogService.WriteToLogAsync($"Story created {story.Title}");

            JSONStory ParsedNewStory = new JSONStory();

            // Convert the JSON to a dynamic object
            ParsedNewStory = await ParseJSONNewStory(ParsedStoryJSON.Content.ToString());

            // *****************************************************

            // Create the Character files
            TextEvent?.Invoke(this, new TextEventArgs($"Create the Characters", 5));

            List<AIStoryBuilders.Models.LocalStorage.Character> CharacterContents = new List<AIStoryBuilders.Models.LocalStorage.Character>();

            foreach (var character in ParsedNewStory.characters)
            {
                AIStoryBuilders.Models.LocalStorage.Character objCharacter = new AIStoryBuilders.Models.LocalStorage.Character();
                objCharacter.descriptions = new List<AIStoryBuilders.Models.LocalStorage.Descriptions>();

                objCharacter.name = OrchestratorMethods.SanitizeFileName(character.name);

                foreach (var description in character.descriptions)
                {
                    AIStoryBuilders.Models.LocalStorage.Descriptions objDescription = new AIStoryBuilders.Models.LocalStorage.Descriptions();

                    objDescription.description_type = description.description_type ?? "";
                    objDescription.timeline_name = description.timeline_name ?? "";
                    objDescription.description = description.description ?? "";
                    objDescription.embedding = await OrchestratorMethods.GetVectorEmbedding(description.description ?? "", false);

                    objCharacter.descriptions.Add(objDescription);
                }

                CharacterContents.Add(objCharacter);
            }

            await AIStoryBuildersCharactersService.SaveDatabaseAsync(story.Title, CharacterContents);

            // Create the Location files
            TextEvent?.Invoke(this, new TextEventArgs($"Create the Locations", 5));

            List<AIStoryBuilders.Models.LocalStorage.Locations> LocationContents = new List<AIStoryBuilders.Models.LocalStorage.Locations>();

            foreach (var location in ParsedNewStory.locations)
            {
                AIStoryBuilders.Models.LocalStorage.Locations objLocation = new AIStoryBuilders.Models.LocalStorage.Locations();
                objLocation.descriptions = new List<AIStoryBuilders.Models.LocalStorage.Descriptions>();

                objLocation.name = OrchestratorMethods.SanitizeFileName(location.name);

                if (location.descriptions != null)
                {
                    foreach (var description in location.descriptions)
                    {
                        AIStoryBuilders.Models.LocalStorage.Descriptions objDescription = new AIStoryBuilders.Models.LocalStorage.Descriptions();

                        objDescription.description = description ?? "";
                        // We are deliberately not setting a LocationTimeline (therefore setting it to empty string)
                        // We did not ask the AI to set this value because it would have ben asking too much
                        objDescription.embedding = await OrchestratorMethods.GetVectorEmbedding(description ?? "", false);

                        objLocation.descriptions.Add(objDescription);
                    }
                }
                else
                {
                    AIStoryBuilders.Models.LocalStorage.Descriptions objDescription = new AIStoryBuilders.Models.LocalStorage.Descriptions();

                    objDescription.description = location.name ?? "";
                    // We are deliberately not setting a LocationTimeline (therefore setting it to empty string)
                    // We did not ask the AI to set this value because it would have ben asking too much
                    objDescription.embedding = await OrchestratorMethods.GetVectorEmbedding(location.name ?? "", false);

                    objLocation.descriptions.Add(objDescription);
                }

                LocationContents.Add(objLocation);
            }

            await AIStoryBuildersLocationsService.SaveDatabaseAsync(story.Title, LocationContents);

            // Create the Timeline file
            TextEvent?.Invoke(this, new TextEventArgs($"Create the Timelines", 5));

            List<AIStoryBuilders.Models.LocalStorage.Timelines> TimelinesContents = new List<AIStoryBuilders.Models.LocalStorage.Timelines>();

            int i = 0;
            foreach (var timeline in ParsedNewStory.timelines)
            {
                AIStoryBuilders.Models.LocalStorage.Timelines objTimeline = new AIStoryBuilders.Models.LocalStorage.Timelines();
                objTimeline.name = OrchestratorMethods.SanitizeFileName(timeline.name);

                objTimeline.description = timeline.description;
                objTimeline.StartDate = DateTime.Now.AddDays(i).ToShortDateString() + " " + DateTime.Now.AddDays(i).ToShortTimeString();
                objTimeline.StopDate = DateTime.Now.AddDays(i + 1).ToShortDateString() + " " + DateTime.Now.AddDays(i + 1).ToShortTimeString();

                TimelinesContents.Add(objTimeline);
                i = i + 2;
            }

            await AIStoryBuildersTimelinesService.SaveDatabaseAsync(story.Title, TimelinesContents);

            //// **** Create the First Paragraph and the Chapters

            // Call ChatGPT
            OpenAI.Chat.Message ParsedChaptersJSON = await OrchestratorMethods.CreateNewChapters(ParsedStoryJSON, story.ChapterCount, GPTModelId);

            JSONChapters ParsedNewChapters = new JSONChapters();

            // Convert the JSON to a dynamic object
            ParsedNewChapters = await ParseJSONNewChapters(GetOnlyJSON(ParsedChaptersJSON.Content.ToString()));

            // Test to see that something was returned
            if (ParsedNewChapters.chapter.Length == 0)
            {
                // Clean the JSON
                ParsedChaptersJSON = await OrchestratorMethods.CleanJSON(GetOnlyJSON(ParsedChaptersJSON.Content.ToString()), GPTModelId);

                // Convert the JSON to a dynamic object
                ParsedNewChapters = await ParseJSONNewChapters(GetOnlyJSON(ParsedChaptersJSON.Content.ToString()));
            }

            //// **** Create the Files

            List<AIStoryBuilders.Models.LocalStorage.Chapter> ChapterContents = new List<AIStoryBuilders.Models.LocalStorage.Chapter>();

            int ChapterNumber = 1;
            foreach (var chapter in ParsedNewChapters.chapter)
            {
                AIStoryBuilders.Models.LocalStorage.Chapter objChapter = new AIStoryBuilders.Models.LocalStorage.Chapter();
                objChapter.paragraphs = new List<AIStoryBuilders.Models.LocalStorage.Paragraphs>();

                objChapter.chapter_name = $"Chapter{ChapterNumber}";
                objChapter.sequence = ChapterNumber;

                TextEvent?.Invoke(this, new TextEventArgs($"Create Chapter {ChapterNumber}", 5));

                if (chapter.chapter_synopsis != null)
                {
                    objChapter.chapter_synopsis = chapter.chapter_synopsis;
                    objChapter.embedding = await OrchestratorMethods.GetVectorEmbedding(chapter.chapter_synopsis ?? "", false);

                    if (chapter.paragraphs[0] != null)
                    {
                        Paragraphs objParagraph = new Paragraphs();

                        objParagraph.sequence = 1;

                        objParagraph.contents = chapter.paragraphs[0].contents;
                        objParagraph.embedding = await OrchestratorMethods.GetVectorEmbedding(chapter.paragraphs[0].contents ?? "", false);

                        // Only allow one Location and Timeline
                        var TempLocation = chapter.paragraphs[0].location_name;
                        var TempTimeline = chapter.paragraphs[0].timeline_name;

                        // Split the Location and Timeline using the comma
                        var TempLocationSplit = TempLocation.Split(',');
                        var TempTimelineSplit = TempTimeline.Split(',');

                        // Get the first Location and Timeline
                        objParagraph.location_name = TempLocationSplit[0];
                        objParagraph.timeline_name = TempTimelineSplit[0];

                        string Characters = "[";

                        if (chapter.paragraphs[0].character_names != null)
                        {
                            foreach (var character in chapter.paragraphs[0].character_names)
                            {
                                Characters += $"{character},";
                            }

                            // Remove the last comma
                            Characters = Characters.Remove(Characters.Length - 1);

                        }
                        Characters = Characters + "]";

                        objParagraph.character_names = Characters;

                        objChapter.paragraphs.Add(objParagraph);
                    }
                }

                ChapterContents.Add(objChapter);
                ChapterNumber++;
            }

            await AIStoryBuildersChaptersService.SaveDatabaseAsync(story.Title, ChapterContents);

            // Manifest
            TextEvent?.Invoke(this, new TextEventArgs($"Create the Manifest", 5));

            JSONManifest objJSONManifest = new JSONManifest
            {
                Version = _appMetadata.Version,
                Title = story.Title,
                Style = story.Style,
                Theme = story.Theme,
                Synopsis = story.Synopsis,
                ExportedDate = DateTime.Now.ToString()
            };

            await AIStoryBuildersManifestService.SaveManifestAsync(story.Title, objJSONManifest);
        }

        public async Task UpdateStoryAsync(Models.Story story)
        {
            // Remove any line breaks
            story.Style = RemoveLineBreaks(story.Style);
            story.Theme = RemoveLineBreaks(story.Theme);
            story.Synopsis = RemoveLineBreaks(story.Synopsis);

            // Remove any pipes (because that is used as a delimiter)
            story.Style = story.Style.Replace("|", "");
            story.Theme = story.Theme.Replace("|", "");
            story.Synopsis = story.Synopsis.Replace("|", "");

            await AIStoryBuildersStoryService.UpdateStoryAsync(new AIStoryBuildersStory
            {
                Id = story.Id,
                Title = story.Title,
                Style = story.Style,
                Theme = story.Theme,
                Synopsis = story.Synopsis
            });
        }

        public async Task DeleteStoryAsync(string StoryTitle, int StoryId)
        {
            await AIStoryBuildersStoryService.DeleteStoryAsync(StoryId);

            // Log
            await LogService.WriteToLogAsync($"Story deleted {StoryTitle}");
        }
        #endregion

        #region *** Timelines ***
        public async Task<List<AIStoryBuilders.Models.Timeline>> GetTimelines(Models.Story story)
        {
            // Create a collection of Timelines
            List<AIStoryBuilders.Models.Timeline> Timelines = new List<AIStoryBuilders.Models.Timeline>();

            try
            {
                await AIStoryBuildersTimelinesService.LoadAIStoryBuildersTimelinesAsync(story.Title);
                var AIStoryBuildersTimelines = AIStoryBuildersTimelinesService.Timelines;

                int i = 1;
                foreach (var AIStoryBuildersTimelineLine in AIStoryBuildersTimelines)
                {
                    // Create a Timeline
                    AIStoryBuilders.Models.Timeline Timeline = new AIStoryBuilders.Models.Timeline();
                    Timeline.Id = i;
                    Timeline.TimelineName = AIStoryBuildersTimelineLine.name;
                    Timeline.TimelineDescription = AIStoryBuildersTimelineLine.description;
                    Timeline.StartDate = DateTime.Parse(AIStoryBuildersTimelineLine.StartDate);

                    // use tryparse to try to parse TimelineStopTime
                    DateTime TimelineStopDate;
                    DateTime.TryParse(AIStoryBuildersTimelineLine.StopDate, out TimelineStopDate);
                    if (TimelineStopDate != DateTime.MinValue)
                    {
                        Timeline.StopDate = DateTime.Parse(AIStoryBuildersTimelineLine.StopDate);
                    }

                    // Add Timeline to collection
                    Timelines.Add(Timeline);

                    i++;
                }

                // Return collection of Timelines
                return Timelines.OrderBy(x => x.StartDate).ToList();
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("GetTimelines: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return new List<AIStoryBuilders.Models.Timeline>();
            }
        }

        public async Task AddTimeline(Models.Timeline objTimeline)
        {
            try
            {
                // Convert the passed Timeline to Timelines
                var ObjNewTimelines = AIStoryBuildersTimelinesService.ConvertTimelineToTimelines(objTimeline);

                // Update the Timeline
                await AIStoryBuildersTimelinesService.AddTimelineAsync(objTimeline.Story.Title, ObjNewTimelines);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("AddTimeline: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public async Task UpdateTimeline(Models.Timeline objTimeline)
        {
            try
            {
                // Convert the passed Timeline to Timelines
                var ObjNewTimelines = AIStoryBuildersTimelinesService.ConvertTimelineToTimelines(objTimeline);

                // Update the Timeline
                await AIStoryBuildersTimelinesService.UpdateTimelineAsync(objTimeline.Story.Title, ObjNewTimelines);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("UpdateTimeline: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public async Task UpdateTimelineAndTimelineNameAsync(Models.Timeline objTimeline, string paramTimelineNameOriginal)
        {
            try
            {
                // ********************************************************
                // Update Timelines
                // ********************************************************

                await AIStoryBuildersTimelinesService.LoadAIStoryBuildersTimelinesAsync(objTimeline.Story.Title);

                var ExistingTimelines = AIStoryBuildersTimelinesService.Timelines;

                // Get all Timelines except the one to update
                ExistingTimelines = ExistingTimelines.Where(x => x.name != paramTimelineNameOriginal).ToList();

                // Convert the passed Timeline to Timelines
                var ObjNewTimelines = AIStoryBuildersTimelinesService.ConvertTimelineToTimelines(objTimeline);

                // Set the new name
                ObjNewTimelines.name = objTimeline.TimelineName;

                // Add the new Timeline - It will have the updated name
                ExistingTimelines.Add(ObjNewTimelines);

                // Write All Timelines
                await AIStoryBuildersTimelinesService.SaveDatabaseAsync(objTimeline.Story.Title, ExistingTimelines);

                // ********************************************************
                // Update Chapter files
                // ********************************************************

                // Loops through every Chapter and Paragraph 
                await AIStoryBuildersChaptersService.LoadAIStoryBuildersChaptersAsync(objTimeline.Story.Title);
                var Chapters = AIStoryBuildersChaptersService.Chapters;

                List<Models.LocalStorage.Chapter> NewChapters = new List<Models.LocalStorage.Chapter>();

                foreach (var Chapter in Chapters)
                {
                    Models.LocalStorage.Chapter NewChapter = new Models.LocalStorage.Chapter();
                    NewChapter.chapter_name = Chapter.chapter_name;
                    NewChapter.sequence = Chapter.sequence;
                    NewChapter.chapter_synopsis = Chapter.chapter_synopsis;
                    NewChapter.embedding = Chapter.embedding;

                    NewChapter.paragraphs = new List<Models.LocalStorage.Paragraphs>();

                    foreach (var Paragraph in Chapter.paragraphs)
                    {
                        // If the Location is the one to update, then set it to new name
                        if (Paragraph.timeline_name == paramTimelineNameOriginal)
                        {
                            Paragraph.timeline_name = objTimeline.TimelineName;
                        }
                        else
                        {
                            // Use existing Timeline name
                            Paragraph.timeline_name = Paragraph.timeline_name;
                        }

                        NewChapter.paragraphs.Add(Paragraph);
                    }

                    NewChapters.Add(NewChapter);
                }

                await AIStoryBuildersChaptersService.SaveDatabaseAsync(objTimeline.Story.Title, NewChapters);

                // ********************************************************
                // Update Location files
                // ********************************************************

                await AIStoryBuildersLocationsService.LoadAIStoryBuildersLocationsAsync(objTimeline.Story.Title);
                List<Models.LocalStorage.Locations> Locations = AIStoryBuildersLocationsService.Locations;

                List<Models.LocalStorage.Locations> LocationContents = new List<Models.LocalStorage.Locations>();

                // Loop through each Location file
                foreach (var AIStoryBuildersLocation in Locations)
                {
                    Models.LocalStorage.Locations objLocations = new Models.LocalStorage.Locations();
                    objLocations.descriptions = new List<Models.LocalStorage.Descriptions>();

                    foreach (var LocationDescription in AIStoryBuildersLocation.descriptions)
                    {
                        Models.LocalStorage.Descriptions objDescriptions = new Models.LocalStorage.Descriptions();

                        // Is the TimelineName the one to update?
                        if (LocationDescription.timeline_name == paramTimelineNameOriginal)
                        {
                            // Use new Timeline name
                            objDescriptions.timeline_name = paramTimelineNameOriginal;
                        }
                        else
                        {
                            // Use existing Timeline name
                            objDescriptions.timeline_name = LocationDescription.timeline_name;
                        }

                        objDescriptions.description = LocationDescription.description;
                        objDescriptions.description_type = LocationDescription.description_type;
                        objDescriptions.embedding = LocationDescription.embedding;

                        objLocations.descriptions.Add(objDescriptions);
                    }

                    LocationContents.Add(objLocations);
                }

                await AIStoryBuildersLocationsService.SaveDatabaseAsync(objTimeline.Story.Title, LocationContents);

                // ********************************************************
                // Update Character files
                // ********************************************************

                await AIStoryBuildersCharactersService.LoadAIStoryBuildersCharactersAsync(objTimeline.Story.Title);
                List<Models.LocalStorage.Character> Characters = AIStoryBuildersCharactersService.characters;

                List<Models.LocalStorage.Character> CharacterContents = new List<Models.LocalStorage.Character>();

                // Loop through each Character 
                foreach (var AIStoryBuildersCharacter in Characters)
                {
                    Models.LocalStorage.Character objCharacter = new Models.LocalStorage.Character();
                    objCharacter.descriptions = new List<Models.LocalStorage.Descriptions>();

                    foreach (var CharacterDescription in AIStoryBuildersCharacter.descriptions)
                    {
                        Models.LocalStorage.Descriptions objDescriptions = new Models.LocalStorage.Descriptions();

                        // Is the TimelineName the one to update?
                        if (CharacterDescription.timeline_name == paramTimelineNameOriginal)
                        {
                            // Use new Timeline name
                            objDescriptions.timeline_name = paramTimelineNameOriginal;
                        }
                        else
                        {
                            // Use existing Timeline name
                            objDescriptions.timeline_name = CharacterDescription.timeline_name;
                        }

                        objDescriptions.description = CharacterDescription.description;
                        objDescriptions.description_type = CharacterDescription.description_type;
                        objDescriptions.embedding = CharacterDescription.embedding;

                        objCharacter.descriptions.Add(objDescriptions);
                    }

                    CharacterContents.Add(objCharacter);
                }

                await AIStoryBuildersCharactersService.SaveDatabaseAsync(objTimeline.Story.Title, CharacterContents);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("UpdateTimelineAndTimelineNameAsync: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public async Task DeleteTimelineAndTimelineNameAsync(Models.Timeline objTimeline, string paramTimelineNameOriginal)
        {
            try
            {
                // ********************************************************
                // Update Timelines
                // ********************************************************

                await AIStoryBuildersTimelinesService.LoadAIStoryBuildersTimelinesAsync(objTimeline.Story.Title);

                var ExistingTimelines = AIStoryBuildersTimelinesService.Timelines;

                // Get all Timelines except the one to update
                ExistingTimelines = ExistingTimelines.Where(x => x.name != paramTimelineNameOriginal).ToList();

                // Write All Timelines - This will remove the Timeline to delete
                await AIStoryBuildersTimelinesService.SaveDatabaseAsync(objTimeline.Story.Title, ExistingTimelines);

                // ********************************************************
                // Update Chapter files
                // ********************************************************

                // Loops through every Chapter and Paragraph 
                await AIStoryBuildersChaptersService.LoadAIStoryBuildersChaptersAsync(objTimeline.Story.Title);
                var Chapters = AIStoryBuildersChaptersService.Chapters;

                List<Models.LocalStorage.Chapter> NewChapters = new List<Models.LocalStorage.Chapter>();

                foreach (var Chapter in Chapters)
                {
                    Models.LocalStorage.Chapter NewChapter = new Models.LocalStorage.Chapter();
                    NewChapter.chapter_name = Chapter.chapter_name;
                    NewChapter.sequence = Chapter.sequence;
                    NewChapter.chapter_synopsis = Chapter.chapter_synopsis;
                    NewChapter.embedding = Chapter.embedding;

                    NewChapter.paragraphs = new List<Models.LocalStorage.Paragraphs>();

                    foreach (var Paragraph in Chapter.paragraphs)
                    {
                        // If the Location is the one to update, then set it to blank
                        if (Paragraph.timeline_name == paramTimelineNameOriginal)
                        {
                            Paragraph.timeline_name = "";
                        }
                        else
                        {
                            Paragraph.timeline_name = Paragraph.timeline_name;
                        }

                        NewChapter.paragraphs.Add(Paragraph);
                    }

                    NewChapters.Add(NewChapter);
                }

                await AIStoryBuildersChaptersService.SaveDatabaseAsync(objTimeline.Story.Title, NewChapters);

                // ********************************************************
                // Update Location files
                // ********************************************************

                await AIStoryBuildersLocationsService.LoadAIStoryBuildersLocationsAsync(objTimeline.Story.Title);
                List<Models.LocalStorage.Locations> Locations = AIStoryBuildersLocationsService.Locations;

                List<Models.LocalStorage.Locations> LocationContents = new List<Models.LocalStorage.Locations>();

                // Loop through each Location file
                foreach (var AIStoryBuildersLocation in Locations)
                {
                    Models.LocalStorage.Locations objLocations = new Models.LocalStorage.Locations();
                    objLocations.descriptions = new List<Models.LocalStorage.Descriptions>();

                    foreach (var LocationDescription in AIStoryBuildersLocation.descriptions)
                    {
                        Models.LocalStorage.Descriptions objDescriptions = new Models.LocalStorage.Descriptions();

                        // Is the TimelineName the one to update?
                        if (LocationDescription.timeline_name == paramTimelineNameOriginal)
                        {
                            // Set it to blank
                            objDescriptions.timeline_name = "";
                        }
                        else
                        {
                            // Use existing Timeline name
                            objDescriptions.timeline_name = LocationDescription.timeline_name;
                        }

                        objDescriptions.description = LocationDescription.description;
                        objDescriptions.description_type = LocationDescription.description_type;
                        objDescriptions.embedding = LocationDescription.embedding;

                        objLocations.descriptions.Add(objDescriptions);
                    }

                    LocationContents.Add(objLocations);
                }

                await AIStoryBuildersLocationsService.SaveDatabaseAsync(objTimeline.Story.Title, LocationContents);

                // ********************************************************
                // Update Character files
                // ********************************************************

                await AIStoryBuildersCharactersService.LoadAIStoryBuildersCharactersAsync(objTimeline.Story.Title);
                List<Models.LocalStorage.Character> Characters = AIStoryBuildersCharactersService.characters;

                List<Models.LocalStorage.Character> CharacterContents = new List<Models.LocalStorage.Character>();

                // Loop through each Character 
                foreach (var AIStoryBuildersCharacter in Characters)
                {
                    Models.LocalStorage.Character objCharacter = new Models.LocalStorage.Character();
                    objCharacter.descriptions = new List<Models.LocalStorage.Descriptions>();

                    foreach (var CharacterDescription in AIStoryBuildersCharacter.descriptions)
                    {
                        Models.LocalStorage.Descriptions objDescriptions = new Models.LocalStorage.Descriptions();

                        // Is the TimelineName the one to update?
                        if (CharacterDescription.timeline_name == paramTimelineNameOriginal)
                        {
                            // Set it to blank
                            objDescriptions.timeline_name = "";
                        }
                        else
                        {
                            // Use existing Timeline name
                            objDescriptions.timeline_name = CharacterDescription.timeline_name;
                        }

                        objDescriptions.description = CharacterDescription.description;
                        objDescriptions.description_type = CharacterDescription.description_type;
                        objDescriptions.embedding = CharacterDescription.embedding;

                        objCharacter.descriptions.Add(objDescriptions);
                    }

                    CharacterContents.Add(objCharacter);
                }

                await AIStoryBuildersCharactersService.SaveDatabaseAsync(objTimeline.Story.Title, CharacterContents);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("DeleteTimelineAndTimelineNameAsync: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }
        #endregion

        #region *** Locations ***
        public async Task<List<AIStoryBuilders.Models.Location>> GetLocations(Models.Story story)
        {
            // Create a collection of Location
            List<AIStoryBuilders.Models.Location> Locations = new List<AIStoryBuilders.Models.Location>();

            try
            {
                await AIStoryBuildersLocationsService.LoadAIStoryBuildersLocationsAsync(story.Title);
                var AIStoryBuildersLocations = AIStoryBuildersLocationsService.Locations;

                int i = 1;
                foreach (var AIStoryBuildersLocation in AIStoryBuildersLocations)
                {
                    // Create a Location
                    AIStoryBuilders.Models.Location Location = new AIStoryBuilders.Models.Location();
                    Location.Id = i;
                    Location.LocationName = AIStoryBuildersLocation.name;

                    Location.LocationDescription = new List<LocationDescription>();

                    if (AIStoryBuildersLocation.descriptions != null)
                    {
                        int ii = 1;
                        foreach (var description in AIStoryBuildersLocation.descriptions)
                        {
                            LocationDescription objLocationDescription = new LocationDescription();
                            objLocationDescription.Id = ii;
                            objLocationDescription.Description = description.description;

                            // Does the TimelineName element exist?
                            if (description.timeline_name != null)
                            {
                                Models.Timeline objTimeline = new Models.Timeline();
                                objTimeline.TimelineName = description.timeline_name;

                                objLocationDescription.Timeline = objTimeline;
                            }

                            Location.LocationDescription.Add(objLocationDescription);
                            ii++;
                        }
                    }

                    // Add Location to collection
                    Locations.Add(Location);
                    i++;
                }

                // Return collection of Locations
                return Locations;
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("GetLocations: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return new List<AIStoryBuilders.Models.Location>();
            }
        }

        public async Task<bool> LocationExists(Models.Location objLocation)
        {
            bool LocationExists = true;
            var AIStoryBuildersLocationsPath = $"{BasePath}/{objLocation.Story.Title}/Locations";

            try
            {
                // Get a list of all the Location files
                string[] AIStoryBuildersLocationsFiles = Directory.GetFiles(AIStoryBuildersLocationsPath, "*.csv", SearchOption.AllDirectories);

                List<string> ExistingLocations = new List<string>();
                // Loop through each Location file
                foreach (var AIStoryBuildersLocationFile in AIStoryBuildersLocationsFiles)
                {
                    // Get the LocationName from the file name
                    string LocationName = Path.GetFileNameWithoutExtension(AIStoryBuildersLocationFile);

                    ExistingLocations.Add(LocationName.ToLower());
                }

                if (ExistingLocations.Contains(objLocation.LocationName.ToLower()))
                {
                    LocationExists = true;
                }
                else
                {
                    LocationExists = false;
                }

                return LocationExists;
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("LocationExists: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return true;
            }
        }

        public async Task AddLocationAsync(Models.Location objLocation)
        {
            try
            {
                string StoryPath = $"{BasePath}/{objLocation.Story.Title}";
                string LocationsPath = $"{StoryPath}/Locations";

                // Add Location to file
                List<string> LocationContents = new List<string>();
                string LocationName = OrchestratorMethods.SanitizeFileName(objLocation.LocationName);

                foreach (var description in objLocation.LocationDescription)
                {
                    string VectorEmbedding = await OrchestratorMethods.GetVectorEmbedding(description.Description, false);

                    // Set TimelineName to empty string if null
                    string TimelineName = "";
                    if (description.Timeline == null)
                    {
                        TimelineName = "";
                    }
                    else
                    {
                        TimelineName = description.Timeline.TimelineName ?? "";
                    }

                    var LocationDescriptionAndTimeline = $"{description.Description}|{TimelineName}";
                    LocationContents.Add($"{LocationDescriptionAndTimeline}|{VectorEmbedding}" + Environment.NewLine);
                }

                string LocationPath = $"{LocationsPath}/{LocationName}.csv";
                File.WriteAllLines(LocationPath, LocationContents);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("AddLocationAsync: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public async Task UpdateLocationDescriptions(Models.Location objLocation)
        {
            try
            {
                string StoryPath = $"{BasePath}/{objLocation.Story.Title}";
                string LocationsPath = $"{StoryPath}/Locations";

                // Add Location to file
                List<string> LocationContents = new List<string>();
                string LocationName = OrchestratorMethods.SanitizeFileName(objLocation.LocationName);

                foreach (var description in objLocation.LocationDescription)
                {
                    string VectorEmbedding = await OrchestratorMethods.GetVectorEmbedding(description.Description, false);

                    // Set TimelineName to empty string if null
                    string TimelineName = "";
                    if (description.Timeline == null)
                    {
                        TimelineName = "";
                    }
                    else
                    {
                        TimelineName = description.Timeline.TimelineName ?? "";
                    }

                    var LocationDescriptionAndTimeline = $"{description.Description}|{TimelineName}";
                    LocationContents.Add($"{LocationDescriptionAndTimeline}|{VectorEmbedding}" + Environment.NewLine);
                }

                string LocationPath = $"{LocationsPath}/{LocationName}.csv";
                File.WriteAllLines(LocationPath, LocationContents);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("UpdateLocationDescriptions: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public async Task DeleteLocation(Models.Location objLocation)
        {
            try
            {
                string StoryPath = $"{BasePath}/{objLocation.Story.Title}";
                string LocationsPath = $"{StoryPath}/Locations";
                string LocationPath = $"{LocationsPath}/{objLocation.LocationName}.csv";

                if (objLocation.LocationName.Trim() != "")
                {
                    // Loops through every Chapter and Paragraph and remove the Location
                    var Chapters = await GetChapters(objLocation.Story);

                    foreach (var Chapter in Chapters)
                    {
                        var Paragraphs = await GetParagraphs(Chapter);

                        foreach (var Paragraph in Paragraphs)
                        {
                            // Create the path to the Paragraph file
                            var ChapterNameParts = Chapter.ChapterName.Split(' ');
                            string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];
                            string ParagraphPath = $"{StoryPath}/Chapters/{ChapterName}/Paragraph{Paragraph.Sequence}.txt";

                            // Get the ParagraphContent from the file
                            string[] ParagraphContent = File.ReadAllLines(ParagraphPath);

                            // Remove all empty lines
                            ParagraphContent = ParagraphContent.Where(line => line.Trim() != "").ToArray();

                            // Get the Location from the file
                            string[] ParagraphLocation = ParagraphContent[0].Split('|');

                            // If the Location is the one to delete, then set it to empty string
                            if (ParagraphLocation[0] == objLocation.LocationName)
                            {
                                ParagraphLocation[0] = "";

                                // Put the ParagraphContent back together
                                ParagraphContent[0] = string.Join("|", ParagraphLocation);

                                // Write the ParagraphContent back to the file
                                File.WriteAllLines(ParagraphPath, ParagraphContent);
                            }
                        }
                    }
                }

                // Delete Location file
                File.Delete(LocationPath);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("DeleteLocation: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public async Task UpdateLocationName(Models.Location objLocation, string paramOriginalLocationName)
        {
            try
            {
                string StoryPath = $"{BasePath}/{objLocation.Story.Title}";
                string LocationsPath = $"{StoryPath}/Locations";
                string LocationPath = $"{LocationsPath}/{paramOriginalLocationName}.csv";

                if (objLocation.LocationName.Trim() != "")
                {
                    // Loops through every Chapter and Paragraph and remove the Location
                    var Chapters = await GetChapters(objLocation.Story);

                    foreach (var Chapter in Chapters)
                    {
                        var Paragraphs = await GetParagraphs(Chapter);

                        foreach (var Paragraph in Paragraphs)
                        {
                            // Create the path to the Paragraph file
                            var ChapterNameParts = Chapter.ChapterName.Split(' ');
                            string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];
                            string ParagraphPath = $"{StoryPath}/Chapters/{ChapterName}/Paragraph{Paragraph.Sequence}.txt";

                            // Get the ParagraphContent from the file
                            string[] ParagraphContent = File.ReadAllLines(ParagraphPath);

                            // Remove all empty lines
                            ParagraphContent = ParagraphContent.Where(line => line.Trim() != "").ToArray();

                            // Get the Location from the file
                            string[] ParagraphLocation = ParagraphContent[0].Split('|');

                            // If the Location is the one to update, then set it to new name
                            if (ParagraphLocation[0] == paramOriginalLocationName)
                            {
                                // Set to the new name
                                ParagraphLocation[0] = objLocation.LocationName;

                                // Put the ParagraphContent back together
                                ParagraphContent[0] = string.Join("|", ParagraphLocation);

                                // Write the ParagraphContent back to the file
                                File.WriteAllLines(ParagraphPath, ParagraphContent);
                            }
                        }
                    }

                    // Rename Location file
                    string NewLocationPath = $"{LocationsPath}/{objLocation.LocationName}.csv";
                    File.Move(LocationPath, NewLocationPath);
                }
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("UpdateLocationName: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        #endregion

        #region *** Character ***
        public async Task<List<AIStoryBuilders.Models.Character>> GetCharacters(Models.Story story)
        {
            // Create a collection of Character
            List<AIStoryBuilders.Models.Character> Characters = new List<AIStoryBuilders.Models.Character>();

            try
            {
                await AIStoryBuildersCharactersService.LoadAIStoryBuildersCharactersAsync(story.Title);

                var AIStoryBuildersCharacters = AIStoryBuildersCharactersService.characters;

                // Loop through each Character file
                int i = 1;
                foreach (var AIStoryBuildersCharacter in AIStoryBuildersCharacters)
                {
                    int ii = 1;
                    List<CharacterBackground> colCharacterBackground = new List<CharacterBackground>();

                    foreach (var CharacterBackground in AIStoryBuildersCharacter.descriptions)
                    {
                        CharacterBackground objCharacterBackground = new CharacterBackground();

                        objCharacterBackground.Id = ii;
                        objCharacterBackground.Sequence = ii;
                        objCharacterBackground.Type = CharacterBackground.description_type;
                        objCharacterBackground.Timeline = new Models.Timeline() { TimelineName = CharacterBackground.timeline_name };
                        objCharacterBackground.Description = CharacterBackground.description;
                        objCharacterBackground.VectorContent = CharacterBackground.embedding;
                        objCharacterBackground.Character = new Character() { CharacterName = AIStoryBuildersCharacter.name };

                        colCharacterBackground.Add(objCharacterBackground);
                        ii++;
                    }

                    // Create a Character
                    AIStoryBuilders.Models.Character Character = new AIStoryBuilders.Models.Character();
                    Character.Id = i;
                    Character.CharacterName = AIStoryBuildersCharacter.name;
                    Character.CharacterBackground = colCharacterBackground;

                    // Add Character to collection
                    Characters.Add(Character);
                    i++;
                }

                // Return collection of Characters
                return Characters;
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("GetCharacters: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return new List<AIStoryBuilders.Models.Character>();
            }
        }

        public async Task AddUpdateCharacterAsync(Character character, string paramOrginalCharacterName)
        {
            string StoryPath = $"{BasePath}/{character.Story.Title}";
            string CharactersPath = $"{StoryPath}/Characters";
            string ChaptersPath = $"{StoryPath}/Chapters";

            // Add Character to file
            string CharacterName = OrchestratorMethods.SanitizeFileName(paramOrginalCharacterName);

            // Create Character file
            string CharacterPath = $"{CharactersPath}/{CharacterName}.csv";
            List<string> CharacterContents = new List<string>();

            foreach (var description in character.CharacterBackground)
            {
                string description_type = description.Type ?? "";

                string TimeLineName = "";

                if (description.Timeline != null)
                {
                    TimeLineName = description.Timeline.TimelineName ?? "";
                }

                string timeline_name = TimeLineName;
                string VectorDescriptionAndEmbedding = await OrchestratorMethods.GetVectorEmbedding(description.Description ?? "", true);
                CharacterContents.Add($"{description_type}|{timeline_name}|{VectorDescriptionAndEmbedding}" + Environment.NewLine);
            }

            File.WriteAllLines(CharacterPath, CharacterContents);
        }

        public async Task DeleteCharacter(Character character, string paramOrginalCharcterName)
        {
            string StoryPath = $"{BasePath}/{character.Story.Title}";
            string CharactersPath = $"{StoryPath}/Characters";
            string ChaptersPath = $"{StoryPath}/Chapters";
            string CharacterPath = $"{CharactersPath}/{paramOrginalCharcterName}.csv";

            if (character.CharacterName.Trim() != "")
            {
                // Loops through every Chapter and Paragraph and update the Character
                var Chapters = await GetChapters(character.Story);

                foreach (var Chapter in Chapters)
                {
                    var Paragraphs = await GetParagraphs(Chapter);

                    foreach (var Paragraph in Paragraphs)
                    {
                        // Create the path to the Paragraph file
                        var ChapterNameParts = Chapter.ChapterName.Split(' ');
                        string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];
                        string ParagraphPath = $"{StoryPath}/Chapters/{ChapterName}/Paragraph{Paragraph.Sequence}.txt";

                        // Get the ParagraphContent from the file
                        string[] ParagraphContent = File.ReadAllLines(ParagraphPath);

                        // Remove all empty lines
                        ParagraphContent = ParagraphContent.Where(line => line.Trim() != "").ToArray();

                        // Get the file as an array
                        string[] ParagraphArray = ParagraphContent[0].Split('|');

                        // Remove the [ and ] from the array
                        ParagraphArray[2] = ParagraphArray[2].Replace("[", "");
                        ParagraphArray[2] = ParagraphArray[2].Replace("]", "");

                        // Get the Character array from the file
                        string[] ParagraphCharacters = ParagraphArray[2].Split(',');

                        // Loop through each Character to see if the Character is the one to delete
                        for (int i = 0; i < ParagraphCharacters.Length; i++)
                        {
                            // If the Character is the one to update, then set it to new name
                            if (ParagraphCharacters[i] == paramOrginalCharcterName)
                            {
                                // Remove the Character
                                ParagraphCharacters[i] = "";
                            }
                        }

                        // Create an array of Characters that are not empty
                        ParagraphCharacters = ParagraphCharacters.Where(x => !string.IsNullOrEmpty(x)).ToArray();

                        // Put the ParagraphCharacters back together
                        string ParagraphCharacterString = string.Join(",", ParagraphCharacters);

                        // Put the [ and ] back on the array
                        ParagraphCharacterString = "[" + ParagraphCharacterString + "]";

                        // Set the Character array back to the ParagraphArray
                        ParagraphArray[2] = ParagraphCharacterString;

                        // Put the ParagraphContent back together
                        ParagraphContent[0] = string.Join("|", ParagraphArray);

                        // Write the ParagraphContent back to the file
                        File.WriteAllLines(ParagraphPath, ParagraphContent);
                    }
                }

                // Delete the Character file
                File.Delete(CharacterPath);
            }
        }

        public async Task UpdateCharacterName(Character character, string paramOrginalCharcterName)
        {
            string StoryPath = $"{BasePath}/{character.Story.Title}";
            string CharactersPath = $"{StoryPath}/Characters";
            string ChaptersPath = $"{StoryPath}/Chapters";
            string CharacterPath = $"{CharactersPath}/{paramOrginalCharcterName}.csv";

            if (character.CharacterName.Trim() != "")
            {
                // Loops through every Chapter and Paragraph and update the Character
                var Chapters = await GetChapters(character.Story);

                foreach (var Chapter in Chapters)
                {
                    var Paragraphs = await GetParagraphs(Chapter);

                    foreach (var Paragraph in Paragraphs)
                    {
                        // Create the path to the Paragraph file
                        var ChapterNameParts = Chapter.ChapterName.Split(' ');
                        string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];
                        string ParagraphPath = $"{StoryPath}/Chapters/{ChapterName}/Paragraph{Paragraph.Sequence}.txt";

                        // Get the ParagraphContent from the file
                        string[] ParagraphContent = File.ReadAllLines(ParagraphPath);

                        // Remove all empty lines
                        ParagraphContent = ParagraphContent.Where(line => line.Trim() != "").ToArray();

                        // Get the file as an array
                        string[] ParagraphArray = ParagraphContent[0].Split('|');

                        // Remove the [ and ] from the array
                        ParagraphArray[2] = ParagraphArray[2].Replace("[", "");
                        ParagraphArray[2] = ParagraphArray[2].Replace("]", "");

                        // Get the Character array from the file
                        string[] ParagraphCharacters = ParagraphArray[2].Split(',');

                        // Loop through each Character to see if the Character is the one to update
                        for (int i = 0; i < ParagraphCharacters.Length; i++)
                        {
                            // If the Character is the one to update, then set it to new name
                            if (ParagraphCharacters[i] == paramOrginalCharcterName)
                            {
                                // Set to the new name
                                ParagraphCharacters[i] = character.CharacterName;
                            }
                        }

                        // Put the ParagraphCharacters back together
                        string ParagraphCharacterString = string.Join(",", ParagraphCharacters);

                        // Put the [ and ] back on the array
                        ParagraphCharacterString = "[" + ParagraphCharacterString + "]";

                        // Set the Character array back to the ParagraphArray
                        ParagraphArray[2] = ParagraphCharacterString;

                        // Put the ParagraphContent back together
                        ParagraphContent[0] = string.Join("|", ParagraphArray);

                        // Write the ParagraphContent back to the file
                        File.WriteAllLines(ParagraphPath, ParagraphContent);
                    }
                }

                // Rename Character file
                string NewCharacterPath = $"{CharactersPath}/{character.CharacterName.Trim()}.csv";
                File.Move(CharacterPath, NewCharacterPath);
            }
        }
        #endregion

        #region *** Chapter ***
        public async Task<List<AIStoryBuilders.Models.Chapter>> GetChapters(Models.Story story)
        {
            // Create a collection of Chapter
            List<AIStoryBuilders.Models.Chapter> Chapters = new List<AIStoryBuilders.Models.Chapter>();

            try
            {
                await AIStoryBuildersChaptersService.LoadAIStoryBuildersChaptersAsync(story.Title);

                if (AIStoryBuildersChaptersService.Chapters.Count == 0)
                {
                    return new List<AIStoryBuilders.Models.Chapter>();
                }

                // order by the folder name
                var AIStoryBuildersChapters = AIStoryBuildersChaptersService.Chapters.OrderBy(x => x.chapter_name).ToList();

                // Loop through each Chapter folder
                int ChapterSequenceNumber = 1;
                foreach (var AIStoryBuildersChapter in AIStoryBuildersChapters)
                {
                    // Get the ChapterName from the file name                    
                    string ChapterName = AIStoryBuildersChapter.chapter_name;

                    // Put in a space after the word Chapter
                    ChapterName = ChapterName.Insert(7, " ");

                    // Create a Chapter
                    AIStoryBuilders.Models.Chapter Chapter = new AIStoryBuilders.Models.Chapter();
                    Chapter.ChapterName = ChapterName;
                    Chapter.Sequence = ChapterSequenceNumber;
                    Chapter.Synopsis = AIStoryBuildersChapter.chapter_synopsis;
                    Chapter.Story = story;

                    // Add Chapter to collection
                    Chapters.Add(Chapter);

                    ChapterSequenceNumber++;
                }

                // Return collection of Chapters
                return Chapters.OrderBy(x => x.Sequence).ToList();
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("GetChapters: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return new List<AIStoryBuilders.Models.Chapter>();
            }
        }

        public async Task<int> CountChapters(Models.Story story)
        {
            int ChapterCount = 0;

            try
            {
                var AIStoryBuildersChaptersPath = $"{BasePath}/{story.Title}/Chapters";
                string[] AIStoryBuildersChaptersFolders = Directory.GetDirectories(AIStoryBuildersChaptersPath);

                ChapterCount = AIStoryBuildersChaptersFolders.Count();

                return ChapterCount;
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("CountChapters: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return 0;
            }
        }

        public async Task AddChapterAsync(Models.Chapter objChapter, string ChapterName)
        {
            if (objChapter.Synopsis == null)
            {
                objChapter.Synopsis = " ";
            }

            var AIStoryBuildersChaptersPath = $"{BasePath}/{objChapter.Story.Title}/Chapters";

            // Create the Chapter folder
            string ChapterPath = $"{AIStoryBuildersChaptersPath}/{ChapterName}";
            Directory.CreateDirectory(ChapterPath);

            // Create the Chapter file
            string ChapterFilePath = $"{ChapterPath}/{ChapterName}.txt";
            string ChapterSynopsisAndEmbedding = await OrchestratorMethods.GetVectorEmbedding(objChapter.Synopsis, true);
            File.WriteAllText(ChapterFilePath, $"{ChapterSynopsisAndEmbedding}");
        }

        public async Task InsertChapterAsync(Models.Chapter objChapter)
        {
            if (objChapter.Synopsis == null)
            {
                objChapter.Synopsis = " ";
            }

            string ChapterName = objChapter.ChapterName.Replace(" ", "");
            var AIStoryBuildersChaptersPath = $"{BasePath}/{objChapter.Story.Title}/Chapters";

            // Create the Chapter folder
            string ChapterPath = $"{AIStoryBuildersChaptersPath}/{ChapterName}";
            Directory.CreateDirectory(ChapterPath);

            // Create the Chapter file
            string ChapterFilePath = $"{ChapterPath}/{ChapterName}.txt";
            string ChapterSynopsisAndEmbedding = await OrchestratorMethods.GetVectorEmbedding(objChapter.Synopsis, true);
            File.WriteAllText(ChapterFilePath, $"{ChapterSynopsisAndEmbedding}");
        }

        public async Task UpdateChapterAsync(Models.Chapter objChapter)
        {
            string ChapterName = objChapter.ChapterName.Replace(" ", "");
            var AIStoryBuildersChaptersPath = $"{BasePath}/{objChapter.Story.Title}/Chapters";
            string ChapterPath = $"{AIStoryBuildersChaptersPath}/{ChapterName}";
            string ChapterFilePath = $"{ChapterPath}/{ChapterName}.txt";

            string ChapterSynopsisAndEmbedding = await OrchestratorMethods.GetVectorEmbedding(objChapter.Synopsis, true);
            File.WriteAllText(ChapterFilePath, $"{ChapterSynopsisAndEmbedding}");
        }

        public void DeleteChapter(Models.Chapter objChapter)
        {
            // Delete Chapter
            string ChapterName = objChapter.ChapterName.Replace(" ", "");
            var AIStoryBuildersChaptersPath = $"{BasePath}/{objChapter.Story.Title}/Chapters";
            string ChapterPath = $"{AIStoryBuildersChaptersPath}/{ChapterName}";

            // Delete folder
            Directory.Delete(ChapterPath, true);
        }
        #endregion

        #region *** Paragraph ***
        public async Task<List<AIStoryBuilders.Models.Paragraph>> GetParagraphs(Models.Chapter chapter)
        {
            List<Paragraph> colParagraphs = new List<Paragraph>();

            try
            {
                string ChapterName = "Chapter" + chapter.Sequence.ToString();

                await AIStoryBuildersChaptersService.LoadAIStoryBuildersChaptersAsync(chapter.Story.Title);
                var AIStoryBuildersChapters = AIStoryBuildersChaptersService.Chapters;

                // Get the paragraphs for the specified Chapter
                var AIStoryBuildersChapter = AIStoryBuildersChapters.Where(x => x.chapter_name == ChapterName).FirstOrDefault();
                var AIStoryBuildersPargraphs = AIStoryBuildersChapter.paragraphs;

                // Loop through each Paragraph file
                foreach (var AIStoryBuildersParagraph in AIStoryBuildersPargraphs)
                {
                    // Convert ParagraphCharactersRaw to a List
                    List<string> ParagraphCharacters = ParseStringToList(AIStoryBuildersParagraph.character_names);

                    // Convert to List<Models.Character>
                    List<Models.Character> Characters = new List<Models.Character>();
                    foreach (var ParagraphCharacter in ParagraphCharacters)
                    {
                        Characters.Add(new Models.Character() { CharacterName = ParagraphCharacter });
                    }

                    // Create a Paragraph
                    AIStoryBuilders.Models.Paragraph Paragraph = new AIStoryBuilders.Models.Paragraph();
                    Paragraph.Sequence = AIStoryBuildersParagraph.sequence;
                    Paragraph.Location = new Models.Location() { LocationName = AIStoryBuildersParagraph.location_name };
                    Paragraph.Timeline = new Models.Timeline() { TimelineName = AIStoryBuildersParagraph.timeline_name };
                    Paragraph.Characters = Characters;
                    Paragraph.ParagraphContent = AIStoryBuildersParagraph.contents;

                    // Add Paragraph to collection
                    colParagraphs.Add(Paragraph);
                }

                return colParagraphs.OrderBy(x => x.Sequence).ToList();
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("GetParagraphs: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return new List<AIStoryBuilders.Models.Paragraph>();
            }
        }

        public async Task<List<AIParagraph>> GetParagraphVectors(Models.Chapter chapter, string TimelineName)
        {
            List<AIParagraph> colParagraphs = new List<AIParagraph>();

            try
            {
                var ChapterNameParts = chapter.ChapterName.Split(' ');
                string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];

                var AIStoryBuildersParagraphsPath = $"{BasePath}/{chapter.Story.Title}/Chapters/{ChapterName}";

                // Get a list of all the Paragraph files
                string[] AIStoryBuildersParagraphsFiles = Directory.GetFiles(AIStoryBuildersParagraphsPath, "Paragraph*.txt", SearchOption.AllDirectories);

                // Loop through each Paragraph file
                foreach (var AIStoryBuildersParagraphFile in AIStoryBuildersParagraphsFiles)
                {
                    // Get the ParagraphName from the file name
                    string ParagraphName = Path.GetFileNameWithoutExtension(AIStoryBuildersParagraphFile);

                    // Put in a space after the word ParagraphName
                    ParagraphName = ParagraphName.Insert(9, " ");

                    // Get sequence number from Paragraph Name
                    string ParagraphSequence = ParagraphName.Split(' ')[1];
                    int ParagraphSequenceNumber = int.Parse(ParagraphSequence);

                    // Get the ChapterContent from the file
                    string[] ChapterContent = File.ReadAllLines(AIStoryBuildersParagraphFile);

                    // Remove all empty lines
                    ChapterContent = ChapterContent.Where(line => line.Trim() != "").ToArray();

                    var ParagraphLocation = ChapterContent.Select(x => x.Split('|')).Select(x => x[0]).FirstOrDefault();
                    var ParagraphTimeline = ChapterContent.Select(x => x.Split('|')).Select(x => x[1]).FirstOrDefault();
                    var ParagraphCharactersRaw = ChapterContent.Select(x => x.Split('|')).Select(x => x[2]).FirstOrDefault();
                    var ParagraphContent = ChapterContent.Select(x => x.Split('|')).Select(x => x[3]).FirstOrDefault();
                    var ParagraphVectors = ChapterContent.Select(x => x.Split('|')).Select(x => x[4]).FirstOrDefault();

                    // Only get Paragraphs for the specified Timeline
                    if (TimelineName == ParagraphTimeline)
                    {
                        // Convert ParagraphCharactersRaw to a List
                        List<string> ParagraphCharacters = ParseStringToList(ParagraphCharactersRaw);

                        // Convert to List<Models.Character>
                        string[] CharactersArray = new string[ParagraphCharacters.Count()];
                        int i = 0;
                        foreach (var ParagraphCharacter in ParagraphCharacters)
                        {
                            CharactersArray[i] = ParagraphCharacter;
                            i++;
                        }

                        // Create a Paragraph
                        AIParagraph Paragraph = new AIParagraph();
                        Paragraph.sequence = ParagraphSequenceNumber;
                        Paragraph.location_name = ParagraphLocation;
                        Paragraph.timeline_name = ParagraphTimeline;
                        Paragraph.character_names = CharactersArray;
                        Paragraph.contents = ParagraphContent;
                        Paragraph.vectors = ParagraphVectors;

                        // Add Paragraph to collection
                        colParagraphs.Add(Paragraph);
                    }
                }

                return colParagraphs.OrderBy(x => x.sequence).ToList();
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("GetParagraphVectors: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return new List<AIParagraph>();
            }
        }

        public async Task<int> CountParagraphs(Models.Chapter chapter)
        {
            int ParagraphCount = 0;

            try
            {
                var ChapterNameParts = chapter.ChapterName.Split(' ');
                string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];

                var AIStoryBuildersParagraphsPath = $"{BasePath}/{chapter.Story.Title}/Chapters/{ChapterName}";

                // Get a list of all the Paragraph files
                string[] AIStoryBuildersParagraphsFiles = Directory.GetFiles(AIStoryBuildersParagraphsPath, "Paragraph*.txt", SearchOption.AllDirectories);

                ParagraphCount = AIStoryBuildersParagraphsFiles.Count();

                return ParagraphCount;
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("CountParagraphs: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");

                // File is empty
                return 0;
            }
        }

        public async Task AddParagraph(Models.Chapter chapter, Paragraph Paragraph)
        {
            try
            {
                // First restructure the existing Paragraphs
                await RestructureParagraphs(chapter, Paragraph.Sequence, RestructureType.Add);

                // Create a file for the new Paragraph
                var ChapterNameParts = chapter.ChapterName.Split(' ');
                string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];

                var AIStoryBuildersParagraphsPath = $"{BasePath}/{chapter.Story.Title}/Chapters/{ChapterName}";

                // Create the Paragraph file
                string ParagraphPath = $"{AIStoryBuildersParagraphsPath}/Paragraph{Paragraph.Sequence}.txt";

                // Create the ParagraphContent
                string VectorDescriptionAndEmbedding = "|";
                string ParagraphContent = $"{Paragraph.Location.LocationName ?? ""}|{Paragraph.Timeline.TimelineName ?? ""}|[{string.Join(",", Paragraph.Characters.Select(x => x.CharacterName))}]|{VectorDescriptionAndEmbedding}";

                // Write the ParagraphContent to the file
                File.WriteAllText(ParagraphPath, ParagraphContent);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("UpdateParagraph: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public async Task UpdateParagraph(Models.Chapter chapter, Paragraph Paragraph)
        {
            try
            {
                var ChapterNameParts = chapter.ChapterName.Split(' ');
                string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];

                var AIStoryBuildersParagraphsPath = $"{BasePath}/{chapter.Story.Title}/Chapters/{ChapterName}";

                // Create the Paragraph file
                string ParagraphPath = $"{AIStoryBuildersParagraphsPath}/Paragraph{Paragraph.Sequence}.txt";

                // Create the ParagraphContent
                string VectorDescriptionAndEmbedding = await OrchestratorMethods.GetVectorEmbedding(Paragraph.ParagraphContent, true);
                string ParagraphContent = $"{Paragraph.Location.LocationName ?? ""}|{Paragraph.Timeline.TimelineName ?? ""}|[{string.Join(",", Paragraph.Characters.Select(x => x.CharacterName))}]|{VectorDescriptionAndEmbedding}";

                // Preserve any line breaks
                ParagraphContent = ParagraphContent.Replace("\n", "\r\n");

                // Write the ParagraphContent to the file
                File.WriteAllText(ParagraphPath, ParagraphContent);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("UpdateParagraph: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public async Task DeleteParagraph(Models.Chapter chapter, Paragraph Paragraph)
        {
            try
            {
                var ChapterNameParts = chapter.ChapterName.Split(' ');
                string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];

                var AIStoryBuildersParagraphsPath = $"{BasePath}/{chapter.Story.Title}/Chapters/{ChapterName}";

                // Delete the Paragraph file
                string ParagraphPath = $"{AIStoryBuildersParagraphsPath}/Paragraph{Paragraph.Sequence}.txt";
                File.Delete(ParagraphPath);

                await RestructureParagraphs(chapter, Paragraph.Sequence, RestructureType.Delete);
            }
            catch (Exception ex)
            {
                // Log error
                await LogService.WriteToLogAsync("DeleteParagraph: " + ex.Message + " " + ex.StackTrace ?? "" + " " + ex.InnerException.StackTrace ?? "");
            }
        }

        public List<Paragraph> AddParagraphIndenting(List<Paragraph> paramParagraphs)
        {
            List<Paragraph> colParagraphs = new List<Paragraph>();

            foreach (var Paragraph in paramParagraphs)
            {
                Paragraph.ParagraphContent = "&nbsp;&nbsp;&nbsp;&nbsp;" + Paragraph.ParagraphContent;
                Paragraph.ParagraphContent = Paragraph.ParagraphContent.Replace("\n", "<br />&nbsp;&nbsp;&nbsp;&nbsp;");
                colParagraphs.Add(Paragraph);
            }

            return colParagraphs;
        }

        public Paragraph AddParagraphIndenting(Paragraph paramParagraph)
        {
            Paragraph objParagraph = new Paragraph();

            objParagraph.ParagraphContent = "&nbsp;&nbsp;&nbsp;&nbsp;" + paramParagraph.ParagraphContent;
            objParagraph.ParagraphContent = objParagraph.ParagraphContent.Replace("\n", "<br />&nbsp;&nbsp;&nbsp;&nbsp;");
            objParagraph.Chapter = paramParagraph.Chapter;
            objParagraph.Characters = paramParagraph.Characters;
            objParagraph.Location = paramParagraph.Location;
            objParagraph.Sequence = paramParagraph.Sequence;
            objParagraph.Timeline = paramParagraph.Timeline;
            objParagraph.Id = paramParagraph.Id;

            return objParagraph;
        }

        public List<Paragraph> RemoveParagraphIndenting(List<Paragraph> paramParagraphs)
        {
            List<Paragraph> colParagraphs = new List<Paragraph>();

            foreach (var Paragraph in paramParagraphs)
            {
                Paragraph.ParagraphContent = Paragraph.ParagraphContent.Replace("&nbsp;", "");
                Paragraph.ParagraphContent = Paragraph.ParagraphContent.Replace("<br />", "\n");
                colParagraphs.Add(Paragraph);
            }

            return colParagraphs;
        }

        public Paragraph RemoveParagraphIndenting(Paragraph paramParagraph)
        {
            Paragraph objParagraph = new Paragraph();

            objParagraph.ParagraphContent = paramParagraph.ParagraphContent.Replace("&nbsp;", "");
            objParagraph.ParagraphContent = objParagraph.ParagraphContent.Replace("<br />", "\n");
            objParagraph.Chapter = paramParagraph.Chapter;
            objParagraph.Characters = paramParagraph.Characters;
            objParagraph.Location = paramParagraph.Location;
            objParagraph.Sequence = paramParagraph.Sequence;
            objParagraph.Timeline = paramParagraph.Timeline;
            objParagraph.Id = paramParagraph.Id;

            return objParagraph;
        }
        #endregion
    }
}
