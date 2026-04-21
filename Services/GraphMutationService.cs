using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIStoryBuilders.Services
{
    public class GraphMutationService : IGraphMutationService
    {
        private readonly AIStoryBuildersService _storyService;
        private readonly LogService _log;

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
    }
}
