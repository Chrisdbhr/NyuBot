using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using NyuBot.Extensions;
using RestSharp;
using SimpleJSON;

namespace NyuBot.Modules {
	public class WeatherModule : ModuleBase<SocketCommandContext> {
		
		private readonly WeatherService _service;
		private readonly DiscordSocketClient _discord;
		private readonly IConfigurationRoot _config;

		public WeatherModule(DiscordSocketClient discord, IConfigurationRoot config, WeatherService service) {
			this._service = service;
			this._discord = discord;
			this._config = config;
		}


		[Command("weather"), Alias("w")]
		[Summary("Show weather for a given city")]
		public async Task ShowWeather(params string[] locationStrings) {
			if (locationStrings.Length <= 0) return;
			var location = locationStrings.CJoin();
			if (string.IsNullOrEmpty(location)) return;
			location = location.ToLower();

			// check cache
			var weatherJson = await JsonCache.LoadJsonAsync($"WeatherInfo/{location}", TimeSpan.FromHours(1));
			if (weatherJson == null) {
				var locationEncoded = HttpUtility.UrlEncode(location);
				var apiKey = this._config["api-key-weather"];
				
				// api.openweathermap.org/data/2.5/weather?q={city name}&appid={API key}
				var client = new RestClient($"https://api.openweathermap.org/data/2.5/weather?q={locationEncoded}&appid={apiKey}");
				var request = new RestRequest(Method.GET);
				var timeline = await client.ExecuteAsync(request, CancellationToken.None);

				if (!string.IsNullOrEmpty(timeline.ErrorMessage)) {
					Console.WriteLine($"Error trying to get weather for '{locationEncoded}': {timeline.ErrorMessage}");
					return;
				}
				if (string.IsNullOrEmpty(timeline.Content)) return;

				weatherJson = JSON.Parse(timeline.Content);
				if (weatherJson == null || weatherJson["cod"].AsInt != 200) {
					Console.WriteLine($"Error trying to parse weather json for {location}! timeline.Content:\n{timeline.Content}");
					return;
				}
				await JsonCache.SaveJsonAsync($"WeatherInfo/{location}", weatherJson);
			}

			var currentKelvin = weatherJson["main"]["temp"].AsFloat;
			var currentCelsius = currentKelvin - 273.15f;
			
			var embed = new EmbedBuilder();

			embed.Title = $"{currentCelsius:0} °C";
			embed.Description = $"Temperatura atual para {weatherJson["name"].Value}";

			try {
				var iconCode = weatherJson["weather"][0]["icon"].Value;
				embed.ThumbnailUrl = $"http://openweathermap.org/img/w/{iconCode}.png";

			} catch (Exception e) {
				Console.WriteLine($"Error trying to set icon from weather {location}:\n{e}");
			}
			
			await this.ReplyAsync("", false, embed.Build());
		}
		
	}
}
