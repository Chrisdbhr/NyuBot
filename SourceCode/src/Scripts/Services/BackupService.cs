using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace NyuBot {
	public class BackupService {
		
		private readonly DiscordSocketClient _discord;
		private readonly GuildSettingsService _guildSettings;
		private readonly LoggingService _log;

		
		
		
		public BackupService(DiscordSocketClient discord, LoggingService loggingService, GuildSettingsService guildSettings) {
			this._discord = discord;
			this._log = loggingService;
			this._guildSettings = guildSettings;

			this._discord.MessageReceived += this.MessageWithAttachmentReceived;
		}
		
		private async Task MessageWithAttachmentReceived(SocketMessage socketMessage) {
			if (socketMessage.Author.IsBot || socketMessage.Author.IsWebhook) return;
			if (socketMessage.Channel is not SocketGuildChannel guildChannel) return;

			var guildId = guildChannel.Guild.Id;
			
			// check for attachment backup channel on guild
			var guildSettings = this._guildSettings.GetGuildSettings(guildId);

			if (guildSettings == null || guildSettings.AttachmentsBackupChannelId == null) return;

			var guild = this._discord.GetGuild(guildId);
			if (guild == null) return;
			var channel = guild.GetTextChannel(guildSettings.AttachmentsBackupChannelId.Value);
			if (channel == null) return;

			var attachmentsPaths = await BackupAttachments(socketMessage.Attachments);
			
			var embed = new EmbedBuilder {
				Title = $"From {socketMessage.Author.Username}",
				Fields = {
					new EmbedFieldBuilder {
						Name = "Author",
						Value = socketMessage.Author.Mention
					},
					new EmbedFieldBuilder {
						Name = "Channel",
						Value = socketMessage.Channel.Name
					}
				}
			};

			foreach (var path in attachmentsPaths) {
				var msg = await channel.SendFileAsync(path, socketMessage.Content, false, embed.Build());
				if (msg == null) {
					await this._log.Error($"Could not backup file '{path}' on text channel '{channel.Name}'.");
				}
				else {
					File.Delete(path);
				}
			}
		}

		/// <summary>
		/// Returns list of attachments backup paths.
		/// </summary>
		private async Task<string[]> BackupAttachments(IReadOnlyCollection<Attachment> Attachments) {
			var paths = new List<string>();
			
			foreach (var attachment in Attachments) {
				var dateTime = DateTime.UtcNow;
				
				var targetDir = "temp/";
				Directory.CreateDirectory(targetDir);
				
				var fileName = $"{dateTime.Year}-{dateTime.Month:00}-{dateTime.Day:00}_{dateTime.Hour:00}-{dateTime.Minute:00}_{attachment.Filename}";
				
				var filePathAndName = Path.Combine(targetDir, fileName);
				if (File.Exists(filePathAndName)) {
					filePathAndName = $"{dateTime.Ticks}_{filePathAndName}";
				}
				using (var client = new WebClient()) {
					await client.DownloadFileTaskAsync(new Uri(attachment.Url), filePathAndName);
				}
				
				paths.Add(filePathAndName);

			}

			return paths.ToArray();
		}
		
	}
}
