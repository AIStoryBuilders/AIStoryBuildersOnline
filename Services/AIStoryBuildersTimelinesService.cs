using AIStoryBuilders.AI;
using AIStoryBuilders.Models.LocalStorage;
using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using System.Text.Json.Serialization;

namespace AIStoryBuilders.Model
{
    public class AIStoryBuildersTimelinesService
    {
        const string PropertyTypeName = "AIStoryBuildersTimelines";

        // Properties
        public List<Timelines> Timelines { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public AIStoryBuildersTimelinesService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        // Load the database
        public async Task LoadAIStoryBuildersTimelinesAsync(string paramStoryName)
        {
            List<Timelines> AIStoryBuildersTimelines = await localStorage.GetItemAsync<List<Timelines>>($"{paramStoryName}|{PropertyTypeName}");

            if (AIStoryBuildersTimelines == null)
            {
                // Create a new instance of the AIStoryBuildersTimelines
                AIStoryBuildersTimelines = new List<Timelines>();

                Timelines = AIStoryBuildersTimelines;

                await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", Timelines);
            }

            if (Timelines == null)
            {
                Timelines = new List<Timelines>();
            }

            Timelines = AIStoryBuildersTimelines;
        }

        // Save the database

        public async Task SaveDatabaseAsync(string paramStoryName, List<Timelines> paramTimelines)
        {
            await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", paramTimelines);

            // Update the properties
            Timelines = paramTimelines;
        }

        // Add a new Timeline
        public async Task AddTimelineAsync(string paramStoryName, Timelines paramTimeline)
        {
            await LoadAIStoryBuildersTimelinesAsync(paramStoryName);

            Timelines.Add(paramTimeline);

            await SaveDatabaseAsync(paramStoryName, Timelines);
        }

        // Update a Timeline
        public async Task UpdateTimelineAsync(string paramStoryName, Timelines paramTimeline)
        {
            await LoadAIStoryBuildersTimelinesAsync(paramStoryName);

            var Timeline = Timelines.Where(x => x.name == paramTimeline.name).FirstOrDefault();

            if (Timeline != null)
            {
                Timelines.Remove(Timeline);
                Timelines.Add(paramTimeline);
                await SaveDatabaseAsync(paramStoryName, Timelines);
            }
        }

        // Delete a Timeline
        public async Task DeleteTimelineAsync(string paramStoryName, Timelines paramTimeline)
        {
            await LoadAIStoryBuildersTimelinesAsync(paramStoryName);

            var Timeline = Timelines.Where(x => x.name == paramTimeline.name).FirstOrDefault();

            if (Timeline != null)
            {
                Timelines.Remove(Timeline);
                await SaveDatabaseAsync(paramStoryName, Timelines);
            }
        }

        // Delete all Timelines
        public async Task DeleteAllTimelinesAsync(string paramStoryName)
        {
            await localStorage.RemoveItemAsync($"{paramStoryName}|{PropertyTypeName}");
        }

        // Convert 
        public Timelines ConvertTimelineToTimelines(Models.Timeline paramTimeline)
        {
            Timelines Timelines = new Timelines();

            Timelines.name = paramTimeline.TimelineName;
            Timelines.description = paramTimeline.TimelineDescription;
            Timelines.StartDate = OrchestratorMethods.ConvertDateToLongDateString(paramTimeline.StartDate);
            Timelines.StopDate = OrchestratorMethods.ConvertDateToLongDateString(paramTimeline.StopDate);

            return Timelines;
        }
    }
}