using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

using NyuBot.Extensions;

namespace NyuBot {
	public class ChatService : IDisposable {

		#region <<---------- Initializers ---------->>

		public ChatService(DiscordSocketClient discord, CommandService commands, AudioService audioService, LoggingService loggingService, IConfigurationRoot configurationRoot, GuildSettingsService guildSettings) {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			// dependecies injected
			this._discord = discord;
			this._commands = commands;
			this._audioService = audioService;
			this._log = loggingService;
			this._config = configurationRoot;
			this._guildSettings = guildSettings;

			this._discord.MessageReceived += this.MessageReceivedAsync;
			this._discord.MessageDeleted += this.MessageDeletedAsync;
			this._discord.MessageUpdated += this.OnMessageUpdated;

			// first triggers
			Observable.Timer(TimeSpan.FromSeconds(5)).Subscribe(async _ => {
				// status
				await this._discord.SetGameAsync(Assembly.GetExecutingAssembly().ImageRuntimeVersion);

				await this.DayNightImgAndName();
				await this.HourlyMessage();
			}).AddTo(this._disposable);


			// status
			Observable.Timer(TimeSpan.FromMinutes(30)).Repeat().Subscribe(async _ => { await this.UpdateSelfStatusAsync(); }).AddTo(this._disposable);

			// check for bot ip change
			Observable.Timer(TimeSpan.FromMinutes(10)).Repeat().Subscribe(async _ => { await this.CheckForBotPublicIp(); }).AddTo(this._disposable);

			// change image at midnight
			Observable.Timer(TimeSpan.FromMinutes(15)).Repeat().Subscribe(async _ => { await this.DayNightImgAndName(); }).AddTo(this._disposable);

			// hora em hora
			Observable.Timer(TimeSpan.FromMinutes(1)).Repeat().Subscribe(async _ => { await this.HourlyMessage(); }).AddTo(this._disposable);
		}


		#endregion <<---------- Initializers ---------->>




		#region <<---------- Properties ---------->>

		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;
		private readonly AudioService _audioService;
		private readonly LoggingService _log;
		private readonly IConfigurationRoot _config;
		private readonly GuildSettingsService _guildSettings;
		private readonly Random _rand = new();
		private Dictionary<ulong, List<ulong>> LastMessagesIdsInChannel = new();

		private System.Timers.Timer _bumpTimer;
		private int _previousHour = -1;

		private CompositeDisposable _disposable;

		#endregion <<---------- Properties ---------->>




		#region <<---------- Callbacks ---------->>

		private async Task MessageReceivedAsync(SocketMessage socketMessage) {
			if (!(socketMessage is SocketUserMessage userMessage)) return;

			// private message
			if (userMessage.Channel is IDMChannel dmChannel) {
				await this.PrivateMessageReceivedAsync(socketMessage, dmChannel);
				return;
			}

			if (userMessage.Source != MessageSource.User) return;
			await this.UserMessageReceivedAsync(userMessage);
		}

		private async Task MessageDeletedAsync(Cacheable<IMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> cacheable1) {
			if (!cacheable.HasValue) return;
			var message = cacheable.Value;
			await this._log.Warning($"[MessageDeleted] from {message.Author.Username}: '{message.Content}'");
		}

		

		private async Task OnMessageUpdated(Cacheable<IMessage, ulong> cacheable, SocketMessage msg, ISocketMessageChannel channel) {
			await this.MessageReceivedAsync(msg);
		}

		private async Task PrivateMessageReceivedAsync(SocketMessage socketMessage, IDMChannel dmChannel) {
			await this._log.Info($"Private message received from {socketMessage.Author}: {socketMessage.Content}");

			if (socketMessage.Content.ToLower() == ",ip") {
				var ip = await this.GetBotPublicIp();
				await dmChannel.SendMessageAsync($"Meu IP:```{ip}```");
			}

			var dm = await this._discord.GetDMChannelAsync(203373041063821313);
			if (dm == null) return;
			await dm.SendMessageAsync($"```{socketMessage.Author.Username} me mandou DM:```{socketMessage.Content}");
			if (socketMessage.Attachments != null) {
				foreach (var attachment in socketMessage.Attachments) {
					await dm.SendFileAsync(attachment.Url, attachment.Filename);
				}
			}
		}

		#endregion <<---------- Callbacks ---------->>




		#region <<---------- Message Answer ---------->>

		private async Task UserMessageReceivedAsync(SocketUserMessage userMessage) {

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


			#region <<---------- New Users Anti Spam ---------->>

			try {
				if (messageString.Length > 140) {
					if (userMessage.Author is SocketGuildUser guildUser) {
						bool antiSpamEnabled = this._guildSettings.GetGuildSettings(guildUser.Guild.Id).EnableNewUserAntiSpam;
						if (antiSpamEnabled
							&& !guildUser.IsBot
							&& guildUser.JoinedAt.HasValue
							&& DateTimeOffset.UtcNow < guildUser.JoinedAt.Value.AddDays(7)) {
							await this._log.Warning($"Deleting {guildUser.GetNameSafe()} message because this user is new on this guild.");
							await userMessage.DeleteAsync();
							return;
						}
					}
				}
			} catch (Exception e) {
				await this._log.Error(e.ToString());
			}

			#endregion <<---------- New Users Anti Spam ---------->>


			// #region <<---------- User Specific ---------->>
			//
			// // babies
			// try {
			// 	var jsonArray = (await JsonCache.LoadValueAsync("UsersBabies", "data")).AsArray;
			// 	for (int i = 0; i < jsonArray.Count; i++) {
			// 		var userId = jsonArray[i].Value;
			// 		if (string.IsNullOrEmpty(userId)) continue;
			// 		if (userMessage.Author.Id != Convert.ToUInt64(userId)) continue;
			// 		await userMessage.AddReactionAsync(new Emoji("😭"));
			// 		break;
			// 	}
			// } catch (Exception e) {
			// 	await this._log.Error($"Exception trying to process babies answer: {e.ToString()}");
			// }
			//
			// #endregion <<---------- User Specific ---------->>

			#region Fast Answers

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
				|| messageString == "opa"
			) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {
					"oi", "olá", "hello", "coé", "oin", "aoba", "fala tu", "manda a braba", "opa"
				}));
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
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {
					"tchau", "xiau", "bye bye", "flw"
				}));
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
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {
					"Agora eu saqueeeeei!",
					"Agora tudo faz sentido!",
					"Eu estava cego agora estou enchergaaaando!",
					"Agora tudo vai mudar!",
					"Agora eu vou ficar de olhos abertos!"
				}));
				return;
			}

			#endregion

			#region General

			if (messageString == "alguem ai") {
				await userMessage.Channel.SendMessageAsync("Eu");
				return;
			}

			if (messageString.Contains("que horas sao") 
				|| messageString.Contains("que horas e") 
				|| messageString.Contains("que horas que e") 
				|| messageString.Contains("que horas q e")
				) {
				if (isQuestion) {
					await userMessage.Channel.SendMessageAsync("É hora de acertar as contas...");
					return;
				}
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

			#region <<---------- Anti trava discord ---------->>

			if (messageString.Length > 16 && ( 
					messageString.StartsWith("𒀱") 
					|| messageString.StartsWith("⬛")
					|| messageString.StartsWith("◼")
					|| messageString.StartsWith("\\ðŸ¤")
					|| messageString.StartsWith("¡ê")
					|| messageString.StartsWith("◻◾")
					)) {
				
				await userMessage.DeleteAsync();

				if (userMessage.Channel is SocketGuildChannel guildChannel) {
					var guild = this._discord.GetGuild(guildChannel.Guild.Id);
				
					// quarantine role
					if (userMessage.Author is SocketGuildUser guildUser) {
						var roleToAdd = guild.GetRole(474627963221049344);
						if (roleToAdd != null) {
							await guildUser.AddRoleAsync(roleToAdd);
						}
					}
				
					var msg = $"{MentionUtils.MentionUser(guild.OwnerId)} o {MentionUtils.MentionUser(userMessage.Author.Id)} enviou uma mensagem suspeita...";
					await userMessage.Channel.SendMessageAsync(msg);
				}
				return; // return because spam message is delete and not any more threatment is required
			}
			
			#endregion <<---------- Anti trava discord ---------->>

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
		
		#endregion <<---------- Message Answer ---------->>
		
		
		

		#region <<---------- User ---------->>
		
		public async Task UpdateSelfStatusAsync() {
			var statusText = Program.VERSION;
			try {
				var activitiesJsonArray = JsonCache.LoadFromJson<JArray>("BotStatus");
				var index = this._rand.Next(0, activitiesJsonArray.Count);
				var answers = activitiesJsonArray.ElementAt(index)["answers"];
				var statusTextArray = answers;
				var selectedStatus = statusTextArray[this._rand.Next(0, statusTextArray.Count())];
				await this._discord.SetGameAsync(
					selectedStatus.Value<string>(), 
					(ActivityType)index == ActivityType.Streaming ? "https://twitch.tv/chrisdbhr" : null, 
					(ActivityType)index
					);

			} catch (Exception e) {
				await this._log.Error(e.ToString());
				if (this._discord == null) return;
				await this._discord.SetGameAsync(statusText, null, ActivityType.Watching);
			}
		}
		
		#endregion <<---------- User ---------->>


		
		
		#region <<---------- Bot IP ---------->>

		private async Task CheckForBotPublicIp() {
			var ip = await this.GetBotPublicIp();
			
			var json = JsonCache.LoadFromJson<JObject>("Ip");
			
			if (json["lastIp"]?.Value<string>() == ip) return;

			// ip changed
			
			json["lastIp"] = ip;
			JsonCache.SaveToJson("Ip", json);
		}

		private async Task<string> GetBotPublicIp() {
			var client = new RestClient();
			var timeline = await client.ExecuteAsync(new RestRequest("http://ipinfo.io/ip", Method.Get));

			if (!string.IsNullOrEmpty(timeline.ErrorMessage)) {
				await this._log.Error($"Error trying to get bot IP: {timeline.ErrorMessage}");
				return null;
			}
			if (string.IsNullOrEmpty(timeline.Content)) return null;
			return timeline.Content.Trim();
		}
		
		#endregion <<---------- Bot IP ---------->>

		
		
		
		private async Task DayNightImgAndName() {
			if (this._discord.CurrentUser == null) return;

			var currentHour = DateTime.UtcNow.AddHours(-3).Hour;

			if (currentHour == this._previousHour) return;
			bool day = currentHour > 5;
			this._previousHour = currentHour;

			// change img
			var imgName = day ? "day" : "night";
			try {
				await using (var fileStream = new FileStream(Directory.GetCurrentDirectory() + $"/Assets/Images/Profile/{imgName}.jpg", FileMode.Open)) {
					var image = new Image(fileStream);
					await this._discord.CurrentUser.ModifyAsync(p => p.Avatar = image);
				}
			} catch (Exception e) {
				await this._log.Error($"Error trying to modify bot own profile pic: {e.Message.SubstringSafe(32)}");
			}

			var newName = day ? "Nyu" : "Lucy";
			foreach (var guild in this._discord.Guilds) {
				if (guild.CurrentUser.Nickname == newName) continue;
				if (guild.CurrentUser.GuildPermissions.ChangeNickname) {
					await guild.CurrentUser.ModifyAsync(p => p.Nickname = newName);
				}
			}

		}

		private async Task HourlyMessage() {
			var time = DateTime.UtcNow.AddHours(-3);
			if (time.Minute != 0) return;
			if (time.Hour == 0) {
				this._audioService.PlaySoundByNameOnAllMostPopulatedAudioChannels("meianoite").CAwait();
				return;
			}
			
			foreach (var guild in this._discord.Guilds) {
				try {
					string title = time.ToString("h tt", CultureInfo.InvariantCulture);
					string msg = null;
					switch (time.Hour) {
						case 0:
							title = "Meia noite, vão dormi";
							msg = $"Horário oficial do óleo de macaco";
							break;
						case 12:
							title = "Meio dia";
							msg = $"Hora de comer *nhon nhon nhon*";
							break;
					}

					if (this._guildSettings.GetGuildSettings(guild.Id).HourlyMessageChannelId == null) continue;
					
					var channel = guild.GetTextChannel(this._guildSettings.GetGuildSettings(guild.Id).HourlyMessageChannelId.Value);
					if (channel == null) continue;

					if (channel.CachedMessages.Count <= 0) return;

					var lastUserMsg = channel.CachedMessages.OrderBy(m => m.Timestamp).Last() as IUserMessage;

					bool lastMsgIsFromThisBot = lastUserMsg != null && lastUserMsg.Author.Id == this._discord.CurrentUser.Id;

					// motivation phrase
					if (string.IsNullOrEmpty(msg)) {
						msg = (await this.GetRandomMotivationPhrase()).RandomElement();
					}
					msg = string.IsNullOrEmpty(msg) ? "Hora agora" : $"*\"{msg}\"*";

					var embed = new EmbedBuilder {
						Title = title,
						Description = msg
					};


					RestUserMessage msgSend = null;
					if (lastMsgIsFromThisBot) {
						if (lastUserMsg.MentionedUserIds.Count <= 0) {
							await lastUserMsg.ModifyAsync(p =>
									p.Embed = embed.Build()
							);
						}
					}
					else {
						msgSend = await channel.SendMessageAsync(string.Empty, false, embed.Build());
					}

					// get random photo
					try {
						var client = new RestClient();
						var timeline = await client.ExecuteAsync(new RestRequest("https://picsum.photos/96", Method.Get));
						if (!string.IsNullOrEmpty(timeline.ResponseUri.OriginalString)) {
							embed.ThumbnailUrl = timeline.ResponseUri.OriginalString;
							if(msgSend != null) await msgSend.ModifyAsync(p => p.Embed = embed.Build());
						}
					} catch (Exception e) {
						await this._log.Error(e.ToString());
					}

				} catch (Exception e) {
					await this._log.Error(e.ToString());
					continue;
				}

			}
		}



		#region <<---------- Chat Messages ---------->>

		public async Task GetAndRepplyRememberMessage(SocketUserMessage msg, int amount, bool downloadIfNeeded) {
			var emoji = new Emoji("⌚") ;
			
			await msg.AddReactionAsync(emoji);
			
			await this.UpdateMessagesCacheForChannel(msg.Channel, amount, downloadIfNeeded);
			var channelId = msg.Channel.Id;
			this.SerializeMessages(this.LastMessagesIdsInChannel[channelId], channelId.ToString());

			var selectedMsg = await this.GetRandomUserMessageFromChannelCache(msg.Channel, 7);
			if (selectedMsg == null) return;
			if (selectedMsg is not IUserMessage userMsg) return;

			var time = DateTime.UtcNow - selectedMsg.Timestamp;
			
			var embed = new EmbedBuilder {
				Title = $"{time.TotalDays:0} dias atrás",
				Description = selectedMsg.Content,
				ThumbnailUrl = selectedMsg.Author.GetAvatarUrl() ?? selectedMsg.Author.GetDefaultAvatarUrl() 
			};

			await msg.RemoveAllReactionsForEmoteAsync(emoji);
			await msg.AddReactionAsync(new Emoji("👇"));
			await userMsg.ReplyAsync($"Lembrança de **{userMsg.Author.Mention}**",false,embed.Build(), AllowedMentions.None);
		}
		
		public async Task UpdateMessagesCacheForChannel(ISocketMessageChannel channel, int ammount, bool downloadIfNeeded) {
			var key = channel.Id;

			if (!downloadIfNeeded && this.LastMessagesIdsInChannel.ContainsKey(key)) {
				return;
			}
			
			this.UpdateCachedMsgsIdForChannelFromLocal(channel.Id);
			
			if (!downloadIfNeeded) {
				return;
			}
			
			var allMsgs = channel.GetMessagesAsync(ammount).GetAsyncEnumerator();

			while (await allMsgs.MoveNextAsync()) {
				var list = this.LastMessagesIdsInChannel[key];
				list.AddRange(allMsgs.Current.Select(e=>e.Id));
				await this._log.Info($"Including {allMsgs.Current.Count} messages on list of {list.Count} messages in '{channel.Name}'");
				this.LastMessagesIdsInChannel[key] = list.Distinct().ToList();
			}
			
		}

		private async Task<IMessage> GetRandomUserMessageFromChannelCache(ISocketMessageChannel channel, int minimumDays) {
			if (channel == null) return null;
			var listOfMessages = this.LastMessagesIdsInChannel[channel.Id];

			int maxTries = 10000;

			do {
				var selectedId = listOfMessages.RandomElement();
				var msg = await channel.GetMessageAsync(selectedId);
				if (msg is RestUserMessage m) {
					var mResolved = m.Resolve(0,
						TagHandling.Remove, TagHandling.Remove,
						TagHandling.Remove, TagHandling.Ignore,
						TagHandling.Ignore
					);

					bool isValidMsg = !m.Author.IsBot
					&& !m.Author.IsWebhook
					&& !string.IsNullOrEmpty(mResolved)
					&& (DateTime.UtcNow - m.Timestamp) > TimeSpan.FromDays(minimumDays)
					&& mResolved[0] != ','
					&& mResolved[0] != '<'
					&& mResolved[0] != 'h'
					&& mResolved.Length > 5;

					if (isValidMsg) {
						this.LastMessagesIdsInChannel[channel.Id] = listOfMessages;
						return m;
					}
				}
				
				// not valid, continue searching
				listOfMessages.Remove(selectedId);
				maxTries--;
			} while (maxTries > 0);

			await this._log.Info("Could not find valid message from channel cache.");
			return null;
		}

		private const string CHANNEL_MSGS_PATH = "Backup/ChannelMessagesIds/";
		private void UpdateCachedMsgsIdForChannelFromLocal(ulong channelId) {
			var cachedJson = JsonCache.LoadFromJson<JArray>(CHANNEL_MSGS_PATH + channelId.ToString());
			if (cachedJson == null) {
				this.LastMessagesIdsInChannel[channelId] = new List<ulong>();
				return;
			}
			var list = cachedJson.Select(e=>e.Value<ulong>()).ToList();
			this.LastMessagesIdsInChannel[channelId] = list;
		}
		
		private void SerializeMessages(List<ulong> messagesIds, string fileName) {
			JsonCache.SaveToJson($"{CHANNEL_MSGS_PATH}{fileName}", messagesIds);
		}
		
		#endregion <<---------- Chat Messages ---------->>
		
		
		

		#region <<---------- Pensador API ---------->>


		private async Task<List<string>> GetListOfMotivationalPhrases() {
			var client = new RestClient();
			var timeline = await client.ExecuteAsync(new RestRequest("https://www.pensador.com/recentes", Method.Get));
			
			if (!string.IsNullOrEmpty(timeline.ErrorMessage) || string.IsNullOrEmpty(timeline.Content)) {
				await this._log.Error($"Error trying Random Motivation Phrase: {timeline.ErrorMessage}");
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
			
			listOfPhrases = listOfPhrases.Where(p => !p.ToLower().Contains("deus") || !p.ToLower().Contains("senhor")).ToList();

			return listOfPhrases;
		}

		#endregion <<---------- Pensador API ---------->>


		

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
