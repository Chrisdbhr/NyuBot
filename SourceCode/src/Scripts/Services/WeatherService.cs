using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace NyuBot {
	public class WeatherService {
		
		private readonly DiscordSocketClient _discord;
		private readonly IConfigurationRoot _config;

		
		
		
		public WeatherService(DiscordSocketClient discord, IConfigurationRoot config) {
			this._discord = discord;
			this._config = config;
		}
		
	}
}
