using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using System.Text.Json.Serialization;

namespace AIStoryBuilders.Model
{
    public class AIStoryBuildersStory
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Style { get; set; }
        public string Theme { get; set; }
        public string Synopsis { get; set; }
        public string ZipFile { get; set; }
    }

    public class AIStoryBuildersStories
    {
        public List<AIStoryBuildersStory> colAIStoryBuildersStory { get; set; }
    }

    public class AIStoryBuildersStoryService
    {
        // Properties

        public List<AIStoryBuildersStory> colAIStoryBuildersStory { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public AIStoryBuildersStoryService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;

            colAIStoryBuildersStory = new List<AIStoryBuildersStory>();
        }
        
        // Load the database
        public async Task LoadAIStoryBuildersStoriesAsync()
        {
            AIStoryBuildersStories AIStoryBuildersStories = await localStorage.GetItemAsync<AIStoryBuildersStories>("AIStoryBuildersStories");

            if (AIStoryBuildersStories == null)
            {
                // Create a new instance of the AIStoryBuildersStories
                AIStoryBuildersStories = new AIStoryBuildersStories();

                AIStoryBuildersStories.colAIStoryBuildersStory = new List<AIStoryBuildersStory>();

                await localStorage.SetItemAsync("AIStoryBuildersStories", AIStoryBuildersStories);
            }

            if (AIStoryBuildersStories.colAIStoryBuildersStory == null)
            {
                AIStoryBuildersStories.colAIStoryBuildersStory = new List<AIStoryBuildersStory>();
            }

            colAIStoryBuildersStory = AIStoryBuildersStories.colAIStoryBuildersStory;
        }

        // Save the database

        public async Task SaveDatabaseAsync(List<AIStoryBuildersStory> paramcolAIStoryBuildersStory)
        {
            var AIStoryBuildersStories = new AIStoryBuildersStories();

            AIStoryBuildersStories.colAIStoryBuildersStory = paramcolAIStoryBuildersStory;

            await localStorage.SetItemAsync("AIStoryBuildersStories", AIStoryBuildersStories);

            // Update the properties
            colAIStoryBuildersStory = paramcolAIStoryBuildersStory;
        }

        // Add a new story
        public async Task AddStoryAsync(AIStoryBuildersStory paramAIStoryBuildersStory)
        {
            int maxId = 0;

            if (colAIStoryBuildersStory != null)
            {
                if (colAIStoryBuildersStory.Count > 0)
                {
                    // Get the highest Id in colAIStoryBuildersStory
                    maxId = colAIStoryBuildersStory.Max(x => x.Id);
                }
            }

            // Set the Id
            paramAIStoryBuildersStory.Id = maxId + 1;

            colAIStoryBuildersStory.Add(paramAIStoryBuildersStory);

            await SaveDatabaseAsync(colAIStoryBuildersStory);
        }

        // Update a story
        public async Task UpdateStoryAsync(AIStoryBuildersStory paramAIStoryBuildersStory)
        {
            await LoadAIStoryBuildersStoriesAsync();

            // Find the story in colAIStoryBuildersStory
            AIStoryBuildersStory story = colAIStoryBuildersStory.Where(x => x.Id == paramAIStoryBuildersStory.Id).FirstOrDefault();

            if (story != null)
            {
                // Update the story
                story.Title = paramAIStoryBuildersStory.Title;
                story.Style = paramAIStoryBuildersStory.Style;
                story.Theme = paramAIStoryBuildersStory.Theme;
                story.Synopsis = paramAIStoryBuildersStory.Synopsis;
                story.ZipFile = paramAIStoryBuildersStory.ZipFile;

                await SaveDatabaseAsync(colAIStoryBuildersStory);
            }
        }

        // Delete a story
        public async Task DeleteStoryAsync(string paramStoryTitle)
        {
            await LoadAIStoryBuildersStoriesAsync();

            // Find the story in colAIStoryBuildersStory
            AIStoryBuildersStory story = colAIStoryBuildersStory.Where(x => x.Title == paramStoryTitle).FirstOrDefault();

            if (story != null)
            {
                // Remove the story from colAIStoryBuildersStory
                colAIStoryBuildersStory.Remove(story);
                await SaveDatabaseAsync(colAIStoryBuildersStory);

                // Delete the story's manifest
                AIStoryBuildersManifestService AIStoryBuildersManifestService = new AIStoryBuildersManifestService(localStorage);
                await AIStoryBuildersManifestService.DeleteManifestAsync(story.Title);
            }
        }
    }
}