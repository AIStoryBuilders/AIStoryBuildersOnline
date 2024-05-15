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
        public string GUID { get; set; }
        public string AIType { get; set; }
        public string Endpoint { get; set; }
        public string AIEmbeddingModel { get; set; }
        public string ApiVersion { get; set; }
    }

    public class SettingsService
    {
        // Properties

        public string Organization { get; set; }
        public string ApiKey { get; set; }
        public string AIModel { get; set; }
        public string GUID { get; set; }
        public string AIType { get; set; }
        public string Endpoint { get; set; }
        public string AIEmbeddingModel { get; set; }
        public string ApiVersion { get; set; }

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
                AIStoryBuildersSettings.AIModel = "gpt-4o";
                AIStoryBuildersSettings.GUID = Guid.NewGuid().ToString();
                AIStoryBuildersSettings.AIType = "";
                AIStoryBuildersSettings.Endpoint = "";
                AIStoryBuildersSettings.ApiVersion = "";
                AIStoryBuildersSettings.AIEmbeddingModel = "";                

                await localStorage.SetItemAsync("AIStoryBuildersSettings", AIStoryBuildersSettings);
            }
            else
            {
                // Create GUID if it is blank
                if (string.IsNullOrEmpty(AIStoryBuildersSettings.GUID))
                {
                    AIStoryBuildersSettings.GUID = Guid.NewGuid().ToString();
                    await localStorage.SetItemAsync("AIStoryBuildersSettings", AIStoryBuildersSettings);
                }

                // Set if AIType is blank
                if (AIStoryBuildersSettings.AIType == null || AIStoryBuildersSettings.AIType == "")
                {
                    AIStoryBuildersSettings.AIType = "OpenAI";
                    await localStorage.SetItemAsync("AIStoryBuildersSettings", AIStoryBuildersSettings);
                }
            }

            Organization = AIStoryBuildersSettings.Organization;
            ApiKey = AIStoryBuildersSettings.ApiKey;
            AIModel = AIStoryBuildersSettings.AIModel;
            GUID = AIStoryBuildersSettings.GUID;
            AIType = AIStoryBuildersSettings.AIType;
            Endpoint = AIStoryBuildersSettings.Endpoint;
            ApiVersion = AIStoryBuildersSettings.ApiVersion;
            AIEmbeddingModel = AIStoryBuildersSettings.AIEmbeddingModel;
        }

        public async Task SaveSettingsAsync(string paramOrganization, string paramApiKey, string paramAIModel, string paramAIType, string paramGUID, string paramEndpoint, string paramApiVersion, string paramAIEmbeddingModel)
        {
            var AIStoryBuildersSettings = new Settings();

            AIStoryBuildersSettings.Organization = paramOrganization;
            AIStoryBuildersSettings.ApiKey = paramApiKey;
            AIStoryBuildersSettings.AIModel = paramAIModel;
            AIStoryBuildersSettings.GUID = paramGUID;
            AIStoryBuildersSettings.AIType = paramAIType;
            AIStoryBuildersSettings.Endpoint = paramEndpoint;
            AIStoryBuildersSettings.ApiVersion = paramApiVersion;
            AIStoryBuildersSettings.AIEmbeddingModel = paramAIEmbeddingModel;

            await localStorage.SetItemAsync("AIStoryBuildersSettings", AIStoryBuildersSettings);

            // Update the properties
            Organization = paramOrganization;
            ApiKey = paramApiKey;
            AIModel = paramAIModel;
            AIType = paramAIType;
            GUID = paramGUID;
            Endpoint = paramEndpoint;
            ApiVersion = paramApiVersion;
            AIEmbeddingModel = paramAIEmbeddingModel;
        }
    }
}