using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using HtmlAgilityPack;
using RestSharp;
using SimpleJSON;

using NyuBot.Extensions;

namespace NyuBot {
	public class ChatService : IDisposable {

		#region <<---------- Initializers ---------->>
		
		public ChatService(DiscordSocketClient discord, CommandService commands, AudioService audioService) {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			// dependecies injected
			this._discord = discord;
			this._commands = commands;
			this._audioService = audioService;
			
			this._discord.MessageReceived += this.MessageReceivedAsync;
			this._discord.MessageReceived += this.MessageWithAttachment;
			this._discord.MessageDeleted += this.MessageDeletedAsync;
			this._discord.MessageUpdated += this.OnMessageUpdated;
			
			this._discord.UserJoined += this.UserJoined;
			this._discord.UserLeft += this.UserLeft;
			this._discord.UserBanned += this.UserBanned;
			this._discord.UserUpdated += async (oldUser, newUser) => {
				if (newUser.IsBot) return;
				Console.WriteLine($"UserUpdated: {newUser.Username}, Status: {newUser.Status}");
			};

			this._discord.UserVoiceStateUpdated += async (user, oldState, newState) => {
				if(!(user is SocketGuildUser socketGuildUser)) return;
				if (oldState.VoiceChannel == null && newState.VoiceChannel != null) {
					// joined voice channel
					await this.InformUserWakeUp(socketGuildUser);
				} 
			}; 

			
			// first triggers
			Observable.Timer(TimeSpan.FromSeconds(5)).Subscribe(async _ => {
				await this.SetStatusAsync();
				await this.DayNightImgAndName();
				await this.HourlyMessage();
			}).AddTo(this._disposable);

			
			// status
			Observable.Timer(TimeSpan.FromHours(1)).Repeat().Subscribe(async _ => {
				await this.SetStatusAsync();
			}).AddTo(this._disposable);

			// check for bot ip change
			Observable.Timer(TimeSpan.FromMinutes(10)).Repeat().Subscribe(async _ => {
				await this.CheckForBotPublicIp();
			}).AddTo(this._disposable);

			// change image at midnight
			Observable.Timer(TimeSpan.FromMinutes(15)).Repeat().Subscribe(async _ => {
				await this.DayNightImgAndName();
			}).AddTo(this._disposable);
			
			// hora em hora
			Observable.Timer(TimeSpan.FromMinutes(1)).Repeat().Subscribe(async _ => {
				await this.HourlyMessage();
			}).AddTo(this._disposable);
		}
		
		#endregion <<---------- Initializers ---------->>

		
		
		
		#region <<---------- Properties ---------->>
		
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;
		private readonly AudioService _audioService;
		private readonly Random _rand = new Random();
		
		private System.Timers.Timer _bumpTimer;
		private Dictionary<ulong, SocketMessage> _lastsSocketMessageOnChannels = new Dictionary<ulong, SocketMessage>();
		private int _previousHour = -1;

		private CompositeDisposable _disposable;
		
		#endregion <<---------- Properties ---------->>
		
		
		
		
		#region <<---------- Callbacks ---------->>

		private async Task UserJoined(SocketGuildUser socketGuildUser) {
			var channel = socketGuildUser.Guild.SystemChannel;
			if (channel == null) return;
			await channel.SendMessageAsync($"Temos uma nova pessoinha no servidor, digam **oi** para {socketGuildUser.Mention}!");
		}

		private async Task UserBanned(SocketUser socketGuildUser, SocketGuild socketGuild) {
			await this.UserLeavedGuild(socketGuildUser as SocketGuildUser, " saiu do servidor...");
		}	
		
		private async Task UserLeft(SocketGuildUser socketGuildUser) {
			await this.UserLeavedGuild(socketGuildUser, " saiu do servidor.");
		}

		private async Task MessageReceivedAsync(SocketMessage socketMessage) {
			if (!(socketMessage is SocketUserMessage userMessage)) return;
			switch (userMessage.Source) {
				case MessageSource.System:
					break;
				case MessageSource.User:
					if (userMessage.Channel is IDMChannel dmChannel) {
						await this.PrivateMessageReceivedAsync(socketMessage, dmChannel);
					}
					else {
						await this.UserMessageReceivedAsync(userMessage);
						
						// check user sleeping
						if (userMessage.Content.ToLower() == "a mimir") {
							await this.SetUserIsSleeping(userMessage);
						}
						else {
							await this.InformUserWakeUp(userMessage.Author as SocketGuildUser);
						}
					}
					break;
				case MessageSource.Bot:
					await this.BotMessageReceivedAsync(userMessage);
					break;
				case MessageSource.Webhook:
					break;
			}
		}

		private async Task MessageDeletedAsync(Cacheable<IMessage, ulong> cacheable, ISocketMessageChannel socketMessageChannel) {
			if (!cacheable.HasValue) return;
			var message = cacheable.Value;
			Console.WriteLine($"[MessageDeleted] from {message.Author.Username} in {socketMessageChannel.Name}: '{message.Content}'");
		}
		
		private async Task MessageWithAttachment(SocketMessage socketMessage) {
			foreach (var attachment in socketMessage.Attachments) {
				using (var client = new WebClient()) {
					var targetDir = $"FilesBackup/{socketMessage.Channel.Name}/";
					Directory.CreateDirectory(targetDir);
					await client.DownloadFileTaskAsync(new Uri(attachment.Url), $"{targetDir}{attachment.Filename}");
				}
			}
		}
		
		private async Task OnMessageUpdated(Cacheable<IMessage, ulong> cacheable, SocketMessage msg, ISocketMessageChannel channel) {
			await this.MessageReceivedAsync(msg);
		}

		private async Task PrivateMessageReceivedAsync(SocketMessage socketMessage, IDMChannel dmChannel) {
			Console.WriteLine($"Private message received from {socketMessage.Author}: {socketMessage.Content}");

			if (socketMessage.Content.ToLower().Contains("ip")) {
				var ip = await this.GetBotPublicIp();
				await dmChannel.SendMessageAsync($"Meu IP:```{ip}```");
			}
		}

		#endregion <<---------- Callbacks ---------->>


		

		#region <<---------- Message Answer ---------->>

		private async Task UserMessageReceivedAsync(SocketUserMessage userMessage) {
			
			// save this as last message
			this._lastsSocketMessageOnChannels[userMessage.Channel.Id] = userMessage;


			if (string.IsNullOrEmpty(userMessage.Content)) return;
			
			// Parameters
			bool userSaidHerName = false;
			bool isQuestion = false;

			#region <<---------- Setup message string to read ---------->>

			// Content of the message in lower case string.
			string messageString = userMessage.Content.ToLower();

			messageString = RemoveDiacritics(messageString);

			messageString = messageString.Trim();

			// if the message is a question
			if (messageString.Contains('?')) {
				messageString = messageString.Replace("?", string.Empty);
				isQuestion = true;
			}

			// if user said her name
			if (HasAtLeastOneWord(messageString, new[] {"nyu", "nuy"})) {
				userSaidHerName = true;
				messageString = RemoveBotNameFromMessage(messageString);
			}
			else if (userMessage.MentionedUsers.Contains(this._discord.CurrentUser)) {
				// remove the mention string from text
				messageString = messageString.Replace(this._discord.CurrentUser.Mention, "");
				userSaidHerName = true;
			}

			// remove double and tripple spaces
			messageString = messageString.Replace("  ", " ").Replace("   ", " ");

			// See if message is empty now
			if (messageString.Length <= 0) {
				return;
			}
			
			#endregion <<---------- Setup message string to read ---------->>
	
			
			#region <<---------- User Specific ---------->>

			// babies
			try {
				var jsonArray = (await JsonCache.LoadValueAsync("UsersBabies", "data")).AsArray;
				for (int i = 0; i < jsonArray.Count; i++) {
					var userId = jsonArray[i].Value;
					if (string.IsNullOrEmpty(userId)) continue;
					if (userMessage.Author.Id != Convert.ToUInt64(userId)) continue;
					await userMessage.AddReactionAsync(new Emoji("😭"));
					break;
				}
			} catch (Exception e) {
				Console.WriteLine($"Exception trying to process babies answer: {e}");
			}
			
			#endregion <<---------- User Specific ---------->>

			#region Fast Answers

			if (messageString == "a mimir") {
				await this.SetUserIsSleeping(userMessage);
				return;
			}
			
			if (messageString == ("ping")) {
				await userMessage.Channel.SendMessageAsync("pong");
				return;
			}
			if (messageString == ("pong")) {
				await userMessage.Channel.SendMessageAsync("ping");
				return;
			}

			if (messageString == ("marco")) {
				await userMessage.Channel.SendMessageAsync("polo");
				return;
			}
			if (messageString == ("polo")) {
				await userMessage.Channel.SendMessageAsync("marco");
				return;
			}

			if (messageString == ("dotto")) {
				await userMessage.Channel.SendMessageAsync("Dotto. :musical_note:");
				return;
			}

			if (messageString == "❤" || messageString == ":heart:") {
				await userMessage.Channel.SendMessageAsync("❤");
				return;
			}

			if (messageString == ":broken_heart:" || messageString == "💔") {
				await userMessage.Channel.SendMessageAsync("❤");
				await userMessage.AddReactionAsync(new Emoji("😥"));
				return;
			}

			if (messageString == ("ne") || messageString == ("neh")) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Isso ai.", "Pode crê.", "Boto fé."}));
				return;
			}

			if (messageString == ("vlw") || messageString == ("valeu") || messageString == ("valew")) {
				await userMessage.AddReactionAsync(new Emoji("😉"));
				return;
			}

			// see if message is an Hi
			if (messageString == "oi"
				|| messageString == "ola"
				|| messageString == "hi"
				|| messageString == "hello"
				|| messageString == "coe"
				|| messageString == "ola pessoas"
				|| messageString == "coe rapaziada"
				|| messageString == "dae"
				|| messageString == "oi galera"
				|| messageString == "dae galera"
			) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Oi.", "Olá.", "Hello.", "Coé.", "Oin.", "Aoba."}));
				return;
			}

			// see if message has an BYE
			if (messageString == "tchau"
				|| messageString == "xau"
				|| messageString == "tiau"
				|| messageString == "thau"
				|| messageString == "xau"
				|| messageString == "flw"
				|| messageString == "flws"
				|| messageString == "falou"
				|| messageString == "falous"
				|| messageString.Contains(" flw")
			) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Tchau.", "Xiau.", "Bye bye.", "Flw."}));
				return;
			}

			#endregion

			#region Nyu
			// check if user said nyu / nuy
			if (userSaidHerName) {
				if (HasAtLeastOneWord(messageString, new[] {"serve", "faz"})) {
					if (isQuestion) {
						await userMessage.Channel.SendMessageAsync("Sou um bot que responde diversas perguntas sobre assuntos comuns aqui no servidor. Com o tempo o Chris me atualiza com mais respostas e reações.");
						return;
					}
				}

				// Zueras
				if (messageString == ("vo ti cume")
					|| messageString == ("vo ti come")
					|| messageString == ("vou te come")
					|| messageString == ("quero te come")
					|| messageString == ("quero te pega")
				) {
					await userMessage.AddReactionAsync(new Emoji("😠")); // angry
					return;
				}

				// Praises
				if (messageString.Contains("gata")
					|| messageString.Contains("cremosa")
					|| messageString.Contains("gostosa")
					|| messageString.Contains("gatinha")
					|| messageString.Contains("linda")
					|| messageString.Contains("delicia")
					|| messageString.Contains("dlicia")
					|| messageString.Contains("dlcia")
					|| messageString == ("amo te")
					|| messageString == ("ti amu")
					|| messageString == ("ti amo")
					|| messageString == ("ti adoro")
					|| messageString == ("te adoro")
					|| messageString == ("te amo")
					|| messageString == ("obrigado")
					|| messageString == ("obrigada")
				) {
					await userMessage.AddReactionAsync(new Emoji("❤"));
					return;
				}

			}
			#endregion

			#region Animes
			// Check for `Boku no picu`
			if (messageString.Contains("boku no picu")
				|| messageString.Contains("boku no pico")
				|| messageString.Contains("boku no piku")
				|| messageString.Contains("boku no piku")
			) {
				await userMessage.AddReactionAsync(new Emoji("😶"));
				return;
			}
			#endregion

			#region Memes
			// Ahhh agora eu entendi
			if (messageString.EndsWith("agora eu entendi")) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Agora eu saqueeeeei!", "Agora tudo faz sentido!", "Eu estava cego agora estou enchergaaaando!", "Agora tudo vai mudar!", "Agora eu vou ficar de olhos abertos!"}));
				return;
			}
			
			#endregion

			#region General
			
			if (messageString == "alguem ai") {
				await userMessage.Channel.SendMessageAsync("Eu");
				return;
			}

			if (messageString.Contains("que horas sao")) {
				if (isQuestion) {
					await userMessage.Channel.SendMessageAsync("É hora de acertar as contas...");
					return;
				}
			}

			// Disboard bump
			if (messageString == "!d bump") {
				this._bumpTimer?.Close();
				this._bumpTimer = new System.Timers.Timer(120 * 60 * 1000);
				var channel = userMessage.Channel;
				var user = userMessage.Author;
				await channel.SendMessageAsync($"{user.Mention} vou lembrar daqui a 2 horas pra dar bump de novo.");
				this._bumpTimer.Elapsed += async (sender, args) => {
					await channel.SendMessageAsync($"{user.Mention}\nJa da pra dar bump no server de novo! Mande essa mensagem aqui:\n```!d bump```");
				};
				this._bumpTimer.AutoReset = false;
				this._bumpTimer.Start();
				return;
			}
			
			#endregion

			#region Insults
			// Answer to insults 

			if (messageString.Contains("bot lixo")
				|| messageString.Contains("suamaeeminha")
			) {
				await userMessage.AddReactionAsync(new Emoji("👀"));
				return;
			}
			#endregion

			#region Links
			#region Black Yeast
			// Firsts
			if (isQuestion && HasAllWords(messageString, new[] {"black", "yeast"})) {
				// user is speaking about Black Yeast.
				await userMessage.Channel.SendMessageAsync(userMessage.Author.Mention + " vc disse Black Yeast? Veja mais infomações dele aqui: https://chrisdbhr.github.io/blackyeast");
				return;
			}
			#endregion

			#region Canal
			if (HasAllWords(messageString, new[] {"canal", "youtube", "chris"})) {
				await userMessage.Channel.SendMessageAsync("Se quer saber qual o canal do Chris o link é esse: https://www.youtube.com/christopher7");
				return;
			}
			
			if (messageString.Contains("twitch") && HasAtLeastOneWord(messageString, new[] {"seu", "canal"})) {
				await userMessage.Channel.SendMessageAsync("O link para o Twitch do Chris é esse: https://www.twitch.tv/chrisdbhr");
				return;
			}
			#endregion
			#endregion

			//!!! THIS PART OF THE CODE BELOW MUST BE AS THE LAST BECAUSE:
			// TODO bot log unknown commands on file 
			// see if user sayd only bot name on message with some other things and she has no answer yet
			// if (userSaidHerName) {
			// 	string unknownCommandsFileName = "Lists/unknownCommands.txt";
			// 	string textToWrite = messageString + $"	({userMessage.Author.Username})";
			//
			// 	// first, compare if the text to save its not to big
			// 	if (textToWrite.Length > 48) {
			// 		// ignore the message because it can be spam
			// 		return;
			// 	}
			//
			// 	// check if the txt its not biggen then 10mb
			// 	await using (var ss = new StreamWriter(unknownCommandsFileName)) {
			// 		
			// 	}
			// 	var fileInfo = new FileInfo(unknownCommandsFileName);
			// 	if (fileInfo.Length > 10 * 1000000) {
			// 		await userMessage.Channel.SendMessageAsync("<@203373041063821313> eu tentei adicionar o texto que o " + userMessage.Author.Username + " digitou mas o arquivo de lista de comandos alcançou o tamanho limite. :sob:");
			// 		return;
			// 	}
			//
			// 	// get text in string
			// 	string fileContent = File.ReadAllText(unknownCommandsFileName);
			// 	if (fileContent != null) {
			// 		// only write if the unknown text is NOT already on the file
			// 		if (!fileContent.Contains(messageString)) {
			// 			File.AppendAllText(unknownCommandsFileName, textToWrite + Environment.NewLine);
			// 			await userMessage.AddReactionAsync(new Emoji("❔"));
			// 			return;
			// 		}
			// 	}
			// 	else {
			// 		File.AppendAllText(unknownCommandsFileName, textToWrite + Environment.NewLine);
			// 		await userMessage.AddReactionAsync(new Emoji("❔"));
			// 		return;
			// 	}
			//
			// 	// return "Ainda não tenho resposta para isso:\n" + "`" + messageString + "`";
			// 	return;
			// }

			// if arrived here, the message has no answer.
		}

		private async Task BotMessageReceivedAsync(SocketUserMessage userMessage) {
			
		}
		
		#endregion <<---------- Message Answer ---------->>
		
		
		

		#region <<---------- User ---------->>
		
		private async Task SetStatusAsync() {
			var jsonArray = (await JsonCache.LoadValueAsync("ChrisGames", "data")).AsArray;
			string gameName = null;
			if (jsonArray != null) {
				gameName = jsonArray[this._rand.Next(0, jsonArray.Count)].Value;
			}
			if (this._discord == null) return;
			await this._discord.SetGameAsync(gameName ?? "chrisjogos.com");
		}
		
		private async Task UserLeavedGuild(SocketGuildUser socketGuildUser, string sufixMsg) {
			var channel = socketGuildUser.Guild.SystemChannel;
			if (channel == null) return;
			
			var jsonArray = (await JsonCache.LoadValueAsync("Answers/UserLeave", "data")).AsArray;
			string customAnswer = null;
			if (jsonArray != null) {
				customAnswer = jsonArray[this._rand.Next(0, jsonArray.Count)].Value;
			}
			
			var sb = new StringBuilder();
			sb.Append($"{socketGuildUser.Username}#{socketGuildUser.DiscriminatorValue}");
			sb.Append($"{(socketGuildUser.Nickname != null ? $" ({socketGuildUser.Nickname})" : null)}");
			sb.AppendLine($"{sufixMsg}");
			sb.AppendLine($"**{customAnswer}**");
			sb.AppendLine($"Temos {socketGuildUser.Guild.MemberCount} membros agora.");
			await channel.SendMessageAsync(sb.ToString());
		}
		
		#endregion <<---------- User ---------->>


		
		
		#region <<---------- Bot IP ---------->>

		private async Task CheckForBotPublicIp() {
			var ip = await this.GetBotPublicIp();
			
			var json = await JsonCache.LoadJsonAsync("Ip") ?? new JSONObject {["lastIp"] = ""};
			
			if (json.Value == ip) return;

			// ip changed
			
			json["lastIp"].Value = ip;
			await JsonCache.SaveJsonAsync("Ip", json);
		}

		private async Task<string> GetBotPublicIp() {
			var client = new RestClient("http://ipinfo.io/ip");
			var request = new RestRequest(Method.GET);
			var timeline = await client.ExecuteAsync(request, CancellationToken.None);

			if (!string.IsNullOrEmpty(timeline.ErrorMessage)) {
				Console.WriteLine($"Error trying to get bot IP: {timeline.ErrorMessage}");
				return null;
			}
			if (string.IsNullOrEmpty(timeline.Content)) return null;
			return timeline.Content.Trim();
		}

		private async Task DayNightImgAndName() {
			if (this._discord.CurrentUser == null) return;

			if (DateTime.Now.Hour == this._previousHour) return;
			bool day = DateTime.Now.Hour > 5;
			this._previousHour = DateTime.Now.Hour;

			// change img
			var imgName = day ? "day" : "night";
			try {
				await using (var fileStream = new FileStream(Directory.GetCurrentDirectory() + $"/Assets/Images/Profile/{imgName}.jpg", FileMode.Open)) {
					var image = new Image(fileStream);
					await this._discord.CurrentUser.ModifyAsync(p => p.Avatar = image);
				}
			} catch (Exception e) {
				Console.WriteLine(e);
			}

			var newName = day ? "Nyu" : "Lucy";
			foreach (var guild in this._discord.Guilds) {
				if (guild.CurrentUser.Nickname == newName) continue;
				await guild.CurrentUser.ModifyAsync(p => p.Nickname = newName);
			}

		}

		private async Task HourlyMessage() {
			var time = DateTime.Now;
			if (time.Minute != 0) return;
			
			foreach (var guild in this._discord.Guilds) {
				var channel = guild.SystemChannel;
				if (channel == null) continue;

				string title = $"{time:h tt}";
				string msg = null;
				switch (time.Hour) {
					case 0:
						title = "Meia noite, vão dormi";
						msg = $"Horário oficial do óleo de macaco";
						this._audioService.PlaySoundByNameOnAllMostPopulatedAudioChannels("meianoite").CAwait();
						break;
					case 12:
						title = "Meio dia";
						msg = $"Hora de comer *nhon nhon nhon*";
						break;
				}

				// motivation phrase
				if (string.IsNullOrEmpty(msg)) {
					msg = await this.GetRandomMotivationPhrase();
				}
				msg = string.IsNullOrEmpty(msg) ? "Hora agora" : $"*\"{msg}\"*";

				var embed = new EmbedBuilder {
					Title = title,
					Description = msg
				};
				
				var msgSend = await channel.SendMessageAsync(string.Empty, false, embed.Build());

				// get random photo
				try {
					var client = new RestClient("https://picsum.photos/96");
					var request = new RestRequest(Method.GET);
					var timeline = await client.ExecuteAsync(request, CancellationToken.None);
					if (!string.IsNullOrEmpty(timeline.ResponseUri.OriginalString)) {
						embed.ThumbnailUrl = timeline.ResponseUri.OriginalString;
						await msgSend.ModifyAsync(p => p.Embed = embed.Build());
					}
				} catch (Exception e) {
					Console.WriteLine(e);
				}
				
			}
		}

		#endregion <<---------- Bot IP ---------->>


		

		#region <<---------- Pensador API ---------->>

		private async Task<string> GetRandomMotivationPhrase() {
			var client = new RestClient("https://www.pensador.com/frases");
			var request = new RestRequest(Method.GET);
			var timeline = await client.ExecuteAsync(request, CancellationToken.None);

			if (!string.IsNullOrEmpty(timeline.ErrorMessage) || string.IsNullOrEmpty(timeline.Content)) {
				Console.WriteLine($"Error trying Random Motivation Phrase: {timeline.ErrorMessage}");
				return null;
			}

			var html = new HtmlDocument();
			html.LoadHtml(timeline.Content);
			var nodeCollection = html.DocumentNode.SelectNodes("//p");

			var listOfPhrases = new List<string>();
			foreach (var node in nodeCollection) {
				if (string.IsNullOrEmpty(node.Id)) continue;
				listOfPhrases.Add(node.InnerText);
			}
			
			return listOfPhrases.RandomElement();
		}

		#endregion <<---------- Pensador API ---------->>



		#region <<---------- A mimir ---------->>

		private const string JSONPATH_USERSLEEPINFO = "SleepManagment/";
		
		private async Task SetUserIsSleeping(SocketUserMessage msg) {
			var path = $"{JSONPATH_USERSLEEPINFO}{msg.Author.Id}";
			if (!(msg.Author is SocketGuildUser author)) return;

			var jsonNode = (await JsonCache.LoadJsonAsync(path)) ?? new JSONObject();
			jsonNode["sleep-start-time"] = DateTime.Now.Ticks.ToString();
			
			jsonNode["isSleeping"] = true;
			
			await JsonCache.SaveJsonAsync(path, jsonNode);
			await msg.AddReactionAsync(new Emoji("💤"));
		}

		private async Task InformUserWakeUp(SocketGuildUser user) {
			if (user == null) return;
			var path = $"{JSONPATH_USERSLEEPINFO}{user.Id}";
			
			var jsonNode = (await JsonCache.LoadJsonAsync(path)) ?? new JSONObject();
			
			if (!jsonNode["isSleeping"].AsBool) return;
			
			var value = jsonNode["sleep-start-time"].Value;
			var sleepTime = new DateTime(Convert.ToInt64(value));
			var totalSleepTime = DateTime.Now - sleepTime;
			
			var embed = new EmbedBuilder {
				Title = $"Parece que {user.GetNameSafe()} acordou",
				Description = $"{user.Mention} dormiu um total de: {totalSleepTime.ToString(@"hh\:mm")}"
			};
			embed.Color = Color.LightOrange;
			
			embed.AddField(new EmbedFieldBuilder {
				Name = "Horas que foi a mimir",
				Value = sleepTime.ToString(@"hh\:mm tt")
			});

			jsonNode["isSleeping"] = false;
			
			await JsonCache.SaveJsonAsync(path, jsonNode);
			await user.Guild.SystemChannel.SendMessageAsync(string.Empty, false, embed.Build());
		}
		
		#endregion <<---------- A mimir ---------->>
		
		

		#region <<---------- String Threatment ---------->>
		
		/// <summary>
		/// Check if a string contains all defined words.
		/// </summary>
		/// <param name="message">Full string to compare.</param>
		/// <param name="s">Words to check.</param>
		/// <returns>Return if there is all of words in message.</returns>
		public static bool HasAllWords(string message, string[] s) {
			for (int i = 0; i < s.Length; i++) {
				if (!message.Contains(s[i])) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Check if a string contains at least one of defined words.
		/// </summary>
		/// <param name="message">Full string to compare.</param>
		/// <param name="s">Words to check.</param>
		/// <returns>Return true if there is a word in message.</returns>
		public static bool HasAtLeastOneWord(string message, string[] s) {
			return s.Any(c => message.Contains(c));
		}

		/// <summary>
		/// Chosse a string between an array of strings.
		/// </summary>
		/// <param name="s">strings to choose, pass as new[] { "option1", "option2", "..." }</param>
		/// <returns>return the choose string</returns>
		public static string ChooseAnAnswer(string[] s) {
			if (s.Length > 1) {
				return s[new System.Random().Next(0, s.Length)];
			}

			// equals one
			return s[0];
		}

		/// <summary>
		/// Remove special characters from string.
		/// </summary>
		/// <param name="text"></param>
		/// <returns>Return normalized string.</returns>
		public static string RemoveDiacritics(string text) {
			var normalizedString = text.Normalize(NormalizationForm.FormD);
			var stringBuilder = new StringBuilder();

			foreach (var c in normalizedString) {
				var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark) {
					stringBuilder.Append(c);
				}
			}

			return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
		}

		/// <summary>
		/// Removes bot string from message, and trim string, also set a boolean.
		/// </summary>
		public static string RemoveBotNameFromMessage(string messageString) {
			messageString = messageString.Replace("nyu", "");
			messageString = messageString.Replace("nuy", "");
			messageString = messageString.Trim();
			return messageString;
		}
		
		#endregion <<---------- String Threatment ---------->>


		

		#region <<---------- Disposable ---------->>

		public void Dispose() {
			this._discord?.Dispose();
			((IDisposable) this._commands)?.Dispose();
			this._bumpTimer?.Dispose();
			this._disposable?.Dispose();
		}

		#endregion <<---------- Disposable ---------->>
		
	}
}
