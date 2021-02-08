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
            
            this._serviceProvider.GetRequiredService<LoggingService>();      // Start the logging service
            this._serviceProvider.GetRequiredService<CommandHandler>(); 		// Start the command handler service
            this._serviceProvider.GetRequiredService<AudioService>(); 		// Start the chat service handler
            this._serviceProvider.GetRequiredService<ChatService>(); 		// Start the chat service handler
            this._serviceProvider.GetRequiredService<HungerGameService>();
            this._serviceProvider.GetRequiredService<VoiceService>();
            this._serviceProvider.GetRequiredService<ExchangeService>();
            this._serviceProvider.GetRequiredService<JoinAndLeaveService>();
            this._serviceProvider.GetRequiredService<ModeratorService>();
            this._serviceProvider.GetRequiredService<AutoReactService>();
            this._serviceProvider.GetRequiredService<IAudioService>();
            this._serviceProvider.GetRequiredService<DatabaseService>();
            
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
            .AddSingleton<CommandHandler>()         // Add the command handler to the collection
            .AddSingleton<StartupService>()         // Add startupservice to the collection
            .AddSingleton<LoggingService>()         // Add loggingservice to the collection
            .AddSingleton<Random>()                 // Add random to the collection
            .AddSingleton<AudioService>()           // Add audio service to collection
            .AddSingleton<VoiceService>()           // Add audio service to collection
            .AddSingleton<ExchangeService>()           // Add audio service to collection
            .AddSingleton<ChatService>()            // Add chat service to collection
            .AddSingleton<HungerGameService>()
            .AddSingleton<WeatherService>()
            .AddSingleton<JoinAndLeaveService>()
            .AddSingleton<ModeratorService>()
            .AddSingleton<AutoReactService>()
            .AddSingleton<IAudioService, LavalinkNode>()
            .AddSingleton<IDiscordClientWrapper, DiscordClientWrapper>()
            .AddSingleton(new LavalinkNodeOptions {
                AllowResuming = true,
                Password = "",
                RestUri = "http://localhost:8080",
                DisconnectOnStop = false
            })
            .AddSingleton<DatabaseService>()
            .AddSingleton(this.Configuration);      // Add the configuration to the collection
        }

        public void Dispose() {
            this._serviceProvider.Dispose();
        }
    }
}
