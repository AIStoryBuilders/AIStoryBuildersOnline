using AIStoryBuildersOnline.Components.Pages;
using Blazored.LocalStorage;
using Newtonsoft.Json;
using OpenAI.Files;
using Radzen;

namespace AIStoryBuilders.Model
{
    public class Log
    {
        public List<string> colLogs { get; set; }
    }

    public class LogService
    {
        // Properties
        public Log Logs { get; set; }
        private ILocalStorageService localStorage;

        // Constructor
        public LogService(ILocalStorageService LocalStorage)
        {
            localStorage = LocalStorage;
        }

        public async Task LoadLogAsync()
        {
            Log AIStoryBuildersLog = await localStorage.GetItemAsync<Log>("AIStoryBuildersLog");

            if (AIStoryBuildersLog == null)
            {
                // Create a new instance of the AIStoryBuildersLog
                AIStoryBuildersLog = new Log();

                AIStoryBuildersLog.colLogs = new List<string>();

                await localStorage.SetItemAsync("AIStoryBuildersLog", AIStoryBuildersLog);
            }

            Logs = AIStoryBuildersLog;
        }

        public async Task WriteToLogAsync(string LogText)
        {
            await LoadLogAsync();

            // If log has more than 100 lines, keep only the recent 100 lines
            if (Logs.colLogs.Count > 100)
            {
                Logs.colLogs = Logs.colLogs.Take(1000).ToList();
            }

            // Add to the top of the list
            Logs.colLogs.Insert(0, LogText);

            await localStorage.SetItemAsync("AIStoryBuildersLog", Logs);
        }

        // Clear the log
        public async Task ClearLogAsync()
        {
            Logs.colLogs.Clear();

            await localStorage.SetItemAsync("AIStoryBuildersLog", Logs);
        }
    }
}