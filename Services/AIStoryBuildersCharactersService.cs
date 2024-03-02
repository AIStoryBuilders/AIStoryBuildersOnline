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

        // Add a new character
        public async Task AddCharacterAsync(string paramStoryName, Character paramCharacter)
        {
            await LoadAIStoryBuildersCharactersAsync(paramStoryName);

            characters.Add(paramCharacter);

            await SaveDatabaseAsync(paramStoryName, characters);
        }

        // Update a character
        public async Task UpdateCharacterAsync(string paramStoryName, Character paramCharacter)
        {
            await LoadAIStoryBuildersCharactersAsync(paramStoryName);

            var character = characters.Where(x => x.name == paramCharacter.name).FirstOrDefault();

            if (character != null)
            {
                characters.Remove(character);
                characters.Add(paramCharacter);   
                await SaveDatabaseAsync(paramStoryName, characters);
            }
        }

        // Delete a character
        public async Task DeleteCharacterAsync(string paramStoryName, Character paramCharacter)
        {
            await LoadAIStoryBuildersCharactersAsync(paramStoryName);

            var character = characters.Where(x => x.name == paramCharacter.name).FirstOrDefault();

            if (character != null)
            {
                characters.Remove(character);
                await SaveDatabaseAsync(paramStoryName, characters);
            }
        }

        // Delete all characters
        public async Task DeleteAllCharactersAsync(string paramStoryName)
        {
            await localStorage.RemoveItemAsync($"{paramStoryName}|{PropertyTypeName}");
        }

        // Convert AIStoryBuilders.Models.Character to AIStoryBuilders.Models.LocalStorage.Character
        public Character ConvertCharacterToCharacter(Models.Character paramCharacter)
        {
            Character character = new Character();

            return character;
        }

    }
}