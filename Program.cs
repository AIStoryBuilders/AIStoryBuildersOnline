using AIStoryBuilders.AI;
using AIStoryBuilders.Model;
using AIStoryBuilders.Models;
using AIStoryBuilders.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;

namespace AIStoryBuildersOnline
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            // Add services to the container.
            AppMetadata appMetadata = new AppMetadata() { Version = "01.02.20" };
            builder.Services.AddSingleton(appMetadata);

            builder.Services.AddScoped<LogService>();
            builder.Services.AddScoped<SettingsService>();
            builder.Services.AddScoped<DatabaseService>();
            builder.Services.AddScoped<OrchestratorMethods>();
            builder.Services.AddScoped<AIStoryBuildersService>();
            builder.Services.AddScoped<AIStoryBuildersStoryService>();
            builder.Services.AddScoped<AIStoryBuildersTempService>();

            // Radzen
            builder.Services.AddScoped<DialogService>();
            builder.Services.AddScoped<NotificationService>();
            builder.Services.AddScoped<TooltipService>();
            builder.Services.AddScoped<ContextMenuService>();

            // Local Storage
            builder.Services.AddBlazoredLocalStorage();

            // Load Default files
            var folderPath = "";
            var filePath = "";

            // AIStoryBuilders Directory
            folderPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/AIStoryBuilders";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // AIStoryBuildersLog.csv
            filePath = Path.Combine(folderPath, "AIStoryBuildersLog.csv");

            if (!File.Exists(filePath))
            {
                using (var streamWriter = new StreamWriter(filePath))
                {
                    streamWriter.WriteLine("Application started at " + DateTime.Now + " [" + DateTime.Now.Ticks.ToString() + "]");
                }
            }
            else
            {
                // File already exists
                string[] AIStoryBuildersLog;

                // Open the file to get existing content
                using (var file = new System.IO.StreamReader(filePath))
                {
                    AIStoryBuildersLog = file.ReadToEnd().Split('\n');

                    if (AIStoryBuildersLog[AIStoryBuildersLog.Length - 1].Trim() == "")
                    {
                        AIStoryBuildersLog = AIStoryBuildersLog.Take(AIStoryBuildersLog.Length - 1).ToArray();
                    }
                }

                // Append the text to csv file
                using (var streamWriter = new StreamWriter(filePath))
                {
                    streamWriter.WriteLine(string.Join("\n", "Application started at " + DateTime.Now));
                    streamWriter.WriteLine(string.Join("\n", AIStoryBuildersLog));
                }
            }

            await builder.Build().RunAsync();
        }
    }
}
