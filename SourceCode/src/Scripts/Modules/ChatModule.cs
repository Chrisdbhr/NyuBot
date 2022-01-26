using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NyuBot.Extensions;

namespace NyuBot.Modules {
	public class ChatModule : ModuleBase<SocketCommandContext> {

		private const int DEFAULT_MESSAGES_AMOUNT = 500;
		private readonly ChatService _service;



		public ChatModule(ChatService service) {
			this._service = service;
		}
		
		
		
		
		[Command("randomphrase"), Alias("rf")]
		[Summary("Get a random motivacional phrase")]
		public async Task GetRandomPhrase() {
			var phrase = await this._service.GetRandomMotivationPhrase();
			if (string.IsNullOrEmpty(phrase)) return;
			var embed = new EmbedBuilder {
				Title = "Frase",
				Description = $"*\"{phrase}\"*",
				Footer = new EmbedFooterBuilder {
					Text = "pensador.com"
				}
			};
			
			await this.ReplyAsync(string.Empty, false, embed.Build());
			await this.Context.Message.DeleteAsync();
		}


		[Command("remember"), Alias("rm")]
		[Summary("Get a random message from a user in all chats")]
		[RequireUserPermission(GuildPermission.SendMessages)]
		public async Task GetRandomUserMessage() {
			await this._service.GetAndRepplyRememberMessage(this.Context.Message, DEFAULT_MESSAGES_AMOUNT, false);
			if (this.Context.Message != null) {
				await this.Context.Message.DeleteAsync();
			}
		}

		
		[Command("remember"), Alias("rm")]
		[Summary("Get a random message from a user in all chats, adm version")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task GetRandomUserMessage(int ammount) {
			await this._service.GetAndRepplyRememberMessage(this.Context.Message, ammount, true);
			if (this.Context.Message != null) {
				await this.Context.Message.DeleteAsync();
			}
		}

		[Command("updatestatus")]
		[Summary("Update bot self status")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task UpdateSelfStatus() {
			await this._service.UpdateSelfStatusAsync();
		}


	}
}
