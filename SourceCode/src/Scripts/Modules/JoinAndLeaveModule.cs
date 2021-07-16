using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using SimpleJSON;

namespace NyuBot.Modules {
	public class JoinAndLeaveModule : ModuleBase<SocketCommandContext> {
		
		[Command("setjoinchannel")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetJoinChannel(SocketTextChannel textChannel) {
			var path = $"GuildSettings/{this.Context.Guild.Id}";
			var jsonNode = await JsonCache.LoadJsonAsync(path);
			if (jsonNode == null) {
				jsonNode = new JSONObject();
			}
			
			jsonNode["joinChannelId"] = textChannel?.Id.ToString() ?? string.Empty;
			await JsonCache.SaveToJson(path, jsonNode);


			var embed = new EmbedBuilder();
			if (textChannel != null) {
				embed.Title = $"Join channel set to";
				embed.Description = textChannel.Mention;
			}
			else {
				embed.Title = $"Disabled join guild messages";
			}

			await  this.ReplyAsync("", false, embed.Build());
		}
		
		[Command("setleavechannel")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetLeaveChannel(SocketTextChannel textChannel) {
			var path = $"GuildSettings/{this.Context.Guild.Id}";
			var jsonNode = await JsonCache.LoadJsonAsync(path);
			if (jsonNode == null) {
				jsonNode = new JSONObject();
			}
			
			jsonNode["leaveChannelId"] = textChannel?.Id.ToString() ?? string.Empty;
			await JsonCache.SaveToJson(path, jsonNode);


			var embed = new EmbedBuilder();
			if (textChannel != null) {
				embed.Title = $"Leave channel set to";
				embed.Description = textChannel.Mention;
			}
			else {
				embed.Title = $"Disabled Leave guild messages";
			}

			await  this.ReplyAsync("", false, embed.Build());
		}

	}
}
