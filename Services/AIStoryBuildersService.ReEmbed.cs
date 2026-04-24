using AIStoryBuilders.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AIStoryBuilders.Services
{
    public partial class AIStoryBuildersService
    {
        /// <summary>
        /// Re-saves metadata, re-embeds every paragraph / chapter / character / location,
        /// rebuilds and persists the Knowledge Graph, and reloads in-memory objects.
        /// Reports progress through <paramref name="progress"/>.
        /// </summary>
        public async Task ReloadStoryAsync(Story story, IGraphBuilder graphBuilder, IProgress<string> progress)
        {
            if (graphBuilder == null)
            {
                throw new ArgumentNullException(nameof(graphBuilder), "ReloadStoryAsync requires a non-null graphBuilder to rebuild the knowledge graph.");
            }
            try
            {
                progress?.Report("Step 1 of 5: Saving story metadata...");
                await UpdateStory(story);

                progress?.Report("Step 2 of 5: Re-embedding paragraphs...");
                var chapters = await GetChapters(story);
                foreach (var chapter in chapters)
                {
                    var ChapterNameParts = chapter.ChapterName.Split(' ');
                    string ChapterName = ChapterNameParts[0] + ChapterNameParts[1];
                    var path = $"{BasePath}/{story.Title}/Chapters/{ChapterName}";
                    if (!Directory.Exists(path)) continue;
                    foreach (var file in Directory.GetFiles(path, "Paragraph*.txt"))
                    {
                        try
                        {
                            string[] lines = File.ReadAllLines(file).Where(l => l.Trim() != "").ToArray();
                            if (lines.Length == 0) continue;
                            string[] parts = lines[0].Split('|');
                            if (parts.Length < 4) continue;
                            string prose = parts[3];
                            string vector = await OrchestratorMethods.GetVectorEmbedding(prose, false);
                            if (parts.Length >= 5) parts[parts.Length - 1] = vector;
                            else parts = parts.Concat(new[] { vector }).ToArray();
                            lines[0] = string.Join("|", parts);
                            File.WriteAllLines(file, lines);
                        }
                        catch (Exception ex)
                        {
                            await LogService.WriteToLogAsync("ReloadStoryAsync paragraph: " + ex.Message);
                        }
                    }
                }

                progress?.Report("Step 3 of 5: Re-embedding chapters, characters, and locations...");
                // Chapter synopses
                foreach (var chapter in chapters)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(chapter.Synopsis)) continue;
                        var ChapterNameParts = chapter.ChapterName.Split(' ');
                        string ChapterFolder = ChapterNameParts[0] + ChapterNameParts[1];
                        string chapterFile = $"{BasePath}/{story.Title}/Chapters/{ChapterFolder}/{ChapterFolder}.txt";
                        if (!File.Exists(chapterFile)) continue;
                        string combined = await OrchestratorMethods.GetVectorEmbedding(chapter.Synopsis, true);
                        File.WriteAllText(chapterFile, combined);
                    }
                    catch (Exception ex)
                    {
                        await LogService.WriteToLogAsync("ReloadStoryAsync chapter: " + ex.Message);
                    }
                }

                // Characters
                var characters = await GetCharacters(story);
                foreach (var c in characters)
                {
                    c.Story = story;
                    try { await AddUpdateCharacterAsync(c, c.CharacterName); }
                    catch (Exception ex) { await LogService.WriteToLogAsync("ReloadStoryAsync character: " + ex.Message); }
                }

                // Locations
                var locations = await GetLocations(story);
                foreach (var l in locations)
                {
                    l.Story = story;
                    try { await UpdateLocationDescriptions(l); }
                    catch (Exception ex) { await LogService.WriteToLogAsync("ReloadStoryAsync location: " + ex.Message); }
                }

                progress?.Report("Step 4 of 5: Rebuilding knowledge graph...");
                var fullStory = await LoadFullStory(new Story { Title = story.Title });
                var graph = graphBuilder.Build(fullStory);
                string storyPath = Path.Combine(BasePath, story.Title);
                await PersistGraphAsync(fullStory, graph, storyPath);
                GraphState.Current = graph;
                GraphState.CurrentStory = fullStory;
                GraphState.IsDirty = false;

                progress?.Report("Step 5 of 5: Reloading in-memory story objects...");
                // The fullStory is already hydrated; expose it
                story.Character = fullStory.Character;
                story.Location = fullStory.Location;
                story.Timeline = fullStory.Timeline;
                story.Chapter = fullStory.Chapter;

                progress?.Report("Reload complete.");
            }
            catch (Exception ex)
            {
                await LogService.WriteToLogAsync("ReloadStoryAsync: " + ex.Message);
                progress?.Report("Error: " + ex.Message);
                throw;
            }
        }
    }
}
