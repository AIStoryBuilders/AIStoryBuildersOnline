using AIStoryBuilders.Models.JSON;
using AIStoryBuilders.Models.LocalStorage;
using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using System.Text.Json.Serialization;

namespace AIStoryBuilders.Model
{
    public class AIStoryBuildersManifestService
    {
        const string PropertyTypeName = "AIStoryBuildersManifest";

        // Properties
        public JSONManifest Manifest { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public AIStoryBuildersManifestService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        // Load the database
        public async Task LoadAIStoryBuildersManifestAsync(string paramStoryName)
        {
            JSONManifest AIStoryBuildersManifest = await localStorage.GetItemAsync<JSONManifest>($"{paramStoryName}|{PropertyTypeName}");

            if (AIStoryBuildersManifest == null)
            {
                // Create a new instance of the AIStoryBuildersManifest
                AIStoryBuildersManifest = new JSONManifest();

                Manifest = AIStoryBuildersManifest;

                await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", Manifest);
            }

            if (Manifest == null)
            {
                Manifest = new JSONManifest();
            }

            Manifest = AIStoryBuildersManifest;
        }

        // Save the Manifest

        public async Task SaveManifestAsync(string paramStoryName, JSONManifest paramManifest)
        {
            await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", paramManifest);

            // Update the properties
            Manifest = paramManifest;
        }

        // Delete Manifest
        public async Task DeleteManifestAsync(string paramStoryName)
        {
            await localStorage.RemoveItemAsync($"{paramStoryName}|{PropertyTypeName}");
        }
    }
}