using AIStoryBuilders.Models.LocalStorage;
using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using System.Text.Json.Serialization;

namespace AIStoryBuilders.Model
{
    public class AIStoryBuildersLocationsService
    {
        const string PropertyTypeName = "AIStoryBuildersLocations";

        // Properties
        public List<Locations> Locations { get; set; }

        private ILocalStorageService localStorage;

        // Constructor
        public AIStoryBuildersLocationsService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        // Load the database
        public async Task LoadAIStoryBuildersLocationsAsync(string paramStoryName)
        {
            List<Locations> AIStoryBuildersLocations = await localStorage.GetItemAsync<List<Locations>>($"{paramStoryName}|{PropertyTypeName}");

            if (AIStoryBuildersLocations == null)
            {
                // Create a new instance of the AIStoryBuildersLocations
                AIStoryBuildersLocations = new List<Locations>();

                Locations = AIStoryBuildersLocations;

                await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", Locations);
            }

            if (Locations == null)
            {
                Locations = new List<Locations>();
            }

            Locations = AIStoryBuildersLocations;
        }

        // Save the database

        public async Task SaveDatabaseAsync(string paramStoryName, List<Locations> paramLocations)
        {
            await localStorage.SetItemAsync($"{paramStoryName}|{PropertyTypeName}", paramLocations);

            // Update the properties
            Locations = paramLocations;
        }

        // Add a new Location
        public async Task AddLocationAsync(string paramStoryName, Locations paramLocation)
        {
            await LoadAIStoryBuildersLocationsAsync(paramStoryName);

            Locations.Add(paramLocation);

            await SaveDatabaseAsync(paramStoryName, Locations);
        }

        // Update a Location
        public async Task UpdateLocationAsync(string paramStoryName, Locations paramLocation)
        {
            await LoadAIStoryBuildersLocationsAsync(paramStoryName);

            var Location = Locations.Where(x => x.name == paramLocation.name).FirstOrDefault();

            if (Location != null)
            {
                Locations.Remove(Location);
                Locations.Add(paramLocation);   
                await SaveDatabaseAsync(paramStoryName, Locations);
            }
        }

        // Delete a Location
        public async Task DeleteLocationAsync(string paramStoryName, Locations paramLocation)
        {
            await LoadAIStoryBuildersLocationsAsync(paramStoryName);

            var Location = Locations.Where(x => x.name == paramLocation.name).FirstOrDefault();

            if (Location != null)
            {
                Locations.Remove(Location);
                await SaveDatabaseAsync(paramStoryName, Locations);
            }
        }

        // Delete all Locations
        public async Task DeleteAllLocationsAsync(string paramStoryName)
        {
            await localStorage.RemoveItemAsync($"{paramStoryName}|{PropertyTypeName}");
        }
    }
}