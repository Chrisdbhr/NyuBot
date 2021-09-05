using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NyuBot.Models;

namespace NyuBot.Modules {
	public class GuildSettingsModule : ModuleBase<SocketCommandContext> {

		private GuildSettingsService _service;
		
		public GuildSettingsModule(GuildSettingsService service) {
			this._service = service;
		}
	
		

		#region <<---------- User Leave and Join ---------->>
		
		[Command("setjoinchannel")]
		[Discord.Commands.Summary("Set channel to notify when user joins guild.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetJoinChannel(SocketTextChannel textChannel) {
			var path = $"{GuildSettingsService.PATH_PREFIX}{this.Context.Guild.Id}";
			var guildSettings = JsonCache.LoadFromJson<DGuildSettingsModel>(path) ?? new DGuildSettingsModel();

			guildSettings.JoinChannelId = textChannel?.Id;
			JsonCache.SaveToJson(path, guildSettings);

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
		[Discord.Commands.Summary("Set channel to notify when user leaves guild.")]
		[RequireUserPermission(GuildPermission.Administrator)]
		public async Task SetLeaveChannel(SocketTextChannel textChannel) {
			var path = $"{GuildSettingsService.PATH_PREFIX}{this.Context.Guild.Id}";
			var guildSettings = JsonCache.LoadFromJson<DGuildSettingsModel>(path) ?? new DGuildSettingsModel();
			
			guildSettings.LeaveChannelId = textChannel?.Id;
			JsonCache.SaveToJson(path, guildSettings);

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

		#endregion <<---------- User Leave and Join ---------->>

		
		
		
		
	}
}
