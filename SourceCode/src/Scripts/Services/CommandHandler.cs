using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using Discord;

namespace NyuBot {
    public class CommandHandler {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;

        // DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public CommandHandler(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider provider) {
            this._discord = discord;
            this._commands = commands;
            this._config = config;
            this._provider = provider;

            this._discord.MessageReceived += this.OnMessageReceivedAsync;
        }
        
        private async Task OnMessageReceivedAsync(SocketMessage s) {
            if (!(s is SocketUserMessage msg)) return;
            if (msg.Author.IsBot) return;
            if (msg.Author.Id == this._discord.CurrentUser.Id) return;     // Ignore self when checking commands
            
            var context = new SocketCommandContext(this._discord, msg);     // Create the command context

            int argPos = 0;     // Check if the message has a valid command prefix
            if (msg.HasStringPrefix(this._config["prefix"], ref argPos) || msg.HasMentionPrefix(this._discord.CurrentUser, ref argPos))
            {
                var result = await this._commands.ExecuteAsync(context, argPos, this._provider);     // Execute the command

                if (!result.IsSuccess) { // If not successful, reply with the error.
                    //await context.Channel.SendMessageAsync(result.ToString());
                    await msg.AddReactionAsync(new Emoji("❔"));
                }     
            }
        }
    }
}
