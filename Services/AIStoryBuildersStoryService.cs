using Blazored.LocalStorage;
using Newtonsoft.Json;
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

        private static string ZipStorageKey(string title) => $"AIStoryBuildersStory_Zip_{title}";

        // Get the zip file for a specific story from its own LocalStorage key
        public async Task<string> GetZipFileAsync(string title)
        {
            return await localStorage.GetItemAsync<string>(ZipStorageKey(title));
        }

        // Save the zip file for a specific story to its own LocalStorage key
        private async Task SaveZipFileAsync(string title, string zipBase64)
        {
            await localStorage.SetItemAsync(ZipStorageKey(title), zipBase64);
        }

        // Delete the zip file for a specific story
        private async Task DeleteZipFileAsync(string title)
        {
            await localStorage.RemoveItemAsync(ZipStorageKey(title));
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

            // Migrate any inline ZipFile data to separate LocalStorage keys
            bool needsResave = false;
            foreach (var story in colAIStoryBuildersStory)
            {
                if (!string.IsNullOrEmpty(story.ZipFile))
                {
                    await SaveZipFileAsync(story.Title, story.ZipFile);
                    story.ZipFile = null;
                    needsResave = true;
                }
            }

            if (needsResave)
            {
                await SaveDatabaseAsync(colAIStoryBuildersStory);
            }
        }

        // Save the database (metadata only — zip files are stored separately)

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

            // Store zip file separately
            if (!string.IsNullOrEmpty(paramAIStoryBuildersStory.ZipFile))
            {
                await SaveZipFileAsync(paramAIStoryBuildersStory.Title, paramAIStoryBuildersStory.ZipFile);
                paramAIStoryBuildersStory.ZipFile = null;
            }

            colAIStoryBuildersStory.Add(paramAIStoryBuildersStory);

            await SaveDatabaseAsync(colAIStoryBuildersStory);
        }

        // Update a story
        public async Task UpdateStoryAsync(AIStoryBuildersStory paramAIStoryBuildersStory)
        {
            // Store zip file separately
            if (!string.IsNullOrEmpty(paramAIStoryBuildersStory.ZipFile))
            {
                await SaveZipFileAsync(paramAIStoryBuildersStory.Title, paramAIStoryBuildersStory.ZipFile);
            }

            await LoadAIStoryBuildersStoriesAsync();

            // Find the story in colAIStoryBuildersStory
            AIStoryBuildersStory story = colAIStoryBuildersStory.Where(x => x.Title == paramAIStoryBuildersStory.Title).FirstOrDefault();

            if (story != null)
            {
                // Update the story metadata (ZipFile is stored separately)
                story.Title = paramAIStoryBuildersStory.Title;
                story.Style = paramAIStoryBuildersStory.Style;
                story.Theme = paramAIStoryBuildersStory.Theme;
                story.Synopsis = paramAIStoryBuildersStory.Synopsis;
                story.ZipFile = null;

                await SaveDatabaseAsync(colAIStoryBuildersStory);
            }
        }

        // Delete a story
        public async Task DeleteStoryAsync(string paramStoryTitle)
        {
            // Delete the separate zip storage
            await DeleteZipFileAsync(paramStoryTitle);

            await LoadAIStoryBuildersStoriesAsync();

            // Find the story in colAIStoryBuildersStory
            AIStoryBuildersStory story = colAIStoryBuildersStory.Where(x => x.Title == paramStoryTitle).FirstOrDefault();

            if (story != null)
            {
                // Remove the story from colAIStoryBuildersStory
                colAIStoryBuildersStory.Remove(story);
                await SaveDatabaseAsync(colAIStoryBuildersStory);
            }
        }
    }
}