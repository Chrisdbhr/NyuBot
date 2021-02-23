using System;
using System.Globalization;
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
		private readonly TimeSpan MAX_CACHE_AGE = TimeSpan.FromMinutes(30);

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
			location = ChatService.RemoveDiacritics(location.ToLower());

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			// check cache
			var weatherJson = await JsonCache.LoadJsonAsync($"WeatherInfo/{location}", this.MAX_CACHE_AGE);
			if (weatherJson == null) {
				
				var locationEncoded = HttpUtility.UrlEncode(location);
				var apiKey = this._config["api-key-weather"];
				
				// api.openweathermap.org/data/2.5/weather?q={city name}&appid={API key}
				var client = new RestClient($"https://api.openweathermap.org/data/2.5/weather?q={locationEncoded}&appid={apiKey}");
				var request = new RestRequest(Method.GET);
				var timeline = await client.ExecuteAsync(request);

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

				weatherJson["cacheTime"] = (DateTime.UtcNow - TimeSpan.FromHours(3)).ToString("hh:mm:ss tt");
				
				await JsonCache.SaveJsonAsync($"WeatherInfo/{location}", weatherJson);
			}
			
			var currentCelsius = weatherJson["main"]["temp"].AsFloat - 273.15f; // kelvin to celsius
			
			var embed = new EmbedBuilder();

			embed.Title = $"{currentCelsius:0} °C";
			embed.Description = $"Temperatura em {weatherJson["name"].Value}";

			var cacheTimeStr = weatherJson["cacheTime"].Value;
			if (!string.IsNullOrEmpty(cacheTimeStr)) {
				embed.Footer = new EmbedFooterBuilder {
					Text = $"Atualizado as {cacheTimeStr}"
				};
			}

			// get icon
			try {
				var iconCode = weatherJson["weather"][0]["icon"].Value;
				embed.ThumbnailUrl = $"http://openweathermap.org/img/w/{iconCode}.png";

			} catch (Exception e) {
				Console.WriteLine($"Error trying to set icon from weather {location}:\n{e}");
			}
			
			// get humildade
			try {
				var value = weatherJson["main"]["humidity"].Value;
				embed.AddField(
					new EmbedFieldBuilder {
						Name = $"{value}%",
						Value = "Humildade",
						IsInline = true
					}
				);
			} catch (Exception e) {
				Console.WriteLine($"Error trying to set humidity: {e}");
			}

			// get sensation
			float feelsLike = 0;
			try {
				feelsLike = weatherJson["main"]["feels_like"].AsFloat - 273.15f; // kelvin to celsius
				embed.AddField(
					new EmbedFieldBuilder {
						Name = $"{feelsLike:0} °C",
						Value = "Sensação térmica",
						IsInline = true
					}
				);
			} catch (Exception e) {
				Console.WriteLine($"Error trying to set sensation: {e}");
			}
			
			// get wind
			try {
				var value = weatherJson["wind"]["speed"].AsFloat * 3.6f; // mp/s to km/h
				embed.AddField(
					new EmbedFieldBuilder {
						Name = $"{value:0} (km/h)",
						Value = "Ventos",
						IsInline = true
					}
				);
			} catch (Exception e) {
				Console.WriteLine($"Error trying to set wind: {e}");
			}

			// get weather name
			try {
				var value = weatherJson["weather"][0]["main"].Value;
				var description = weatherJson["weather"][0]["description"].Value;
				embed.AddField(
					new EmbedFieldBuilder {
						Name = value,
						Value = description,
						IsInline = true
					}
				);
			} catch (Exception e) {
				Console.WriteLine($"Error trying to set weather name and description field: {e}");
			}
			
			await this.ReplyAsync(this.GetWeatherVerbalStatus((int)feelsLike), false, embed.Build());
		}

		private string GetWeatherVerbalStatus(int celsiusTemp) {
			if (celsiusTemp >= 45) {
				return "+ quente q o cu do sabs kkk";
			}
			if (celsiusTemp >= 40) {
				return "40 graus que tipo de Nordeste eh esse?";
			}
			if (celsiusTemp >= 35) {
				return "ta quente pracaralho";
			}
			if (celsiusTemp >= 30) {
				return "muito quente se eh loco";
			}
			if (celsiusTemp >= 23) {
				return "quente";
			}
			if (celsiusTemp >= 20) {
				return "maravilha";
			}
			if (celsiusTemp >= 15) {
				return "friozin";
			}
			if (celsiusTemp >= 10) {
				return "sul só pode";
			}
			if (celsiusTemp >= 5) {
				return "friodaporra";
			}
			if (celsiusTemp >= -4) {
				return "temperatura ideal de sulista";
			}
			if (celsiusTemp < -4) {
				return "boa bot, ta mais frio q dentro de uma geladeira kkk";
			}
			
			return string.Empty;
		}
		
	}
}
