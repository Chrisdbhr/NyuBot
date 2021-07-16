using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using RestSharp;
using SimpleJSON;

namespace NyuBot {
	public class ExchangeService : IDisposable {
		
		private readonly DiscordSocketClient _discord;
		private readonly IConfigurationRoot _config;
		private CompositeDisposable _disposable;


		private const string JPATH_EXCHANGEINFO = "ExchangeInfo";
		private const string JKEY_USD_BRL = "USD_BRL";

		#region <<---------- Initializers ---------->>
		
		public ExchangeService(DiscordSocketClient discord, IConfigurationRoot config) {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			// dependecies injected
			this._discord = discord;
			this._config = config;
			
			this._discord.MessageReceived += this.DiscordOnMessageReceived;
		}
		
		#endregion <<---------- Initializers ---------->>


		
		
		private async Task DiscordOnMessageReceived(SocketMessage socketMessage) {
			if (!(socketMessage is SocketUserMessage socketUserMessage)) return;
			if (socketUserMessage.Channel == null) return;
			var msg = socketUserMessage.Resolve().ToLower();
			if (string.IsNullOrEmpty(msg)) return;
			
			// check for msg about dolar
			if (msg.Length == 1) {
				if (msg != "$") return;
			}
			else {
				if (!msg.Contains("dolar")) return;
			}
			
			// thread culture
			var cultureInfo = new CultureInfo("en-us");
			Thread.CurrentThread.CurrentCulture = cultureInfo;
			Thread.CurrentThread.CurrentUICulture = cultureInfo;

			// get last exchange json
			var exchangeJson = await JsonCache.LoadJsonAsync(JPATH_EXCHANGEINFO, TimeSpan.FromMinutes(15));

			if (exchangeJson == null) {
				var apiKey = this._config["apikey-freecurrencyconverter"];
				
				// Example:
				// https://free.currconv.com/api/v7/convert?q=USD_BRL&compact=ultra&apiKey=82e456034f1bb5418116
				var client = new RestClient($"https://free.currconv.com/api/v7/convert?q=USD_BRL&compact=ultra&apiKey={apiKey}");
				var request = new RestRequest(Method.GET);
				var timeline = await client.ExecuteAsync(request);

				if (!string.IsNullOrEmpty(timeline.ErrorMessage)) {
					Console.WriteLine($"Error trying to get exchangeJson: {timeline.ErrorMessage}");
					return;
				}
				if (string.IsNullOrEmpty(timeline.Content)) return;

				exchangeJson = JSON.Parse(timeline.Content);
				if (exchangeJson == null || exchangeJson.IsNull) {
					Console.WriteLine($"Error trying to parse exchangeJson! timeline.Content:\n{timeline.Content}");
					return;
				}

				exchangeJson["cacheTime"] = (DateTime.UtcNow - TimeSpan.FromHours(3)).ToString("hh:mm:ss tt");
				
				await JsonCache.SaveToJson(JPATH_EXCHANGEINFO, exchangeJson);
			}

			var currencyValue = exchangeJson[JKEY_USD_BRL].AsFloat;
			
			var embed = new EmbedBuilder {
				Title = currencyValue.ToString("C2", cultureInfo),
				Description = "Cotação do dolar",
				Color = Color.DarkGreen
			};
			
			var cacheTimeStr = exchangeJson["cacheTime"].Value;
			if (!string.IsNullOrEmpty(cacheTimeStr)) {
				embed.Footer = new EmbedFooterBuilder {
					Text = $"Atualizado as {cacheTimeStr}"
				};
			}

			var channel = socketUserMessage.Channel;
			if (socketUserMessage.Channel == null) return;
			
			await channel.SendMessageAsync(MentionUtils.MentionUser(socketUserMessage.Author.Id), false, embed.Build());
		}

		

		#region <<---------- IDisposable ---------->>
		
		public void Dispose() {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			this._discord.MessageReceived -= this.DiscordOnMessageReceived;
		}
		
		#endregion <<---------- IDisposable ---------->>
		
	}
}
