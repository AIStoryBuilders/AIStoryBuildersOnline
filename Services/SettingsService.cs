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
        public string DeploymentName { get; set; }
    }

    public class SettingsService
    {
        // Properties

        public string Organization { get; set; }
        public string ApiKey { get; set; }
        public string AIModel { get; set; }
        public string GUID { get; set; }
        public string AIType { get; set; }
        public string DeploymentName { get; set; }

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
                AIStoryBuildersSettings.GUID = Guid.NewGuid().ToString();
                AIStoryBuildersSettings.AIType = "";
                AIStoryBuildersSettings.DeploymentName = "";

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
            }

            Organization = AIStoryBuildersSettings.Organization;
            ApiKey = AIStoryBuildersSettings.ApiKey;
            AIModel = AIStoryBuildersSettings.AIModel;
            GUID = AIStoryBuildersSettings.GUID;
            AIType = AIStoryBuildersSettings.AIType;
            DeploymentName = AIStoryBuildersSettings.DeploymentName;
        }

        public async Task SaveSettingsAsync(string paramOrganization, string paramApiKey, string paramAIModel, string paramAIType, string paramGUID, string paramDeploymentName)
        {
            var AIStoryBuildersSettings = new Settings();

            AIStoryBuildersSettings.Organization = paramOrganization;
            AIStoryBuildersSettings.ApiKey = paramApiKey;
            AIStoryBuildersSettings.AIModel = paramAIModel;
            AIStoryBuildersSettings.GUID = paramGUID;
            AIStoryBuildersSettings.AIType = paramAIType;
            AIStoryBuildersSettings.DeploymentName = paramDeploymentName;


            await localStorage.SetItemAsync("AIStoryBuildersSettings", AIStoryBuildersSettings);

            // Update the properties
            Organization = paramOrganization;
            ApiKey = paramApiKey;
            AIModel = paramAIModel;
            AIType = paramAIType;
            GUID = paramGUID;
            DeploymentName = paramDeploymentName;
        }
    }
}