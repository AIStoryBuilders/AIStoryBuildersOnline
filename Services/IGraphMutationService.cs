using AIStoryBuilders.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIStoryBuilders.Services
{
    public interface IGraphMutationService
    {
        Task<MutationResult> RenameCharacterAsync(string oldName, string newName, bool confirmed);
        Task<MutationResult> UpdateCharacterBackgroundAsync(string name, string type, string description, string timeline, bool confirmed);
        Task<MutationResult> AddCharacterAsync(string name, string role, string backstory, bool confirmed);
        Task<MutationResult> DeleteCharacterAsync(string name, bool confirmed);
        Task<MutationResult> AddLocationAsync(string name, string description, bool confirmed);
        Task<MutationResult> UpdateLocationDescriptionAsync(string name, string description, string timeline, bool confirmed);
        Task<MutationResult> DeleteLocationAsync(string name, bool confirmed);
        Task<MutationResult> AddTimelineAsync(string name, string description, string start, string end, bool confirmed);
        Task<MutationResult> UpdateWorldFactsAsync(string facts, bool confirmed);

        // Chapter / paragraph structural edits (preview + confirm).
        Task<MutationResult> AddChapterAsync(string title, string synopsis, int? sequence, bool confirmed);
        Task<MutationResult> UpdateChapterAsync(string title, string newTitle, string synopsis, bool confirmed);
        Task<MutationResult> DeleteChapterAsync(string title, bool confirmed);

        Task<MutationResult> AddParagraphAsync(string chapter, int? sequence, string text,
            string location, string timeline, IEnumerable<string> characters, bool confirmed);
        Task<MutationResult> UpdateParagraphTextAsync(string chapter, int index, string text, bool confirmed);
        Task<MutationResult> UpdateParagraphMetadataAsync(string chapter, int index,
            string location, string timeline, IEnumerable<string> characters, bool confirmed);
        Task<MutationResult> DeleteParagraphAsync(string chapter, int index, bool confirmed);
    }
}
