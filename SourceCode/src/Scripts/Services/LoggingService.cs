using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NyuBot {
	public class LoggingService {
		
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;

		private string _logDirectory { get; }
		private string _logFile { get { return Path.Combine(this._logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.log"); } }

		// DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
		public LoggingService(DiscordSocketClient discord, CommandService commands) {
			var now = DateTime.UtcNow;
			this._logDirectory = Path.Combine(AppContext.BaseDirectory, "logs", now.Year.ToString(), now.Month.ToString("00"));

			this._discord = discord;
			this._commands = commands;

			this._discord.Log += this.OnLogAsync;
			this._commands.Log += this.OnLogAsync;
		}


	
		private Task OnLogAsync(LogMessage msg) {
			if (!Directory.Exists(this._logDirectory)) Directory.CreateDirectory(this._logDirectory);
			if (!File.Exists(this._logFile)) File.Create(this._logFile).Dispose(); // Create today's log file if it doesn't exist

			string logText = $"{DateTime.UtcNow.ToString("hh:mm:ss tt")} [{msg.Severity}] {msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";
			File.AppendAllText(this._logFile, logText + "\n"); // Write the log text to a file

			return Console.Out.WriteLineAsync(logText); // Write the log text to the console
		}
		
		
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

	}
}
