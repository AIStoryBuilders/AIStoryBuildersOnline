using AIStoryBuilders.AI;
using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIStoryBuilders.Services
{
    public class GraphMutationService : IGraphMutationService
    {
        private readonly AIStoryBuildersService _storyService;
        private readonly LogService _log;
        private static readonly SemaphoreSlim _writeGate = new(1, 1);

        public GraphMutationService(AIStoryBuildersService storyService, LogService log)
        {
            _storyService = storyService;
            _log = log;
        }

        private Story CurrentStory => GraphState.CurrentStory;

        public async Task<MutationResult> RenameCharacterAsync(string oldName, string newName, bool confirmed)
        {
            if (!confirmed)
            {
                return new MutationResult
                {
                    IsPreview = true,
                    Success = true,
                    Summary = $"Will rename character '{oldName}' to '{newName}' across all paragraphs, metadata, and embeddings."
                };
            }
            try
            {
                if (CurrentStory == null) return Fail("No active story.");
                var character = new Character
                {
                    CharacterName = newName,
                    Story = CurrentStory
                };
                int updatedCount = await _storyService.UpdateCharacterNameAsync(character, oldName);
                GraphState.MarkDirty();
                return new MutationResult
                {
                    IsPreview = false,
                    Success = true,
                    Summary = $"Renamed '{oldName}' to '{newName}' ({updatedCount} paragraphs touched).",
                    EmbeddingsUpdated = updatedCount,
                    GraphRefreshed = true
                };
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("RenameCharacterAsync: " + ex.Message);
                return Fail(ex.Message);
            }
        }

        public async Task<MutationResult> UpdateCharacterBackgroundAsync(string name, string type, string description, string timeline, bool confirmed)
        {
            if (!confirmed) return Preview($"Will add background '{type}' for character '{name}' on timeline '{timeline}'.");
            try
            {
                if (CurrentStory == null) return Fail("No active story.");
                var character = (await _storyService.GetCharacters(CurrentStory))
                    .FirstOrDefault(c => string.Equals(c.CharacterName, name, StringComparison.OrdinalIgnoreCase));
                if (character == null) return Fail($"Character '{name}' not found.");
                character.Story = CurrentStory;
                character.CharacterBackground ??= new List<CharacterBackground>();
                character.CharacterBackground.Add(new CharacterBackground
                {
                    Type = type ?? "Fact",
                    Description = description ?? "",
                    Timeline = new Timeline { TimelineName = timeline ?? "" }
                });
                await _storyService.AddUpdateCharacterAsync(character, name);
                GraphState.MarkDirty();
                return Ok($"Updated background for '{name}'.");
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("UpdateCharacterBackgroundAsync: " + ex.Message);
                return Fail(ex.Message);
            }
        }

        public async Task<MutationResult> AddCharacterAsync(string name, string role, string backstory, bool confirmed)
        {
            if (!confirmed) return Preview($"Will add a new character '{name}'.");
            try
            {
                if (CurrentStory == null) return Fail("No active story.");
                var character = new Character
                {
                    CharacterName = name,
                    Story = CurrentStory,
                    CharacterBackground = new List<CharacterBackground>()
                };
                if (!string.IsNullOrWhiteSpace(role))
                    character.CharacterBackground.Add(new CharacterBackground { Type = "Role", Description = role, Timeline = new Timeline { TimelineName = "" } });
                if (!string.IsNullOrWhiteSpace(backstory))
                    character.CharacterBackground.Add(new CharacterBackground { Type = "History", Description = backstory, Timeline = new Timeline { TimelineName = "" } });
                await _storyService.AddUpdateCharacterAsync(character, name);
                GraphState.MarkDirty();
                return Ok($"Added character '{name}'.");
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("AddCharacterAsync: " + ex.Message);
                return Fail(ex.Message);
            }
        }

        public async Task<MutationResult> DeleteCharacterAsync(string name, bool confirmed)
        {
            if (!confirmed) return Preview($"Will delete character '{name}' and remove from paragraphs.");
            try
            {
                if (CurrentStory == null) return Fail("No active story.");
                var character = new Character { CharacterName = name, Story = CurrentStory };
                await _storyService.DeleteCharacter(character, name);
                GraphState.MarkDirty();
                return Ok($"Deleted character '{name}'.");
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("DeleteCharacterAsync: " + ex.Message);
                return Fail(ex.Message);
            }
        }

        public async Task<MutationResult> AddLocationAsync(string name, string description, bool confirmed)
        {
            if (!confirmed) return Preview($"Will add location '{name}'.");
            try
            {
                if (CurrentStory == null) return Fail("No active story.");
                var loc = new Location
                {
                    LocationName = name,
                    Story = CurrentStory,
                    LocationDescription = new List<LocationDescription>
                    {
                        new LocationDescription { Description = description ?? name, Timeline = new Timeline { TimelineName = "" } }
                    }
                };
                await _storyService.AddLocationAsync(loc);
                GraphState.MarkDirty();
                return Ok($"Added location '{name}'.");
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("AddLocationAsync: " + ex.Message);
                return Fail(ex.Message);
            }
        }

        public async Task<MutationResult> UpdateLocationDescriptionAsync(string name, string description, string timeline, bool confirmed)
        {
            if (!confirmed) return Preview($"Will update description for location '{name}'.");
            try
            {
                if (CurrentStory == null) return Fail("No active story.");
                var locations = await _storyService.GetLocations(CurrentStory);
                var loc = locations.FirstOrDefault(l => string.Equals(l.LocationName, name, StringComparison.OrdinalIgnoreCase));
                if (loc == null) return Fail($"Location '{name}' not found.");
                loc.Story = CurrentStory;
                loc.LocationDescription ??= new List<LocationDescription>();
                loc.LocationDescription.Add(new LocationDescription { Description = description ?? "", Timeline = new Timeline { TimelineName = timeline ?? "" } });
                await _storyService.UpdateLocationDescriptions(loc);
                GraphState.MarkDirty();
                return Ok($"Updated location '{name}'.");
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("UpdateLocationDescriptionAsync: " + ex.Message);
                return Fail(ex.Message);
            }
        }

        public async Task<MutationResult> DeleteLocationAsync(string name, bool confirmed)
        {
            if (!confirmed) return Preview($"Will delete location '{name}'.");
            try
            {
                if (CurrentStory == null) return Fail("No active story.");
                var loc = new Location { LocationName = name, Story = CurrentStory };
                // Best-effort: reuse the existing delete method if it exists; otherwise just mark dirty
                var method = _storyService.GetType().GetMethod("DeleteLocation");
                if (method != null)
                {
                    var task = method.Invoke(_storyService, new object[] { loc }) as Task;
                    if (task != null) await task;
                }
                GraphState.MarkDirty();
                return Ok($"Deleted location '{name}'.");
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("DeleteLocationAsync: " + ex.Message);
                return Fail(ex.Message);
            }
        }

        public async Task<MutationResult> AddTimelineAsync(string name, string description, string start, string end, bool confirmed)
        {
            if (!confirmed) return Preview($"Will add timeline '{name}'.");
            try
            {
                if (CurrentStory == null) return Fail("No active story.");
                var tl = new Timeline
                {
                    TimelineName = name,
                    TimelineDescription = description ?? "",
                    Story = CurrentStory,
                    StartDate = DateTime.TryParse(start, out var sd) ? sd : (DateTime?)DateTime.Now,
                    StopDate = DateTime.TryParse(end, out var ed) ? ed : (DateTime?)DateTime.Now.AddDays(1)
                };
                await _storyService.AddTimeline(tl);
                GraphState.MarkDirty();
                return Ok($"Added timeline '{name}'.");
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("AddTimelineAsync: " + ex.Message);
                return Fail(ex.Message);
            }
        }

        public Task<MutationResult> UpdateWorldFactsAsync(string facts, bool confirmed)
        {
            if (!confirmed) return Task.FromResult(Preview("Will update world facts / story synopsis."));
            try
            {
                if (CurrentStory == null) return Task.FromResult(Fail("No active story."));
                CurrentStory.Synopsis = facts ?? CurrentStory.Synopsis;
                _ = _storyService.UpdateStory(CurrentStory);
                GraphState.MarkDirty();
                return Task.FromResult(Ok("Updated world facts."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Fail(ex.Message));
            }
        }

        private static MutationResult Preview(string summary) =>
            new() { IsPreview = true, Success = true, Summary = summary };

        private static MutationResult Ok(string summary) =>
            new() { IsPreview = false, Success = true, Summary = summary, GraphRefreshed = true };

        private static MutationResult Fail(string error) =>
            new() { IsPreview = false, Success = false, Summary = error, Error = error };

        // ----------------------------------------------------------------------------------------
        // Chapter / paragraph structural edits
        // ----------------------------------------------------------------------------------------

        private static bool IsUnsafeName(string name) =>
            string.IsNullOrWhiteSpace(name) || name.Contains("..") ||
            name.Contains('/') || name.Contains('\\') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

        private static string Excerpt(string text, int max = 160)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        private async Task<Chapter> ResolveChapterAsync(string title)
        {
            if (CurrentStory == null) return null;
            var chapters = await _storyService.GetChapters(CurrentStory);
            return chapters.FirstOrDefault(c =>
                string.Equals(c.ChapterName, title, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.ChapterName?.Replace(" ", ""), title?.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
        }

        private async Task<(Chapter chapter, Paragraph paragraph)> ResolveParagraphAsync(string chapterTitle, int index)
        {
            var chapter = await ResolveChapterAsync(chapterTitle);
            if (chapter == null) return (null, null);
            var paragraphs = await _storyService.GetParagraphs(chapter);
            var p = paragraphs.FirstOrDefault(x => x.Sequence == index);
            return (chapter, p);
        }

        public async Task<MutationResult> AddChapterAsync(string title, string synopsis, int? sequence, bool confirmed)
        {
            if (CurrentStory == null) return Fail("No active story.");
            if (IsUnsafeName(title)) return Fail("Invalid chapter title.");

            int existingCount = await _storyService.CountChapters(CurrentStory);
            int seq = sequence.HasValue ? Math.Max(1, Math.Min(sequence.Value, existingCount + 1)) : existingCount + 1;

            if (!confirmed)
            {
                return new MutationResult
                {
                    IsPreview = true,
                    Success = true,
                    TargetKind = "Chapter",
                    TargetId = $"Chapter {seq} ({title})",
                    Summary = $"Will add new chapter '{title}' at sequence {seq}. Existing chapters: {existingCount}.",
                    AfterExcerpt = Excerpt(synopsis)
                };
            }

            await _writeGate.WaitAsync();
            try
            {
                var chapter = new Chapter
                {
                    ChapterName = $"Chapter {seq}",
                    Sequence = seq,
                    Synopsis = synopsis ?? " ",
                    Story = CurrentStory
                };

                if (seq <= existingCount)
                {
                    // Shift existing chapters >= seq up.
                    await _storyService.RestructureChapters(chapter, RestructureType.Add);
                    await _storyService.InsertChapterAsync(chapter);
                }
                else
                {
                    await _storyService.AddChapterAsync(chapter, $"Chapter{seq}");
                }

                GraphState.MarkDirty();
                return new MutationResult
                {
                    IsPreview = false,
                    Success = true,
                    GraphRefreshed = true,
                    EmbeddingsUpdated = 1,
                    TargetKind = "Chapter",
                    TargetId = $"Chapter {seq}",
                    Summary = $"Added chapter '{title}' at sequence {seq}."
                };
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("AddChapterAsync: " + ex.Message);
                return Fail(ex.Message);
            }
            finally { _writeGate.Release(); }
        }

        public async Task<MutationResult> UpdateChapterAsync(string title, string newTitle, string synopsis, bool confirmed)
        {
            if (CurrentStory == null) return Fail("No active story.");
            var chapter = await ResolveChapterAsync(title);
            if (chapter == null) return Fail($"Chapter '{title}' not found.");
            if (!string.IsNullOrWhiteSpace(newTitle) &&
                !string.Equals(newTitle, chapter.ChapterName, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("Renaming a chapter is not supported via UpdateChapter. Use a separate RenameChapter tool when available.");
            }
            if (string.IsNullOrWhiteSpace(synopsis)) return Fail("Synopsis must not be empty.");

            if (!confirmed)
            {
                return new MutationResult
                {
                    IsPreview = true,
                    Success = true,
                    TargetKind = "Chapter",
                    TargetId = chapter.ChapterName,
                    Summary = $"Will rewrite synopsis of '{chapter.ChapterName}' ({Excerpt(chapter.Synopsis).Length} -> {Excerpt(synopsis).Length} chars). Synopsis embedding will be regenerated.",
                    BeforeExcerpt = Excerpt(chapter.Synopsis),
                    AfterExcerpt = Excerpt(synopsis)
                };
            }

            await _writeGate.WaitAsync();
            try
            {
                chapter.Story = CurrentStory;
                chapter.Synopsis = synopsis;
                await _storyService.UpdateChapterAsync(chapter);
                GraphState.MarkDirty();
                return new MutationResult
                {
                    IsPreview = false,
                    Success = true,
                    GraphRefreshed = true,
                    EmbeddingsUpdated = 1,
                    TargetKind = "Chapter",
                    TargetId = chapter.ChapterName,
                    Summary = $"Updated synopsis of '{chapter.ChapterName}'."
                };
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("UpdateChapterAsync: " + ex.Message);
                return Fail(ex.Message);
            }
            finally { _writeGate.Release(); }
        }

        public async Task<MutationResult> DeleteChapterAsync(string title, bool confirmed)
        {
            if (CurrentStory == null) return Fail("No active story.");
            var chapter = await ResolveChapterAsync(title);
            if (chapter == null) return Fail($"Chapter '{title}' not found.");

            int paragraphCount = await _storyService.CountParagraphs(chapter);

            if (!confirmed)
            {
                return new MutationResult
                {
                    IsPreview = true,
                    Success = true,
                    TargetKind = "Chapter",
                    TargetId = chapter.ChapterName,
                    Summary = $"Will DELETE chapter '{chapter.ChapterName}' and all {paragraphCount} paragraph(s). Remaining chapters will be renumbered.",
                    BeforeExcerpt = Excerpt(chapter.Synopsis)
                };
            }

            await _writeGate.WaitAsync();
            try
            {
                chapter.Story = CurrentStory;
                _storyService.DeleteChapter(chapter);
                await _storyService.RestructureChapters(chapter, RestructureType.Delete);
                GraphState.MarkDirty();
                return new MutationResult
                {
                    IsPreview = false,
                    Success = true,
                    GraphRefreshed = true,
                    TargetKind = "Chapter",
                    TargetId = chapter.ChapterName,
                    Summary = $"Deleted chapter '{chapter.ChapterName}' ({paragraphCount} paragraph(s))."
                };
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("DeleteChapterAsync: " + ex.Message);
                return Fail(ex.Message);
            }
            finally { _writeGate.Release(); }
        }

        public async Task<MutationResult> AddParagraphAsync(string chapterTitle, int? sequence, string text,
            string location, string timeline, IEnumerable<string> characters, bool confirmed)
        {
            if (CurrentStory == null) return Fail("No active story.");
            if (string.IsNullOrWhiteSpace(text)) return Fail("Paragraph text must not be empty.");

            var chapter = await ResolveChapterAsync(chapterTitle);
            if (chapter == null) return Fail($"Chapter '{chapterTitle}' not found.");

            int existingCount = await _storyService.CountParagraphs(chapter);
            int seq = sequence.HasValue ? Math.Max(1, Math.Min(sequence.Value, existingCount + 1)) : existingCount + 1;
            var chars = (characters ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .ToList();

            if (!confirmed)
            {
                return new MutationResult
                {
                    IsPreview = true,
                    Success = true,
                    TargetKind = "Paragraph",
                    TargetId = $"{chapter.ChapterName} / Paragraph {seq}",
                    Summary = $"Will insert paragraph at sequence {seq} in {chapter.ChapterName} ({text.Length} chars). Location='{location}', Timeline='{timeline}', Characters=[{string.Join(",", chars)}]. Embedding will be generated.",
                    AfterExcerpt = Excerpt(text)
                };
            }

            await _writeGate.WaitAsync();
            try
            {
                chapter.Story = CurrentStory;
                var paragraph = new Paragraph
                {
                    Sequence = seq,
                    ParagraphContent = text,
                    Location = new Location { LocationName = location ?? "" },
                    Timeline = new Timeline { TimelineName = timeline ?? "" },
                    Characters = chars.Select(n => new Character { CharacterName = n }).ToList()
                };

                // AddParagraph restructures existing paragraphs upward and writes a skeleton file.
                await _storyService.AddParagraph(chapter, paragraph);
                // Write actual prose + embedding.
                int embeddings = 0;
                try
                {
                    await _storyService.UpdateParagraph(chapter, paragraph);
                    embeddings = 1;
                }
                catch (Exception ex)
                {
                    await _log.WriteToLogAsync("AddParagraphAsync (embedding): " + ex.Message);
                }

                GraphState.MarkDirty();
                return new MutationResult
                {
                    IsPreview = false,
                    Success = true,
                    GraphRefreshed = true,
                    EmbeddingsUpdated = embeddings,
                    TargetKind = "Paragraph",
                    TargetId = $"{chapter.ChapterName} / Paragraph {seq}",
                    Summary = $"Added paragraph {seq} to {chapter.ChapterName}."
                };
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("AddParagraphAsync: " + ex.Message);
                return Fail(ex.Message);
            }
            finally { _writeGate.Release(); }
        }

        public async Task<MutationResult> UpdateParagraphTextAsync(string chapterTitle, int index, string text, bool confirmed)
        {
            if (CurrentStory == null) return Fail("No active story.");
            if (string.IsNullOrWhiteSpace(text)) return Fail("Paragraph text must not be empty.");

            var (chapter, paragraph) = await ResolveParagraphAsync(chapterTitle, index);
            if (chapter == null) return Fail($"Chapter '{chapterTitle}' not found.");
            if (paragraph == null) return Fail($"Paragraph {index} not found in {chapter.ChapterName}.");

            if (!confirmed)
            {
                return new MutationResult
                {
                    IsPreview = true,
                    Success = true,
                    TargetKind = "Paragraph",
                    TargetId = $"{chapter.ChapterName} / Paragraph {index}",
                    Summary = $"Will replace text in {chapter.ChapterName} / Paragraph {index} ({(paragraph.ParagraphContent ?? "").Length} -> {text.Length} chars). Embedding will be regenerated.",
                    BeforeExcerpt = Excerpt(paragraph.ParagraphContent),
                    AfterExcerpt = Excerpt(text)
                };
            }

            await _writeGate.WaitAsync();
            try
            {
                chapter.Story = CurrentStory;
                paragraph.ParagraphContent = text;
                int embeddings = 0;
                try
                {
                    await _storyService.UpdateParagraph(chapter, paragraph);
                    embeddings = 1;
                }
                catch (Exception ex)
                {
                    await _log.WriteToLogAsync("UpdateParagraphTextAsync (embedding): " + ex.Message);
                    return new MutationResult
                    {
                        IsPreview = false,
                        Success = true,
                        EmbeddingsUpdated = 0,
                        GraphRefreshed = true,
                        TargetKind = "Paragraph",
                        TargetId = $"{chapter.ChapterName} / Paragraph {index}",
                        Summary = $"Updated paragraph text but embedding failed: {ex.Message}"
                    };
                }

                GraphState.MarkDirty();
                return new MutationResult
                {
                    IsPreview = false,
                    Success = true,
                    GraphRefreshed = true,
                    EmbeddingsUpdated = embeddings,
                    TargetKind = "Paragraph",
                    TargetId = $"{chapter.ChapterName} / Paragraph {index}",
                    Summary = $"Updated text for {chapter.ChapterName} / Paragraph {index}."
                };
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("UpdateParagraphTextAsync: " + ex.Message);
                return Fail(ex.Message);
            }
            finally { _writeGate.Release(); }
        }

        public async Task<MutationResult> UpdateParagraphMetadataAsync(string chapterTitle, int index,
            string location, string timeline, IEnumerable<string> characters, bool confirmed)
        {
            if (CurrentStory == null) return Fail("No active story.");
            var (chapter, paragraph) = await ResolveParagraphAsync(chapterTitle, index);
            if (chapter == null) return Fail($"Chapter '{chapterTitle}' not found.");
            if (paragraph == null) return Fail($"Paragraph {index} not found in {chapter.ChapterName}.");

            var chars = (characters ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .ToList();

            if (!confirmed)
            {
                return new MutationResult
                {
                    IsPreview = true,
                    Success = true,
                    TargetKind = "Paragraph",
                    TargetId = $"{chapter.ChapterName} / Paragraph {index}",
                    Summary = $"Will update metadata for {chapter.ChapterName} / Paragraph {index}: Location='{location}', Timeline='{timeline}', Characters=[{string.Join(",", chars)}]. Prose and embedding unchanged."
                };
            }

            await _writeGate.WaitAsync();
            try
            {
                // Metadata-only update: edit the raw file in place to preserve existing embedding.
                var chapterNameParts = chapter.ChapterName.Split(' ');
                string folder = chapterNameParts[0] + chapterNameParts[1];
                string paragraphPath = Path.Combine(
                    _storyService.BasePath, CurrentStory.Title, "Chapters", folder, $"Paragraph{index}.txt");

                if (!File.Exists(paragraphPath)) return Fail($"Paragraph file not found: {paragraphPath}");

                string raw = File.ReadAllText(paragraphPath);
                var parts = raw.Split('|');
                if (parts.Length < 4) return Fail("Paragraph file is malformed.");

                parts[0] = location ?? "";
                parts[1] = timeline ?? "";
                parts[2] = "[" + string.Join(",", chars) + "]";
                File.WriteAllText(paragraphPath, string.Join("|", parts));

                GraphState.MarkDirty();
                return new MutationResult
                {
                    IsPreview = false,
                    Success = true,
                    GraphRefreshed = true,
                    EmbeddingsUpdated = 0,
                    TargetKind = "Paragraph",
                    TargetId = $"{chapter.ChapterName} / Paragraph {index}",
                    Summary = $"Updated metadata for {chapter.ChapterName} / Paragraph {index}."
                };
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("UpdateParagraphMetadataAsync: " + ex.Message);
                return Fail(ex.Message);
            }
            finally { _writeGate.Release(); }
        }

        public async Task<MutationResult> DeleteParagraphAsync(string chapterTitle, int index, bool confirmed)
        {
            if (CurrentStory == null) return Fail("No active story.");
            var (chapter, paragraph) = await ResolveParagraphAsync(chapterTitle, index);
            if (chapter == null) return Fail($"Chapter '{chapterTitle}' not found.");
            if (paragraph == null) return Fail($"Paragraph {index} not found in {chapter.ChapterName}.");

            if (!confirmed)
            {
                return new MutationResult
                {
                    IsPreview = true,
                    Success = true,
                    TargetKind = "Paragraph",
                    TargetId = $"{chapter.ChapterName} / Paragraph {index}",
                    Summary = $"Will DELETE {chapter.ChapterName} / Paragraph {index} and renumber later paragraphs.",
                    BeforeExcerpt = Excerpt(paragraph.ParagraphContent)
                };
            }

            await _writeGate.WaitAsync();
            try
            {
                chapter.Story = CurrentStory;
                await _storyService.DeleteParagraph(chapter, paragraph);
                GraphState.MarkDirty();
                return new MutationResult
                {
                    IsPreview = false,
                    Success = true,
                    GraphRefreshed = true,
                    TargetKind = "Paragraph",
                    TargetId = $"{chapter.ChapterName} / Paragraph {index}",
                    Summary = $"Deleted {chapter.ChapterName} / Paragraph {index}."
                };
            }
            catch (Exception ex)
            {
                await _log.WriteToLogAsync("DeleteParagraphAsync: " + ex.Message);
                return Fail(ex.Message);
            }
            finally { _writeGate.Release(); }
        }
    }
}
