using AIStoryBuilders.Models.LocalStorage;
using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using System.Text.Json.Serialization;

namespace AIStoryBuilders.Model
{
    public class AIStoryBuildersChaptersService
    {
        const string PropertyTypeName = "AIStoryBuildersChapters";

        // Properties
        public List<Chapter> Chapters { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public AIStoryBuildersChaptersService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        // Load the database
        public async Task LoadAIStoryBuildersChaptersAsync(string paramStoryName)
        {
            List<Chapter> AIStoryBuildersChapters = await localStorage.GetItemAsync<List<Chapter>>($"{paramStoryName}|{PropertyTypeName}");

            if (AIStoryBuildersChapters == null)
            {
                // Create a new instance of the AIStoryBuildersChapters
                AIStoryBuildersChapters = new List<Chapter>();

                Chapters = AIStoryBuildersChapters;

                await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", Chapters);
            }

            if (Chapters == null)
            {
                Chapters = new List<Chapter>();
            }

            Chapters = AIStoryBuildersChapters;
        }

        // Save the database

        public async Task SaveDatabaseAsync(string paramStoryName, List<Chapter> paramChapters)
        {
            await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", paramChapters);

            // Update the properties
            Chapters = paramChapters;
        }

        // Add a new Chapter
        public async Task AddChapterAsync(string paramStoryName, Chapter paramChapter)
        {
            await LoadAIStoryBuildersChaptersAsync(paramStoryName);

            Chapters.Add(paramChapter);

            await SaveDatabaseAsync(paramStoryName, Chapters);
        }

        // Update a Chapter
        public async Task UpdateChapterAsync(string paramStoryName, Chapter paramChapter)
        {
            await LoadAIStoryBuildersChaptersAsync(paramStoryName);

            var Chapter = Chapters.Where(x => x.chapter_name == paramChapter.chapter_name).FirstOrDefault();

            if (Chapter != null)
            {
                Chapters.Remove(Chapter);
                Chapters.Add(paramChapter);   
                await SaveDatabaseAsync(paramStoryName, Chapters);
            }
        }

        // Delete a Chapter
        public async Task DeleteChapterAsync(string paramStoryName, Chapter paramChapter)
        {
            await LoadAIStoryBuildersChaptersAsync(paramStoryName);

            var Chapter = Chapters.Where(x => x.chapter_name == paramChapter.chapter_name).FirstOrDefault();

            if (Chapter != null)
            {
                Chapters.Remove(Chapter);
                await SaveDatabaseAsync(paramStoryName, Chapters);
            }
        }

        // Delete all Chapters
        public async Task DeleteAllChaptersAsync(string paramStoryName)
        {
            await localStorage.RemoveItemAsync($"{paramStoryName}|{PropertyTypeName}");
        }
    }
}