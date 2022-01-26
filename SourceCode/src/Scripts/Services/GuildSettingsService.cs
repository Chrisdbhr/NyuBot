using System;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using NyuBot.Models;

namespace NyuBot {
	public class GuildSettingsService {
		
		public const string PATH_PREFIX = "GuildSettings/";
		
		private readonly DiscordSocketClient _discord;
		private readonly LoggingService _log;
		private readonly Random _rand = new();

		public GuildSettingsService(DiscordSocketClient discord) { // cant access Log service here
			this._discord = discord;

			this._discord.UserJoined += async user => {
				await this.UserJoined(user);
			};
			
			this._discord.UserLeft += this.UserLeft;
			this._discord.UserBanned += this.UserBanned;
			
		}


		#region <<---------- Get Guild Settings ---------->>
		
		public DGuildSettingsModel GetGuildSettings(ulong guildId) {
			var path = $"{PATH_PREFIX}{guildId}";
			return JsonCache.LoadFromJson<DGuildSettingsModel>(path) ?? new DGuildSettingsModel();
		}
		
		#endregion <<---------- Get Guild Settings ---------->>
		
		private async Task UserJoined(SocketGuildUser socketGuildUser) {
			await this._log.Info($"{socketGuildUser.Username} entrou no servidor {socketGuildUser.Guild.Name}");

			var guild = socketGuildUser.Guild;
			var guildSettings = GetGuildSettings(guild.Id);
			var channelId = guildSettings.JoinChannelId;
			if (channelId == null) return;
			
			var channel = guild.GetTextChannel(channelId.Value);
			if (channel == null) return;

			var msgText = socketGuildUser.IsBot ? "Ah não mais um bot aqui 😭" : $"Temos uma nova pessoinha no servidor, digam **oi** para {socketGuildUser.Mention}!";
			await channel.SendMessageAsync(msgText);
		}
		
		
		private async Task UserBanned(SocketUser socketUser, SocketGuild socketGuild) {
			await this.UserLeavedGuild(socketUser, socketGuild, " saiu do servidor...");
		}	
		
		private async Task UserLeft(SocketGuild socketGuild, SocketUser socketUser) {
			await this.UserLeavedGuild(socketUser, socketGuild, " saiu do servidor.");
		}
		
		private async Task UserLeavedGuild(SocketUser socketUser, SocketGuild socketGuild, string sufixMsg) {
			var guild = socketGuild;
			var guildSettings = GetGuildSettings(guild.Id);
			var channelId = guildSettings.JoinChannelId;
			if (channelId == null) return;
			
			var channel = guild.GetTextChannel(channelId.Value);
			if (channel == null) return;
			
			var jsonArray = JsonCache.LoadFromJson<JArray>("Answers/UserLeave");
			string customAnswer = null;
			if (jsonArray != null) {
				customAnswer = jsonArray[this._rand.Next(0, jsonArray.Count)].Value<string>();
			}
			
			var embed = new EmbedBuilder {
				Description = $"Temos {socketGuild.MemberCount} membros agora.",
				Color = Color.Red
			};
			
			var title = new StringBuilder();
			title.Append($"{socketUser.Username}#{socketUser.DiscriminatorValue:0000}");
			title.Append($"{sufixMsg}");

			embed.Title = title.ToString();
			
			// just leaved guild
			if (socketUser is SocketGuildUser socketGuildUser) {
				title.Append($"{(socketGuildUser.Nickname != null ? $" ({socketGuildUser.Nickname})" : null)}");

				if (socketGuildUser.JoinedAt.HasValue) {
					embed.Footer = new EmbedFooterBuilder {
						Text = $"Membro desde {socketGuildUser.JoinedAt.Value.ToString("dd/MM/yy hh tt")}"
					};
				}
			}
			else {
				// was banned
				var guildOwner = socketGuild.Owner;
				await guildOwner.SendMessageAsync($"Banido do servidor {socketGuild.Name}", false, embed.Build());
			}
			
			var sendMsg = await channel.SendMessageAsync(socketUser.IsBot ? "Era um bot" : customAnswer, false, embed.Build());
			await sendMsg.AddReactionAsync(new Emoji(":regional_indicator_f:"));
		}


	}
}
