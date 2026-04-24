using AIStoryBuilders.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AIStoryBuilders.Services
{
    public partial class AIStoryBuildersService
    {
        private static readonly JsonSerializerOptions _graphJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public async Task PersistGraphAsync(Story story, StoryGraph graph, string storyPath)
        {
            try
            {
                string graphDir = Path.Combine(storyPath, "Graph");
                CreateDirectory(graphDir);

                var manifest = new GraphManifest
                {
                    StoryTitle = story?.Title ?? "",
                    CreatedDate = DateTime.UtcNow,
                    Version = "1.0",
                    NodeCount = graph.Nodes.Count,
                    EdgeCount = graph.Edges.Count
                };

                var metadata = new GraphMetadata
                {
                    Title = story?.Title ?? "",
                    Genre = story?.Style ?? "",
                    Theme = story?.Theme ?? "",
                    Synopsis = story?.Synopsis ?? "",
                    CharacterCount = graph.Nodes.Count(n => n.Type == NodeType.Character),
                    LocationCount = graph.Nodes.Count(n => n.Type == NodeType.Location),
                    TimelineCount = graph.Nodes.Count(n => n.Type == NodeType.Timeline),
                    ChapterCount = graph.Nodes.Count(n => n.Type == NodeType.Chapter),
                    ParagraphCount = graph.Nodes.Count(n => n.Type == NodeType.Paragraph)
                };

                File.WriteAllText(Path.Combine(graphDir, "manifest.json"), JsonSerializer.Serialize(manifest, _graphJsonOptions));
                File.WriteAllText(Path.Combine(graphDir, "graph.json"), JsonSerializer.Serialize(graph, _graphJsonOptions));
                File.WriteAllText(Path.Combine(graphDir, "metadata.json"), JsonSerializer.Serialize(metadata, _graphJsonOptions));
            }
            catch (Exception ex)
            {
                await LogService.WriteToLogAsync("PersistGraphAsync: " + ex.Message);
            }
        }

        public StoryGraph LoadGraphFromDisk(string storyPath)
        {
            try
            {
                string graphFile = Path.Combine(storyPath, "Graph", "graph.json");
                if (!File.Exists(graphFile)) return null;
                var json = File.ReadAllText(graphFile);
                return JsonSerializer.Deserialize<StoryGraph>(json, _graphJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public async Task<StoryGraph> EnsureGraphExistsAsync(string storyTitle, IGraphBuilder graphBuilder)
        {
            try
            {
                string storyPath = Path.Combine(BasePath, storyTitle);
                if (!Directory.Exists(storyPath)) return null;

                var existing = LoadGraphFromDisk(storyPath);
                var fullStory = await LoadFullStory(new Story { Title = storyTitle });

                if (existing != null)
                {
                    GraphState.Current = existing;
                    GraphState.CurrentStory = fullStory;
                    GraphState.IsDirty = false;
                    return existing;
                }

                var built = graphBuilder.Build(fullStory);
                await PersistGraphAsync(fullStory, built, storyPath);
                GraphState.Current = built;
                GraphState.CurrentStory = fullStory;
                GraphState.IsDirty = false;
                return built;
            }
            catch (Exception ex)
            {
                await LogService.WriteToLogAsync("EnsureGraphExistsAsync: " + ex.Message);
                return null;
            }
        }

        public async Task<Story> LoadFullStory(Story storyMeta)
        {
            try
            {
                string storyPath = Path.Combine(BasePath, storyMeta.Title);
                if (!Directory.Exists(storyPath)) return storyMeta;

                // Pull hydrated metadata from stories index
                var all = await GetStorys();
                var idx = all.FirstOrDefault(s => string.Equals(s.Title, storyMeta.Title, StringComparison.OrdinalIgnoreCase));
                if (idx != null)
                {
                    storyMeta.Id = idx.Id;
                    storyMeta.Style = idx.Style;
                    storyMeta.Theme = idx.Theme;
                    storyMeta.Synopsis = idx.Synopsis;
                }

                storyMeta.Character = await GetCharacters(storyMeta);
                foreach (var c in storyMeta.Character) c.Story = storyMeta;

                storyMeta.Location = await GetLocations(storyMeta);
                foreach (var l in storyMeta.Location) l.Story = storyMeta;

                storyMeta.Timeline = await GetTimelines(storyMeta);
                foreach (var t in storyMeta.Timeline) t.Story = storyMeta;

                storyMeta.Chapter = await GetChapters(storyMeta);
                foreach (var chap in storyMeta.Chapter)
                {
                    chap.Story = storyMeta;
                    chap.Paragraph = await GetParagraphs(chap);
                    foreach (var p in chap.Paragraph)
                    {
                        p.Chapter = chap;
                    }
                }

                return storyMeta;
            }
            catch (Exception ex)
            {
                await LogService.WriteToLogAsync("LoadFullStory: " + ex.Message);
                return storyMeta;
            }
        }

        public async Task<StoryGraph> RefreshGraphIfDirtyAsync(string storyTitle, IGraphBuilder graphBuilder)
        {
            if (GraphState.Current != null && !GraphState.IsDirty &&
                string.Equals(GraphState.Current.StoryTitle, storyTitle, StringComparison.OrdinalIgnoreCase))
            {
                return GraphState.Current;
            }

            var fullStory = await LoadFullStory(new Story { Title = storyTitle });
            var built = graphBuilder.Build(fullStory);
            string storyPath = Path.Combine(BasePath, storyTitle);
            await PersistGraphAsync(fullStory, built, storyPath);
            GraphState.Current = built;
            GraphState.CurrentStory = fullStory;
            GraphState.IsDirty = false;
            return built;
        }
    }
}
