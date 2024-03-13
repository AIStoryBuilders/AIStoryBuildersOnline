using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using OpenAI.Models;

namespace AIStoryBuilders.Model
{
    public class Temp
    {
        public Dictionary<string, string> colAIStoryBuildersTemp { get; set; }
    }

    public class AIStoryBuildersTempService
    {
        // Properties
        public Dictionary<string, string> colAIStoryBuildersTemp { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public AIStoryBuildersTempService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        public async Task LoadTempAsync()
        {
            Temp AIStoryBuildersTemp = await localStorage.GetItemAsync<Temp>("AIStoryBuildersTemp");

            if (AIStoryBuildersTemp == null)
            {
                // Create a new instance of the SettingsService
                AIStoryBuildersTemp = new Temp();

                AIStoryBuildersTemp.colAIStoryBuildersTemp = new Dictionary<string, string>();

                await localStorage.SetItemAsync("AIStoryBuildersTemp", AIStoryBuildersTemp);
            }

            colAIStoryBuildersTemp = AIStoryBuildersTemp.colAIStoryBuildersTemp;
        }

        public async Task SaveTempAsync(Dictionary<string, string> paramColAIStoryBuildersTemp)
        {
            var AIStoryBuildersTemp = new Temp();

            AIStoryBuildersTemp.colAIStoryBuildersTemp = paramColAIStoryBuildersTemp;

            await localStorage.SetItemAsync("AIStoryBuildersTemp", AIStoryBuildersTemp);

            // Update the properties
            colAIStoryBuildersTemp = paramColAIStoryBuildersTemp;
        }
    }
}