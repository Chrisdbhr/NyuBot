using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NyuBot.Extensions;

namespace NyuBot {
	public class LoggingService {
		
		#region <<---------- Properties ---------->>
		
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;
		private readonly GuildSettingsService _guildSettings;

		private string _logDirectory { get; }
		public string LogFile { get { return Path.Combine(this._logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.log"); } }

		#endregion <<---------- Properties ---------->>


		

		#region <<---------- Initializers ---------->>
		
		public LoggingService(DiscordSocketClient discord, CommandService commands, GuildSettingsService guildSettings) {
			var now = DateTime.UtcNow;
			this._logDirectory = Path.Combine(AppContext.BaseDirectory, "logs", now.Year.ToString("00"), now.Month.ToString("00"));

			this._discord = discord;
			this._commands = commands;
			this._guildSettings = guildSettings;

			this._discord.Log += this.OnLogAsync;
			this._commands.Log += this.OnLogAsync;
		}

		#endregion <<---------- Initializers ---------->>


		
	
		private async Task OnLogAsync(LogMessage msg) {
			if (!Directory.Exists(this._logDirectory)) Directory.CreateDirectory(this._logDirectory);
			if (!File.Exists(this.LogFile)) File.Create(this.LogFile).Dispose();// Create today's log file if it doesn't exist

			string logText = $"{DateTime.UtcNow.ToString("hh:mm:ss tt")} [{msg.Severity}] {msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";
			await File.AppendAllTextAsync(this.LogFile, logText + "\n"); // Write the log text to a file

			this.LogOnDiscordChannel(msg).CAwait();
			
			await Console.Out.WriteLineAsync(logText); // Write the log text to the console
		}

		private async Task LogOnDiscordChannel(LogMessage msg) {
			try {
				//if (msg.Severity > LogSeverity.Error) return;
				if (msg.Severity > LogSeverity.Warning) return;
				foreach (var guild in this._discord.Guilds) {
					//var id = await JsonCache.LoadValueAsync($"GuildSettings/{guild.Id.ToString()}", "channel-bot-logs-id");
					var id = this._guildSettings.GetGuildSettings(guild.Id).BotLogsTextChannelId;
					if (id == null) continue;
					var textChannel = guild.GetTextChannel(id.Value);
					if (textChannel == null) continue;
					
					var embed = new EmbedBuilder {
						Color = this.GetColorByLogSeverity(msg.Severity),
						Title = msg.Severity.ToString(),
						Description = msg.ToString().SubstringSafe(1024)
					};

					await textChannel.SendMessageAsync(string.Empty, false, embed.Build());
				}
			} catch (Exception e) {
				await Console.Out.WriteLineAsync("Exception trying to log message to channel:" + e.Message); // Write the log text to the console
			}
		}

		private Color GetColorByLogSeverity(LogSeverity severity) {
			switch (severity) {
				case LogSeverity.Critical:
					return Color.DarkRed;
				case LogSeverity.Error:
					return Color.Red;
				case LogSeverity.Warning:
					return Color.Gold;
				case LogSeverity.Info:
					return Color.Blue;
				case LogSeverity.Verbose:
					return Color.LighterGrey;
				default:
					return Color.LightGrey;
			}
		}

		#region <<---------- Log Categories ---------->>
		
		public async Task Critical(string msg, [CallerMemberName] string callingMethod = null) {
			await this.OnLogAsync(new LogMessage(LogSeverity.Critical, callingMethod, msg));
		}
		public async Task Error(string msg, [CallerMemberName] string callingMethod = null) {
			await this.OnLogAsync(new LogMessage(LogSeverity.Error, callingMethod, msg));
		}
		public async Task Warning(string msg, [CallerMemberName] string callingMethod = null) {
			await this.OnLogAsync(new LogMessage(LogSeverity.Warning, callingMethod, msg));
		}
		public async Task Info(string msg, [CallerMemberName] string callingMethod = null) {
			await this.OnLogAsync(new LogMessage(LogSeverity.Info, callingMethod, msg));
		}
		public async Task Debug(string msg, [CallerMemberName] string callingMethod = null) {
			await this.OnLogAsync(new LogMessage(LogSeverity.Debug, callingMethod, msg));
		}

		#endregion <<---------- Log Categories ---------->>

	}
}
