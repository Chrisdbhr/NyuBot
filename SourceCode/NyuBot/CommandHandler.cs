using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace NyuBot
{
   public class CommandHandler
   {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;

        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands)
        {
            _commands = commands;
            _client = client;
        }
        
        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), 
                                            services: null);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix('!', ref argPos) || 
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(
                context: context, 
                argPos: argPos,
                services: null);
        }
    }
   
   // Create a module with no prefix
   public class InfoModule : ModuleBase<SocketCommandContext>
   {
       // ~say hello world -> hello world
       [Command("say")]
       [Summary("Echoes a message.")]
       public Task SayAsync([Remainder] [Summary("The text to echo")] string echo)
           => ReplyAsync(echo);
		
       // ReplyAsync is a method on ModuleBase 
   }

// Create a module with the 'sample' prefix
   [Group("sample")]
   public class SampleModule : ModuleBase<SocketCommandContext>
   {
       // ~sample square 20 -> 400
       [Command("square")]
       [Summary("Squares a number.")]
       public async Task SquareAsync(
           [Summary("The number to square.")] 
           int num)
       {
           // We can also access the channel from the Command Context.
           await Context.Channel.SendMessageAsync($"{num}^2 = {Math.Pow(num, 2)}");
       }

       // ~sample userinfo --> foxbot#0282
       // ~sample userinfo @Khionu --> Khionu#8708
       // ~sample userinfo Khionu#8708 --> Khionu#8708
       // ~sample userinfo Khionu --> Khionu#8708
       // ~sample userinfo 96642168176807936 --> Khionu#8708
       // ~sample whois 96642168176807936 --> Khionu#8708
       [Command("userinfo")]
       [Summary
           ("Returns info about the current user, or the user parameter, if one passed.")]
       [Alias("user", "whois")]
       public async Task UserInfoAsync(
           [Summary("The (optional) user to get info from")]
           SocketUser user = null)
       {
           var userInfo = user ?? Context.Client.CurrentUser;
           await ReplyAsync($"{userInfo.Username}#{userInfo.Discriminator}");
       }
   }
}