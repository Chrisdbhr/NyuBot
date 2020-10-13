using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NyuBot {
	public class LoggingService {
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;

		private string _logDirectory { get; }
		private string _logFile => Path.Combine(this._logDirectory, $"{DateTime.UtcNow.AddHours(-3).ToString("yyyy-MM-dd")}.txt");

		// DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
		public LoggingService(DiscordSocketClient discord, CommandService commands) {
			this._logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

			this._discord = discord;
			this._commands = commands;

			this._discord.Log += this.OnLogAsync;
			this._commands.Log += this.OnLogAsync;
		}

		private Task OnLogAsync(LogMessage msg) {
			if (!Directory.Exists(this._logDirectory)) // Create the log directory if it doesn't exist
				Directory.CreateDirectory(this._logDirectory);
			if (!File.Exists(this._logFile)) // Create today's log file if it doesn't exist
				File.Create(this._logFile).Dispose();

			string logText = $"{DateTime.UtcNow.AddHours(-3).ToString("hh:mm:ss")} [{msg.Severity}] {msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";
			File.AppendAllText(this._logFile, logText + "\n"); // Write the log text to a file

			return Console.Out.WriteLineAsync(logText); // Write the log text to the console
		}
	}
}
