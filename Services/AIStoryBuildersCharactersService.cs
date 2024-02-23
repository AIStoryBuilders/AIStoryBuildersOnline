using AIStoryBuilders.Models.LocalStorage;
using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using System.Text.Json.Serialization;

namespace AIStoryBuilders.Model
{
    public class AIStoryBuildersCharactersService
    {
        const string PropertyTypeName = "AIStoryBuildersCharacters";

        // Properties
        public List<Character> characters { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public AIStoryBuildersCharactersService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        // Load the database
        public async Task LoadAIStoryBuildersCharactersAsync(string paramStoryName)
        {
            List<Character> AIStoryBuildersCharacters = await localStorage.GetItemAsync<List<Character>>($"{paramStoryName}|{PropertyTypeName}");

            if (AIStoryBuildersCharacters == null)
            {
                // Create a new instance of the AIStoryBuildersCharacters
                AIStoryBuildersCharacters = new List<Character>();

                characters = AIStoryBuildersCharacters;

                await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", characters);
            }

            if (characters == null)
            {
                characters = new List<Character>();
            }

            characters = AIStoryBuildersCharacters;
        }

        // Save the database

        public async Task SaveDatabaseAsync(string paramStoryName, List<Character> paramCharacters)
        {
            await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", paramCharacters);

            // Update the properties
            characters = paramCharacters;
        }
    }
}