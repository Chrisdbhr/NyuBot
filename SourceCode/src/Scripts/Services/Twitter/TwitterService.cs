using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace NyuBot.Twitter {
	public class TwitterService {
		
		private readonly DiscordSocketClient _discord;
		private readonly IConfigurationRoot _config;
		private readonly LoggingService _log;
		private readonly TwitterApi _twitterApi;

		public TwitterService(DiscordSocketClient discord, IConfigurationRoot config, LoggingService loggingService) {
			this._config = config;
			this._discord = discord;
			this._log = loggingService;
			
			this._twitterApi = new TwitterApi(
				this._config["twitter:api-key"],
				this._config["twitter:api-key-secret"],
				this._config["twitter:access-token"],
				this._config["twitter:access-token-secret"]
			);
			
			this._discord.MessageReceived += this.DiscordOnMessageReceived;
		}

		private async Task DiscordOnMessageReceived(SocketMessage msg) {
			if (msg.Source != MessageSource.User) return;
			ulong twitterAutoPostChannelId = ulong.Parse(this._config["twitter:auto-post-channel-id"]);

			if (msg.Channel.Id != twitterAutoPostChannelId) return;

			var response = await this._twitterApi.Tweet(msg.Content);
			
			await this._log.Warning($"Auto post on Twitter, response: {response}");
		}

	}
}
