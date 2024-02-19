using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using System.Text.Json.Serialization;

namespace AIStoryBuilders.Model
{
    public class Settings
    {
        public string Organization { get; set; }
        public string ApiKey { get; set; }
        public string AIModel { get; set; }
    }

    public class SettingsService
    {
        // Properties

        public string Organization { get; set; }
        public string ApiKey { get; set; }
        public string AIModel { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public SettingsService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        public async Task LoadSettingsAsync()
        {
            Settings AIStoryBuildersSettings = await localStorage.GetItemAsync<Settings>("AIStoryBuildersSettings");

            if (AIStoryBuildersSettings == null)
            {
                // Create a new instance of the SettingsService
                AIStoryBuildersSettings = new Settings();

                AIStoryBuildersSettings.Organization = "";
                AIStoryBuildersSettings.ApiKey = "";
                AIStoryBuildersSettings.AIModel = "gpt-4-turbo-preview";

                await localStorage.SetItemAsync("AIStoryBuildersSettings", AIStoryBuildersSettings);
            }

            Organization = AIStoryBuildersSettings.Organization;
            ApiKey = AIStoryBuildersSettings.ApiKey;
            AIModel = AIStoryBuildersSettings.AIModel;
        }

        public async Task SaveSettingsAsync(string paramOrganization, string paramApiKey, string paramAIModel)
        {
            var AIStoryBuildersSettings = new Settings();

            AIStoryBuildersSettings.Organization = paramOrganization;
            AIStoryBuildersSettings.ApiKey = paramApiKey;
            AIStoryBuildersSettings.AIModel = paramAIModel;

            await localStorage.SetItemAsync("AIStoryBuildersSettings", AIStoryBuildersSettings);

            // Update the properties
            Organization = paramOrganization;
            ApiKey = paramApiKey;
            AIModel = paramAIModel;
        }
    }
}