using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NyuBot.HungerGames;
using NyuBot.Twitter;

namespace NyuBot {
    public class Startup : IDisposable {

        private ServiceProvider _serviceProvider;
        public IConfigurationRoot Configuration { get; }

        public Startup(string[] args) {
            var builder = new ConfigurationBuilder()        // Create a new instance of the config builder
                          .SetBasePath(AppContext.BaseDirectory)      // Specify the default location for the config file
                          .AddYamlFile("_config.yml");                // Add this (yaml encoded) file to the configuration
            this.Configuration = builder.Build();           // Build the configuration
        }

        public static async Task RunAsync(string[] args) {
            var startup = new Startup(args);
            await startup.RunAsync();
        }

        public async Task RunAsync() {
            var services = new ServiceCollection();             // Create a new instance of a service collection
            this.ConfigureServices(services);

            this._serviceProvider = services.BuildServiceProvider();     // Build the service provider
            
            this._serviceProvider.GetRequiredService<GuildSettingsService>();
            this._serviceProvider.GetRequiredService<LoggingService>();
            this._serviceProvider.GetRequiredService<CommandHandler>();
            this._serviceProvider.GetRequiredService<AudioService>(); 
            this._serviceProvider.GetRequiredService<ChatService>(); 
            this._serviceProvider.GetRequiredService<HungerGameService>();
            this._serviceProvider.GetRequiredService<VoiceService>();
            this._serviceProvider.GetRequiredService<ExchangeService>();
            this._serviceProvider.GetRequiredService<BackupService>();
            this._serviceProvider.GetRequiredService<ModeratorService>();
            this._serviceProvider.GetRequiredService<AutoReactService>();
            this._serviceProvider.GetRequiredService<TwitterService>();
            this._serviceProvider.GetRequiredService<IAudioService>();
            this._serviceProvider.GetRequiredService<DatabaseService>();
            this._serviceProvider.GetRequiredService<SleepService>();
            
            await this._serviceProvider.GetRequiredService<StartupService>().StartAsync();       // Start the startup service
            await Task.Delay(-1);                               // Keep the program alive
        }

        private void ConfigureServices(IServiceCollection services) {
            services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {                                       // Add discord to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                MessageCacheSize = 1000             // Cache 1,000 messages per channel
            }))
            .AddSingleton(new CommandService(new CommandServiceConfig
            {                                       // Add the command service to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                DefaultRunMode = RunMode.Async,     // Force all commands to run async by default
            }))
            .AddSingleton<GuildSettingsService>()
            .AddSingleton<LoggingService>() 
            .AddSingleton<CommandHandler>() 
            .AddSingleton<StartupService>() 
            .AddSingleton<Random>()         
            .AddSingleton<AudioService>()   
            .AddSingleton<VoiceService>()   
            .AddSingleton<ExchangeService>()
            .AddSingleton<ChatService>()    
            .AddSingleton<HungerGameService>()
            .AddSingleton<WeatherService>()
            .AddSingleton<BackupService>()
            .AddSingleton<ModeratorService>()
            .AddSingleton<AutoReactService>()
            .AddSingleton<TwitterService>()
            .AddSingleton<IAudioService, LavalinkNode>()
            .AddSingleton<IDiscordClientWrapper, DiscordClientWrapper>()
            .AddSingleton(new LavalinkNodeOptions {
                AllowResuming = true,
                Password = "",
                RestUri = "http://localhost:8080",
                DisconnectOnStop = false
            })
            .AddSingleton<DatabaseService>()
            .AddSingleton<SleepService>()
            .AddSingleton(this.Configuration);      // Add the configuration to the collection
        }

        public void Dispose() {
            this._serviceProvider.Dispose();
        }
    }
}
