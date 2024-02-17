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
            AppMetadata appMetadata = new AppMetadata() { Version = "01.01.10" };
            builder.Services.AddSingleton(appMetadata);

            builder.Services.AddScoped<LogService>();
            builder.Services.AddScoped<SettingsService>();
            builder.Services.AddScoped<DatabaseService>();
            builder.Services.AddScoped<OrchestratorMethods>();
            builder.Services.AddScoped<AIStoryBuildersService>();

            // Radzen
            builder.Services.AddScoped<DialogService>();
            builder.Services.AddScoped<NotificationService>();
            builder.Services.AddScoped<TooltipService>();
            builder.Services.AddScoped<ContextMenuService>();

            // Local Storage
            builder.Services.AddBlazoredLocalStorage();

            await builder.Build().RunAsync();
        }
    }
}
