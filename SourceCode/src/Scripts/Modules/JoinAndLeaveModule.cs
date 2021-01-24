using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SimpleJSON;

namespace NyuBot.Modules {
	public class JoinAndLeaveModule : ModuleBase<SocketCommandContext> {

		[Command("setjoinchannel")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetJoinAndLeaveChannel(SocketTextChannel textChannel) {
			if (textChannel == null) return;
			var path = $"GuildSettings/{this.Context.Guild.Id}";
			var jsonNode = await JsonCache.LoadJsonAsync(path);
			if (jsonNode == null) {
				jsonNode = new JSONObject();
			}
			jsonNode["joinAndLeaveChannelId"] = textChannel.Id.ToString();
			await JsonCache.SaveJsonAsync(path, jsonNode);

			var embed = new EmbedBuilder {
				Title = $"Set Join and Leave channel to",
				Description = textChannel.Mention
			};
			await  this.ReplyAsync("", false, embed.Build());
		}

	}
}
