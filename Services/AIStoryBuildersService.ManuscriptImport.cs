using AIStoryBuilders.AI;
using AIStoryBuilders.Model;
using AIStoryBuilders.Models;

namespace AIStoryBuilders.Services
{
    public partial class AIStoryBuildersService
    {
        /// <summary>
        /// Persists an imported manuscript Story to the virtual file system and LocalStorage.
        /// Follows the same file layout as AddStory: Characters/*.csv, Locations/*.csv,
        /// Timelines.csv, Chapters/ChapterN/ChapterN.txt, Chapters/ChapterN/ParagraphN.txt.
        /// </summary>
        public async Task PersistImportedStoryAsync(Story story, OrchestratorMethods orchestrator)
        {
            string StoryPath = $"{BasePath}/{story.Title}";

            // Validate story title is not a duplicate
            if (Directory.Exists(StoryPath))
            {
                throw new InvalidOperationException(
                    $"A story named '{story.Title}' already exists. Please rename the file or delete the existing story first.");
            }

            string CharactersPath = $"{StoryPath}/Characters";
            string ChaptersPath = $"{StoryPath}/Chapters";
            string LocationsPath = $"{StoryPath}/Locations";

            CreateDirectory(StoryPath);
            CreateDirectory(CharactersPath);
            CreateDirectory(ChaptersPath);
            CreateDirectory(LocationsPath);

            // Write Character files
            // Format: Type|TimelineName|Description|[EmbeddingVector]
            TextEvent?.Invoke(this, new TextEventArgs("Creating character files...", 5));
            foreach (var character in story.Character)
            {
                string charName = OrchestratorMethods.SanitizeFileName(character.CharacterName);
                string charPath = $"{CharactersPath}/{charName}.csv";
                List<string> charContents = new List<string>();

                if (character.CharacterBackground != null)
                {
                    foreach (var bg in character.CharacterBackground)
                    {
                        string descriptionType = bg.Type ?? "History";
                        string timelineName = bg.Timeline?.TimelineName ?? "";
                        string description = bg.Description ?? "";

                        string vectorDescriptionAndEmbedding =
                            await orchestrator.GetVectorEmbedding(description, true);

                        charContents.Add($"{descriptionType}|{timelineName}|{vectorDescriptionAndEmbedding}" + Environment.NewLine);
                    }
                }

                if (charContents.Count == 0)
                {
                    // Write at least an empty description
                    string vectorDescriptionAndEmbedding =
                        await orchestrator.GetVectorEmbedding(charName, true);
                    charContents.Add($"Facts||{vectorDescriptionAndEmbedding}" + Environment.NewLine);
                }

                File.WriteAllLines(charPath, charContents);
            }

            // Write Location files
            // Format: Description|TimelineName|[EmbeddingVector]
            TextEvent?.Invoke(this, new TextEventArgs("Creating location files...", 5));
            foreach (var location in story.Location)
            {
                string locName = OrchestratorMethods.SanitizeFileName(location.LocationName);
                string locPath = $"{LocationsPath}/{locName}.csv";
                List<string> locContents = new List<string>();

                if (location.LocationDescription != null && location.LocationDescription.Count > 0)
                {
                    foreach (var desc in location.LocationDescription)
                    {
                        string description = desc.Description ?? "";
                        string timelineName = desc.Timeline?.TimelineName ?? "";
                        string vectorEmbedding = await orchestrator.GetVectorEmbedding(description, false);

                        var descAndTimeline = $"{description}|{timelineName}";
                        locContents.Add($"{descAndTimeline}|{vectorEmbedding}" + Environment.NewLine);
                    }
                }
                else
                {
                    string vectorEmbedding = await orchestrator.GetVectorEmbedding(locName, false);
                    locContents.Add($"{locName}||{vectorEmbedding}" + Environment.NewLine);
                }

                File.WriteAllLines(locPath, locContents);
            }

            // Write Timelines.csv
            // Format: Name|Description|StartDate|StopDate
            TextEvent?.Invoke(this, new TextEventArgs("Creating timeline file...", 5));
            List<string> timelineContents = new List<string>();
            int timelineIndex = 0;
            foreach (var timeline in story.Timeline)
            {
                string tlName = OrchestratorMethods.SanitizeFileName(timeline.TimelineName);
                string description = timeline.TimelineDescription ?? "";
                string startTime = DateTime.Now.AddDays(timelineIndex).ToShortDateString() + " " +
                                   DateTime.Now.AddDays(timelineIndex).ToShortTimeString();
                string stopTime = DateTime.Now.AddDays(timelineIndex + 1).ToShortDateString() + " " +
                                  DateTime.Now.AddDays(timelineIndex + 1).ToShortTimeString();

                timelineContents.Add($"{tlName}|{description}|{startTime}|{stopTime}");
                timelineIndex += 2;
            }

            string timelinePath = $"{StoryPath}/Timelines.csv";
            File.WriteAllLines(timelinePath, timelineContents);

            // Write Chapter folders and files
            int chapterNumber = 1;
            foreach (var chapter in story.Chapter)
            {
                string chapterFolderPath = $"{ChaptersPath}/Chapter{chapterNumber}";
                CreateDirectory(chapterFolderPath);

                TextEvent?.Invoke(this, new TextEventArgs($"Creating Chapter {chapterNumber}...", 5));

                // Write ChapterN.txt: Synopsis|[EmbeddingVector]
                string synopsis = chapter.Synopsis ?? "";
                string chapterSynopsisAndEmbedding = await orchestrator.GetVectorEmbedding(synopsis, true);
                string chapterFilePath = $"{chapterFolderPath}/Chapter{chapterNumber}.txt";
                File.WriteAllText(chapterFilePath, chapterSynopsisAndEmbedding);

                // Write ParagraphN.txt files
                // Format: Location|Timeline|[Characters]|Content|[EmbeddingVector]
                int paragraphNumber = 1;
                if (chapter.Paragraph != null)
                {
                    foreach (var paragraph in chapter.Paragraph)
                    {
                        string paraFilePath = $"{chapterFolderPath}/Paragraph{paragraphNumber}.txt";

                        string locationName = paragraph.Location?.LocationName ?? "";
                        string timelineName = paragraph.Timeline?.TimelineName ?? "";

                        string characters = "[";
                        if (paragraph.Characters != null && paragraph.Characters.Count > 0)
                        {
                            characters += string.Join(",", paragraph.Characters.Select(c => c.CharacterName));
                        }
                        characters += "]";

                        string content = paragraph.ParagraphContent ?? "";
                        string vectorContentAndEmbedding =
                            await orchestrator.GetVectorEmbedding(content, true);

                        File.WriteAllText(paraFilePath,
                            $"{locationName}|{timelineName}|{characters}|{vectorContentAndEmbedding}");

                        paragraphNumber++;
                    }
                }

                chapterNumber++;
            }

            // Create zip and store in LocalStorage
            TextEvent?.Invoke(this, new TextEventArgs("Saving to local storage...", 5));
            string zipFileBase64String = await CreateZipFile(StoryPath);

            await AIStoryBuildersStoryService.LoadAIStoryBuildersStoriesAsync();
            await AIStoryBuildersStoryService.AddStoryAsync(new AIStoryBuildersStory
            {
                Title = story.Title,
                Style = story.Style ?? "",
                Theme = story.Theme ?? "",
                Synopsis = story.Synopsis ?? "",
                ZipFile = zipFileBase64String
            });

            await LogService.WriteToLogAsync($"Manuscript imported: {story.Title}");
        }
    }
}
