using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace NyuBot {
    public class StartupService {
        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;

        // DiscordSocketClient, CommandService, and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands, IConfigurationRoot config) {
            this._provider = provider;
            this._config = config;
            this._discord = discord;
            this._commands = commands;
        }

        public async Task StartAsync() {
            string discordToken = this._config["tokens:discord"];     // Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken))
                throw new Exception("Please enter your bot's token into the `_configuration.json` file found in the applications root directory.");

            await this._discord.LoginAsync(TokenType.Bot, discordToken);     // Login to discord
            await this._discord.StartAsync();                                // Connect to the websocket

            await this._commands.AddModulesAsync(Assembly.GetEntryAssembly(), this._provider);     // Load commands and modules into the command service
        }
    }
}
