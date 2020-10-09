using System;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NyuBot.Extensions;

namespace NyuBot.Modules {
	public class UserModule : ModuleBase<SocketCommandContext> {


		[Command("profilepicture"), Alias("pp")]
		[Summary("Get user profile picture")]
		public async Task GetUserProfilePicture(SocketGuildUser user) {
			var userAvatarUrl = user.GetAvatarUrlSafe();
			
			var uri = new Uri(userAvatarUrl);
			var urlString = userAvatarUrl.Replace(uri.Query, string.Empty);
			
			var embed = new EmbedBuilder();
			embed.Title = $"Foto de perfil de {user.GetNameSafe()}";
			embed.ImageUrl = urlString;

			await this.ReplyAsync("", false, embed.Build());
			await this.Context.Message.DeleteAsync();
		}
		
	}
}
