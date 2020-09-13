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
using Discord.WebSocket;
using NyuBot.Extensions;
using RestSharp;
using SimpleJSON;

namespace NyuBot {
	public class ChatService : IDisposable {

		#region <<---------- Initializers ---------->>
		
		public ChatService(DiscordSocketClient discord, CommandService commands) {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			this._commands = commands;
			this._discord = discord;
			
			this._discord.MessageReceived += this.MessageReceivedAsync;
			this._discord.MessageReceived += this.MessageWithAttachment;
			this._discord.MessageDeleted += this.MessageDeletedAsync;
			this._discord.MessageUpdated += this.OnMessageUpdated;
			this._discord.UserJoined += UserJoined;
			this._discord.UserLeft += this.UserLeft;
			this._discord.UserBanned += this.UserBanned;
			this._discord.UserUpdated += async (oldUser, newUser) => {
				if (newUser.IsBot) return;
				Console.WriteLine($"UserUpdated: {newUser.Username}, Status: {newUser.Status}");
			};

			this.SetStatusAsync().CAwait();
			Observable.Timer(TimeSpan.FromHours(1)).Repeat().Subscribe(async _ => {
				await this.SetStatusAsync();
			});


			// check for bot ip change
			this._disposable.Add(
				Observable.Timer(TimeSpan.FromMinutes(10)).Repeat().Subscribe(async _ => {
					await this.CheckForBotPublicIp();
				})
			);
			
			
		}
		
		#endregion <<---------- Initializers ---------->>

		
		
		
		#region <<---------- Properties ---------->>
		
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;
		private readonly Random _rand = new Random();
		private System.Timers.Timer _bumpTimer;
		private Dictionary<ulong, SocketMessage> _lastsSocketMessageOnChannels = new Dictionary<ulong, SocketMessage>();

		private CompositeDisposable _disposable;
		
		#endregion <<---------- Properties ---------->>
		
		
		
		
		#region <<---------- Callbacks ---------->>

		private async Task UserJoined(SocketGuildUser socketGuildUser) {
			var channel = socketGuildUser.Guild.SystemChannel;
			if (channel == null) return;
			var sb = new StringBuilder();
			sb.Append("Temos uma nova pessoinha no servidor, digam **oi** para ");
			sb.Append(socketGuildUser.Mention);
			sb.Append("!");
			await channel.SendMessageAsync(sb.ToString());
		}

		private async Task UserBanned(SocketUser socketGuildUser, SocketGuild socketGuild) {
			await this.UserLeavedGuild(socketGuildUser as SocketGuildUser, " saiu do servidor...");
		}	
		
		private async Task UserLeft(SocketGuildUser socketGuildUser) {
			await this.UserLeavedGuild(socketGuildUser, " saiu do servidor.");
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
					}
					break;
				case MessageSource.Bot:
					await this.BotMessageReceivedAsync(userMessage);
					break;
				case MessageSource.Webhook:
					break;
			}
		}
		
		private async Task OnMessageUpdated(Cacheable<IMessage, ulong> cacheable, SocketMessage msg, ISocketMessageChannel channel) {
			await this.MessageReceivedAsync(msg);
		}

		private async Task PrivateMessageReceivedAsync(SocketMessage socketMessage, IDMChannel dmChannel) {
			Console.WriteLine($"Private message received from {socketMessage.Author}: {socketMessage.Content}");

			if (socketMessage.Content.ToLower() == "ip") {
				var ip = await this.GetBotPublicIp();
				await dmChannel.SendMessageAsync($"Meu IP:```{ip}```");
			}
		}

		#endregion <<---------- Callbacks ---------->>


		

		#region <<---------- Message Answer ---------->>

		private async Task UserMessageReceivedAsync(SocketUserMessage userMessage) {
			if (string.IsNullOrEmpty(userMessage.Content)) return;
			
			// Parameters
			bool userSaidHerName = false;
			bool isQuestion = false;
			
			#region Setup message string to read
			// Content of the message in lower case string.
			string messageString = userMessage.Content.ToLower();

			messageString = RemoveDiacritics(messageString);

			messageString = messageString.Trim();

			// if the message is a question
			if (messageString.Contains('?')) {
				// Get rid of all ?
				messageString = messageString.Replace("?", "");
				isQuestion = true;
			}

			// if user sayd her name
			if (HasAtLeastOneWord(messageString, new[] {"nyu", "nuy"})) {
				userSaidHerName = true;
				messageString = RemoveBotNameFromMessage(messageString);
			}
			else if (userMessage.MentionedUsers.Contains(_discord.CurrentUser)) {
				// remove the mention string from text
				messageString = messageString.Replace(_discord.CurrentUser.Mention, "");
				userSaidHerName = true;
			}

			// remove double and tripple spaces
			messageString = messageString.Replace("  ", " ").Replace("   ", " ");

			// See if message is empty now
			if (messageString.Length <= 0) {
				if (userSaidHerName) {
					await userMessage.AddReactionAsync(new Emoji(":question:"));
				}
				return;
			}
			#endregion

			#region <<---------- Consider Last message ---------->>
			
			if (messageString.Length > 1 && messageString.All(c => c == 'k')) {
				try {
					if (this._lastsSocketMessageOnChannels.TryGetValue(userMessage.Channel.Id, out var lastMessage)) {
						if (lastMessage is SocketUserMessage lastUserMessage) {
							await userMessage.DeleteAsync();
							await lastUserMessage.AddReactionAsync(new Emoji("🤣"));
							return;
						}
					}
					
				} catch (Exception e) {
					Console.WriteLine($"Exception trying to get last message:\n{e}");
				} 
			}
			
			#endregion <<---------- Consider Last message ---------->>

			
			// save this as last message
			this._lastsSocketMessageOnChannels[userMessage.Channel.Id] = userMessage;

			
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
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Oi.", "Olá.", "Hello.", "Coé.", "Oin."}));
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

			#region Erase BotCommands
			if (
				messageString.StartsWith(",")
			) {
				await userMessage.AddReactionAsync(new Emoji("❌"));
				await Task.Delay(1000 * 2); // 1 second
				await userMessage.DeleteAsync();
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
					await userMessage.Channel.SendMessageAsync("Não pode.");
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
				await userMessage.Channel.SendMessageAsync(userMessage.Author.Mention + " Gay.");
				return;
			}
			#endregion

			#region Memes
			// Ahhh agora eu entendi
			if (messageString.EndsWith("agora eu entendi")) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] {"Agora eu saqueeeeei!", "Agora tudo faz sentido!", "Eu estava cego agora estou enchergaaaando!", "Agora tudo vai mudar!", "Agora eu vou ficar de olhos abertos!"}));
				return;
			}

			#region Teu cu na minha mao
			// all possible answers
			if (messageString.Contains("mo vacilao") || messageString.Contains("mo vacilaum")) {
				await userMessage.Channel.SendMessageAsync("''Hmmmm vacilão... Teu cu na minha mao.''");
				return;
			}
			if (messageString.Contains("teu cu na minha mao")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu e o aeroporto meu pau e o avião.''");
				return;
			}
			if (messageString.Contains("teu cu e o aeroporto meu pau e o aviao")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é a garagem meu pau é o caminhão.''");
				return;
			}
			if (messageString.Contains("teu cu e a garagem meu pau e o caminhao")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é a Carminha meu pau é o Tufão (ãnh?).''");
				return;
			}
			if (HasAllWords(messageString, new[] {"teu cu", "meu pau", "tufao"})) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o mar meu pau é o tubarão.''");
				return;
			}
			if (messageString.Contains("teu cu e o mar meu pau e o tubarao")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o morro meu pau é o Complexo do Alemão.''");
				return;
			}
			if (messageString.Contains("teu cu e o morro meu pau e o complexo do alemao")) {
				await userMessage.Channel.SendMessageAsync("''Caraaalho, sem nexo.''");
				return;
			}
			if (messageString.Contains("sem nexo")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o cabelo meu pau é o reflexo.''");
				return;
			}
			if (HasAllWords(messageString, new[] {"teu cu e o cabelo", "meu pau e reflexo"})) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o Moon Walker meu pau é o Michael Jackson.''");
				return;
			}
			if (HasAllWords(messageString, new[] {"teu cu e o", "meu pau e o"})
				&& (HasAtLeastOneWord(messageString, new[] {"michael", "mickael", "maicow", " maycow", " maico", "jackson", "jackso", "jakso", "jakson", "jequiso", "jequison"})
					|| HasAtLeastOneWord(messageString, new[] {" moon ", " mun ", "walker", "walk", " uauquer"}))) {
				await userMessage.Channel.SendMessageAsync("''Ãhhnnnn Michael Jackson já morreu...''");
				return;
			}
			if (messageString.Contains("ja morreu") && HasAtLeastOneWord(messageString, new[] {"michael", "maicow", " maycow", " maico", "jackson", "jackso", "jakso", "jakson", "jequiso", "jequison"})) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é a Julieta meu pau é o Romeu.''");
				return;
			}
			if (messageString.Contains("tu cu e a julieta") && messageString.Contains("meu pau e o romeu")) {
				await userMessage.Channel.SendMessageAsync("''Caraaalho, nada a vê.''");
				return;
			}
			if (messageString.StartsWith("nada a ve") || messageString == ("nada ve")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu pisca meu pau acende.''");
				return;
			}
			if (messageString.Contains("teu cu pisca") && messageString.Contains("meu pau acende")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é a Globo meu pau é o SBT.''");
				return;
			}
			if (messageString.Contains("teu cu e a globo") && messageString.Contains("meu pau e o sbt")) {
				await userMessage.Channel.SendMessageAsync("''Aahhh vai toma no cu.''");
				return;
			}
			if (messageString.Contains("toma no cu")) {
				await userMessage.Channel.SendMessageAsync("''Teu cu é o Pokemon meu pau é o Pikachu.''");
				return;
			}
			#endregion
			#endregion

			#region General
			
			if (messageString == "alguem ai") {
				await userMessage.Channel.SendMessageAsync("Eu. Mas sou um bot então não vou conseguir ter respostas para todas as suas perguntas.");
				return;
			}

			if (messageString.Contains("que horas sao")) {
				if (isQuestion) {
					await userMessage.Channel.SendMessageAsync("É hora de acertar as contas...");
					return;
				}
			}

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
				await userMessage.Channel.SendMessageAsync(userMessage.Author.Mention + ", o projeto foi pausado por tempo indeterminado. Veja mais detalhes no site: https://chrisdbhr.github.io/blackyeast");
				return;
			}
			#endregion

			#region Canal
			if (HasAllWords(messageString, new[] {"canal", "youtube", "chris"})) {
				await userMessage.Channel.SendMessageAsync("Se quer saber qual o canal do Chris o link é esse: https://www.youtube.com/christopher7");
				return;
			}

			if (messageString.Contains("chris") && HasAtLeastOneWord(messageString, new[] {"face", "facebook"})) {
				await userMessage.Channel.SendMessageAsync("O link para o Facebook do Chris é esse: https://www.facebook.com/chrisdbhr");
				return;
			}

			if (messageString.Contains("twitch") && HasAtLeastOneWord(messageString, new[] {"seu", "canal"})) {
				await userMessage.Channel.SendMessageAsync("O link para o Twitch do Chris é esse: https://www.twitch.tv/chrisdbhr");
				return;
			}
			#endregion
			#endregion

			#region Public Commands
			if (messageString.EndsWith("comandos desconhecidos")) {
				if (userSaidHerName) {
					string readFile = File.ReadAllText("unknownCommands.txt");
					if (readFile != null && readFile.Length > 0) {
						string trimmedMsg = "Quando alguém fala algo que eu não conheço eu guardo em uma lista para o Chris ver depois. Essa é a lista de comandos que podem vir a receber respostas futuramente: " + Environment.NewLine + "`" + readFile + "`";
						await userMessage.Channel.SendMessageAsync(trimmedMsg.Substring(0, 1999));
						return;
					}
				}
			}

			// Best animes list
			if (userSaidHerName) {
				if (messageString == ("add a lista de melhores animes")) {
					messageString = messageString.Replace("add a lista de melhores animes", "");
					string filePath = "Lists/bestAnimes.txt";
					messageString.Trim();
					string file = File.ReadAllText(filePath);

					// first, compare if the text to save its not to big
					if (messageString.Length > 48) {
						// ignore the message because it can be spam
						return;
					}

					// check if the txt its not biggen then 10mb
					FileInfo fileInfo = new FileInfo(file);
					if (fileInfo.Length > 10 * 1000000) {
						await userMessage.Channel.SendMessageAsync("<@203373041063821313> eu tentei adicionar o texto que o " + userMessage.Author.Mention + " digitou mas o arquivo de lista de melhores animes alcançou o tamanho limite. :sob:");
						return;
					}

					// see if the anime is already on the list
					if (file.Contains(messageString)) {
						await userMessage.Channel.SendMessageAsync("O anime " + @"`{messageString}` ja esta na lista de melhores animes.");
						return;
					}
					else {
						File.AppendAllText(filePath, Environment.NewLine + messageString);
						await userMessage.Channel.SendMessageAsync("Adicionado " + @"`{messageString}` a lista de melhores animes. :wink:");
						return;
					}
				}
			}
			#endregion

			#region Lists
			// Voice commands list
			if (messageString == "lista de sons" || messageString == "list" || messageString == "lista" || messageString == ",, help" || messageString == ",,help" || messageString == ",help") {
	
				int stringMaxLength = 1999;
				
				// get all texts
				await userMessage.Channel.SendMessageAsync($"**Posso tocar esses sons com o comando ';; nomeDoSom':**\n");//answerText.ToString().Substring(0, stringMaxLength - 3) + "...");
				
				string boxChar = "```\n";
				int titleAndBoxCharSize = boxChar.Length * 2;
				
				// get all names
				var allAnswers = new StringBuilder();
				foreach (string s in Directory.GetFiles("Voices/").Select(Path.GetFileNameWithoutExtension)) {
					allAnswers.Append(s);
					allAnswers.Append(" | ");
				}

				// for each group send a message
				var msg = new StringBuilder();
				float totalLength = (float)allAnswers.Length / (float)stringMaxLength;
				for (int i = 0; totalLength - i > 0; i++) {
					msg.Clear();
					int length = stringMaxLength - titleAndBoxCharSize;
					msg.Append(boxChar);
					msg.Append(allAnswers.ToString(length * i, length));
					msg.Append(boxChar);
					
					await userMessage.Channel.SendMessageAsync(msg.ToString());
				}
				
				return;
			}

			// Best Animes List
			if (userSaidHerName) {
				if (messageString == "best animes" || messageString == "melhores animes" || messageString == "lista de melhores animes" || messageString == "lista de animes bons" || messageString == "lista dos melhores animes") {
					string filePath = "Lists/bestAnimes.txt";
					string file = File.ReadAllText(filePath);
					if (!string.IsNullOrEmpty(file)) {
						// return the list
						await userMessage.Channel.SendMessageAsync("Lista de melhores animes:" + $"{file}");
					}
					else {
						// Create file if not exists
						File.WriteAllText(filePath, "");
					}
					return;
				}
			}

			//!!! THIS PART OF THE CODE BELOW MUST BE AS THE LAST BECAUSE:
			// see if user sayd only bot name on message with some other things and she has no answer yet
			if (userSaidHerName) {
				string unknownCommandsFileName = "Lists/unknownCommands.txt";
				string textToWrite = messageString + $"	({userMessage.Author.Username})";

				// first, compare if the text to save its not to big
				if (textToWrite.Length > 48) {
					// ignore the message because it can be spam
					return;
				}

				// check if the txt its not biggen then 10mb
				FileInfo fileInfo = new FileInfo(unknownCommandsFileName);
				if (fileInfo.Length > 10 * 1000000) {
					await userMessage.Channel.SendMessageAsync("<@203373041063821313> eu tentei adicionar o texto que o " + userMessage.Author.Username + " digitou mas o arquivo de lista de comandos alcançou o tamanho limite. :sob:");
					return;
				}

				// get text in string
				string fileContent = File.ReadAllText(unknownCommandsFileName);
				if (fileContent != null) {
					// only write if the unknown text is NOT already on the file
					if (!fileContent.Contains(messageString)) {
						File.AppendAllText(unknownCommandsFileName, textToWrite + Environment.NewLine);
						await userMessage.AddReactionAsync(new Emoji("❔"));
						return;
					}
				}
				else {
					File.AppendAllText(unknownCommandsFileName, textToWrite + Environment.NewLine);
					await userMessage.AddReactionAsync(new Emoji("❔"));
					return;
				}

				// return "Ainda não tenho resposta para isso:\n" + "`" + messageString + "`";
				return;
			}
			#endregion

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
			var cancellationTokenSource = new CancellationTokenSource();
			var timeline = await client.ExecuteAsync(request, cancellationTokenSource.Token);

			if (!string.IsNullOrEmpty(timeline.ErrorMessage)) {
				Console.WriteLine($"Error trying to get bot IP: {timeline.ErrorMessage}");
				return null;
			}
			if (string.IsNullOrEmpty(timeline.Content)) return null;
			return timeline.Content.Trim();
		}
		
		#endregion <<---------- Bot IP ---------->>
		
		
		

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
