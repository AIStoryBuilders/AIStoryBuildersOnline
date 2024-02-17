using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using OpenAI.Models;

namespace AIStoryBuilders.Model
{
    public class Database
    {
        public Dictionary<string, string> colAIStoryBuildersDatabase { get; set; }
    }

    public class DatabaseService
    {
        // Properties
        public Dictionary<string, string> colAIStoryBuildersDatabase { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public DatabaseService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        public async Task LoadSettingsAsync()
        {
            Database AIStoryBuildersDatabase = await localStorage.GetItemAsync<Database>("AIStoryBuildersDatabase");

            if (AIStoryBuildersDatabase == null)
            {
                // Create a new instance of the SettingsService
                AIStoryBuildersDatabase = new Database();

                AIStoryBuildersDatabase.colAIStoryBuildersDatabase = new Dictionary<string, string>();

                await localStorage.SetItemAsync("AIStoryBuildersDatabase", AIStoryBuildersDatabase);
            }

            colAIStoryBuildersDatabase = AIStoryBuildersDatabase.colAIStoryBuildersDatabase;
        }

        public async Task SaveDatabase(Dictionary<string, string> paramColAIStoryBuildersDatabase)
        {
            var AIStoryBuildersDatabase = new Database();

            AIStoryBuildersDatabase.colAIStoryBuildersDatabase = paramColAIStoryBuildersDatabase;

            await localStorage.SetItemAsync("AIStoryBuildersDatabase", AIStoryBuildersDatabase);

            // Update the properties
            colAIStoryBuildersDatabase = paramColAIStoryBuildersDatabase;
        }
    }
}