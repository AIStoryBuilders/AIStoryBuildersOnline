using AIStoryBuilders.Models;
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
    }
}
