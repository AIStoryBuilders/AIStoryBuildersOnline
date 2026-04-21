using AIStoryBuilders.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIStoryBuilders.Services
{
    public interface IStoryChatService
    {
        IAsyncEnumerable<string> SendMessageAsync(string userMessage, string sessionId);
        void ClearSession(string sessionId);
        void RefreshClient();
        void SetActiveStory(Story story);
        ConversationSession GetOrCreateSession(string sessionId);
    }
}
