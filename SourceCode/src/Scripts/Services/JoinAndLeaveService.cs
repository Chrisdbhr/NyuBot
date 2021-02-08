using System;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace NyuBot {
	public class JoinAndLeaveService {
		
		private readonly DiscordSocketClient _discord;
		private readonly LoggingService _log;
		private readonly Random _rand = new Random();

		public JoinAndLeaveService(DiscordSocketClient discord, LoggingService loggingService) {
			this._discord = discord;
			this._log = loggingService;

			this._discord.UserJoined += async user => {
				await this.UserJoined(user);
			};
			
			this._discord.UserLeft += this.UserLeft;
			this._discord.UserBanned += this.UserBanned;
			
		}
		
		
		private async Task UserJoined(SocketGuildUser socketGuildUser) {
			await this._log.Info($"{socketGuildUser.Username} entrou no servidor {socketGuildUser.Guild.Name}");
			
			var guild = socketGuildUser.Guild;
			var jsonNode = await JsonCache.LoadValueAsync($"GuildSettings/{guild.Id}", "joinChannelId");
			if (jsonNode == null) return;
			var channelId = jsonNode.Value;
			if (string.IsNullOrEmpty(channelId)) return;
			
			var channel = guild.GetTextChannel(ulong.Parse(channelId));
			if (channel == null) return;
			
			await channel.SendMessageAsync($"Temos uma nova pessoinha no servidor, digam **oi** para {socketGuildUser.Mention}!");
		}
		
		
		private async Task UserBanned(SocketUser socketUser, SocketGuild socketGuild) {
			await this.UserLeavedGuild(socketUser, socketGuild, " saiu do servidor...");
		}	
		
		private async Task UserLeft(SocketGuildUser socketGuildUser) {
			await this.UserLeavedGuild(socketGuildUser, socketGuildUser.Guild, " saiu do servidor.");
		}
		
		private async Task UserLeavedGuild(SocketUser socketUser, SocketGuild socketGuild, string sufixMsg) {
			var jsonNode = await JsonCache.LoadValueAsync($"GuildSettings/{socketGuild.Id}", "leaveChannelId");
			if (jsonNode == null) return;
			var channelId = jsonNode.Value;
			if (string.IsNullOrEmpty(channelId)) return;

			var channel = socketGuild.GetTextChannel(ulong.Parse(channelId));
			if (channel == null) return;

			var jsonArray = (await JsonCache.LoadValueAsync("Answers/UserLeave", "data")).AsArray;
			string customAnswer = null;
			if (jsonArray != null) {
				customAnswer = jsonArray[this._rand.Next(0, jsonArray.Count)].Value;
			}
			
			var embed = new EmbedBuilder {
				Description = $"Temos {socketGuild.MemberCount} membros agora.",
				Color = Color.Red
			};
			
			var title = new StringBuilder();
			title.Append($"{socketUser.Username}#{socketUser.DiscriminatorValue}");
			title.Append($"{sufixMsg}");

			embed.Title = title.ToString();
			
			var sendMsg = await channel.SendMessageAsync(customAnswer, false, embed.Build());
			
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
			
			await sendMsg.AddReactionAsync(new Emoji(":regional_indicator_f:"));
		}


	}
}
