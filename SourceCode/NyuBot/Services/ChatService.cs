using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace NyuBot {
	public class ChatService {

		private readonly CommandService _commands;
		private readonly DiscordSocketClient _discord;
		private readonly IServiceProvider _services;

		public ChatService(IServiceProvider services)
		{
			_commands = services.GetRequiredService<CommandService>();
			_discord = services.GetRequiredService<DiscordSocketClient>();
			_services = services;

			// Hook MessageReceived so we can process each message to see
			// if it qualifies as a command.
			_discord.MessageReceived += MessageReceivedAsync;
		}
		
		public async Task MessageReceivedAsync(SocketMessage rawMessage) {
			
			if (!(rawMessage is SocketUserMessage userMessage)) return;
			if (userMessage.Source != MessageSource.User) return;
			if (string.IsNullOrEmpty(userMessage.Content)) return;
			
			#region Setup message string to read
			
			// Content of the message in lower case string.
			string messageString = rawMessage.Content.ToLower();

			messageString = RemoveDiacritics(messageString);

			messageString = messageString.Trim();

			// if the message is a question
			bool isQuestion = false;
			if (messageString.Contains('?')) {
				// Get rid of all ?
				messageString = messageString.Replace("?", "");
				isQuestion = true;
			}
			bool userSaidHerName = false;
			// if user sayd her name
			if (HasAtLeastOneWord(messageString, new[] { "nyu", "nuy" })) {
				userSaidHerName = true;
				messageString = RemoveBotNameFromMessage(messageString);
			}
			else if (rawMessage.MentionedUsers.Contains(_discord.CurrentUser)) {
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

			#region Fast Answers

			// Fast Tests
			if (messageString == ("ping")) {
				await rawMessage.Channel.SendMessageAsync("pong");
				return;
			}
			if (messageString == ("pong")) {
				await rawMessage.Channel.SendMessageAsync("ping");
				return;
			}

			if (messageString == ("marco")) {
				await rawMessage.Channel.SendMessageAsync("polo");
				return;
			}
			if (messageString == ("polo")) {
				await rawMessage.Channel.SendMessageAsync("marco");
				return;
			}

			if (messageString == ("dotto")) {
				await rawMessage.Channel.SendMessageAsync("Dotto. :musical_note:");
				return;
			}

			if (messageString == "❤" || messageString == ":heart:") {
				await rawMessage.Channel.SendMessageAsync("❤");
				return;
			}

			if (messageString == ":broken_heart:" || messageString == "💔") {
				await rawMessage.Channel.SendMessageAsync("❤");
				await userMessage.AddReactionAsync(new Emoji(":cry:"));
				return;
			}

			if (messageString == ("ne") || messageString == ("neh")) {
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] { "Isso ai.", "Pode crê.", "Boto fé." }));
				return;
			}

			if (messageString == ("vlw") || messageString == ("valeu") || messageString == ("valew")) {
				await userMessage.AddReactionAsync(new Emoji(":wink:"));
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
					await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] { "Oi.", "Olá.", "Hello.", "Coé.", "Oin." }));
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
				await userMessage.Channel.SendMessageAsync(ChooseAnAnswer(new[] { "Tchau.", "Xiau.", "Bye bye.", "Flw." }));
				return;
			}

			if (messageString == ":frowning:"
				|| messageString == ":frowning2:"
				|| messageString == ":slight_frown:"
				) {

			}

			if (messageString.Contains("kk")) {
				if (Randomize().Next(100) < 20) {
					await userMessage.Channel.SendMessageAsync("kkk eae men.");
				}
				return;
			}

			#endregion

			#region Erase BotsCommands

			if (
				messageString.StartsWith(".") ||
				messageString.StartsWith(",") ||
				messageString.StartsWith(";;") ||
				messageString.StartsWith("!")
				) {
					await userMessage.AddReactionAsync(new Emoji(":x:"));
					await Task.Delay(1000 * 2); // 1 second
					await userMessage.DeleteAsync();
					return;
			}

			#endregion


			#region Nyu
			// check if user said nyu / nuy
			if (userSaidHerName) {
				if (HasAtLeastOneWord(messageString, new[] { "serve", "faz" })) {
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
					await userMessage.AddReactionAsync(new Emoji(":angry:"));
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
					await userMessage.AddReactionAsync(new Emoji( ":heart:"));
					return;
				}

				if (messageString == ("casa comigo")
					|| messageString == ("casa cmg")
					) {
					await rawMessage.CreateReactionAsync(DiscordEmoji.FromName(Client, ":thinking:"));
					return "Melhor não. Isso não iria dar certo.";
				}

				if (messageString.Contains("responde") && messageString.Contains("tudo")) {
					if (isQuestion) {
						await rawMessage.CreateReactionAsync(DiscordEmoji.FromName(Client, ":relaxed:"));
						return "Sim. Eu tento.";
					}
				}

				if (messageString.Contains("manda nude")) {
					await rawMessage.CreateReactionAsync(DiscordEmoji.FromName(Client, ":lennyFace:"));
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
				return e.Author.Mention + " Gay.";
			}

			if (messageString.Contains("melhor anime")) {
				if (isQuestion) {
					return "Elfen Lied é o melhor anime. E ponto.";
				}
			}

			if (messageString.Contains("pior anime")) {
				if (isQuestion) {
					return "Ainda não sei qual o pior anime.";
				}
			}

			#endregion

			#region Memes
			// Ahhh agora eu entendi
			if (messageString.EndsWith("agora eu entendi")) {
				return ChooseAnAnswer(new[] { "Agora eu saqueeeeei!", "Agora tudo faz sentido!", "Eu estava cego agora estou enchergaaaando!", "Agora tudo vai mudar!", "Agora eu vou ficar de olhos abertos!" });

			}

			// react to gemiao do zap
			if (messageString.Contains("gemidao")) {
				await rawMessage.CreateReactionAsync(DiscordEmoji.FromName(Client, ":rolling_eyes:"));
				return "Lá vem...";
			}

			#region Teu cu na minha mao
			// all possible answers
			if (messageString.Contains("mo vacilao") || messageString.Contains("mo vacilaum")) {
				return "''Hmmmm vacilão... Teu cu na minha mao.''";
			}
			if (messageString.Contains("teu cu na minha mao")) {
				return "''Teu cu e o aeroporto meu pau e o avião.''";
			}
			if (messageString.Contains("teu cu e o aeroporto meu pau e o aviao")) {
				return "''Teu cu é a garagem meu pau é o caminhão.''";
			}
			if (messageString.Contains("teu cu e a garagem meu pau e o caminhao")) {
				return "''Teu cu é a Carminha meu pau é o Tufão (ãnh?).''";
			}
			if (HasAllWords(messageString, new[] { "teu cu", "meu pau", "tufao" })) {
				return "''Teu cu é o mar meu pau é o tubarão.''";
			}
			if (messageString.Contains("teu cu e o mar meu pau e o tubarao")) {
				return "''Teu cu é o morro meu pau é o Complexo do Alemão.''";
			}
			if (messageString.Contains("teu cu e o morro meu pau e o complexo do alemao")) {
				return "''Caraaalho, sem nexo.''";
			}
			if (messageString.Contains("sem nexo")) {
				return "''Teu cu é o cabelo meu pau é o reflexo.''";
			}
			if (HasAllWords(messageString, new[] { "teu cu e o cabelo", "meu pau e reflexo" })) {
				return "''Teu cu é o Moon Walker meu pau é o Michael Jackson.''";
			}
			if (HasAllWords(messageString, new[] { "teu cu e o", "meu pau e o" })
				&& (HasAtLeastOneWord(messageString, new[] { "michael", "mickael", "maicow", " maycow", " maico", "jackson", "jackso", "jakso", "jakson", "jequiso", "jequison" })
				|| HasAtLeastOneWord(messageString, new[] { " moon ", " mun ", "walker", "walk", " uauquer" }))) {
				return "''Ãhhnnnn Michael Jackson já morreu...''";
			}
			if (messageString.Contains("ja morreu") && HasAtLeastOneWord(messageString, new[] { "michael", "maicow", " maycow", " maico", "jackson", "jackso", "jakso", "jakson", "jequiso", "jequison" })) {
				return "''Teu cu é a Julieta meu pau é o Romeu.''";
			}
			if (messageString.Contains("tu cu e a julieta") && messageString.Contains("meu pau e o romeu")) {
				return "''Caraaalho, nada a vê.''";
			}
			if (messageString.StartsWith("nada a ve") || messageString == ("nada ve")) {
				return "''Teu cu pisca meu pau acende.''";
			}
			if (messageString.Contains("teu cu pisca") && messageString.Contains("meu pau acende")) {
				return "''Teu cu é a Globo meu pau é o SBT.''";
			}
			if (messageString.Contains("teu cu e a globo") && messageString.Contains("meu pau e o sbt")) {
				return "''Aahhh vai toma no cu.''";
			}
			if ((messageString.Contains("toma no cu"))) {
				return "''Teu cu é o Pokemon meu pau é o Pikachu.''";
			}

			#endregion


			#endregion

			#region General
			if (messageString == "alguem ai") {
				return "Eu. Mas sou um bot então não vou conseguir ter respostas para todas as suas perguntas.";

			}

			if (messageString.Contains("que horas sao")) {
				if (isQuestion) {
					return "É hora de acertar as contas...";
				}
			}

			if (messageString.Contains("a justica")) {
				if (isQuestion) {
					return "Vem de cima!";
				}
			}

			#endregion

			#region Insults
			// Answer to insults 

			if (messageString.Contains("bot lixo")
				|| messageString.Contains("suamaeeminha")
				) {
				await rawMessage.CreateReactionAsync(DiscordEmoji.FromName(Client, ":eyes:"));
				return "Algum problema " + rawMessage.Author.Mention + "?";
			}

			#endregion

			#region Links

			#region Black Yeast
			// Firsts

			if (HasAllWords(messageString, new[] { "black", "yeast" })) {
				// user is speaking about Black Yeast.

				// if user is asking about release date.
				if (messageString.Contains("quando") && HasAtLeastOneWord(messageString, new[] { "lanca", "sai" })) {
					return "2018 ~ 2019 " + e.Author.Mention + ". Esse ano vai ser lançada uma versão testes pública do jogo.";
				}

				// asking for the site
				if (messageString.Contains("patreon")) {
					return e.Author.Mention + " esse é o link para o **Patreon** do Black Yeast: https://www.patreon.com/BlackYeastGame \nlá as pessoas podem contribuir financeiramente para o desenvolvimento do projeto.";
				}

				// asking for the blog
				if (messageString.Contains("blog")) {
					return e.Author.Mention + " esse é o link para o **blog** do Black Yeast: https://blackyeast.wordpress.com \nlá tem vários links para o fórum, Patreon e outras informações do projeto. :3";
				}

				// asking for the blog
				if (messageString.Contains("forum")) {
					return e.Author.Mention + " esse é o link para o **forum** do Black Yeast: https://www.reddit.com/r/blackyeast \nAgora no Reddit, fica melhor e mais fácil de postar e se achar.";
				}

				// asking for the site
				if (messageString.Contains("site")) {
					return e.Author.Mention + " esse é o link para o **site** do Black Yeast: http://blackyeast.github.io \nLá tem várias informações e links para as outras redes do projeto!";
				}

				// if its not one of that
				// ignore it
			}

			#endregion

			#region Canal
			if (HasAllWords(messageString, new[] { "canal", "youtube", "chris" })) {
				return "Se quer saber qual o canal do Chris o link é esse: https://www.youtube.com/christopher7";
			}

			if (messageString.Contains("chris") && HasAtLeastOneWord(messageString, new[] { "face", "facebook" })) {
				return "O link para o Facebook do Chris é esse: https://www.facebook.com/chrisdbhr";
			}

			if (messageString.Contains("twitch") && HasAtLeastOneWord(messageString, new[] { "seu", "canal" })) {
				return "O link para o Twitch do Chris é esse: https://www.twitch.tv/chrisdbhr";
			}
			#endregion

			#endregion

			#region Public Commands

			if (messageString.EndsWith("comandos desconhecidos")) {
				if (userSaidHerName) {
					string readFile = File.ReadAllText("unknownCommands.txt");
					if (readFile != null && readFile.Length > 0) {
						return "Quando alguém fala algo que eu não conheço eu guardo em uma lista para o Chris ver depois. Essa é a lista de comandos que podem vir a receber respostas futuramente: " + Environment.NewLine + "`" + readFile + "`";
					}
				}
			}

			// Best animes list
			if (userSaidHerName) {
				if (messageString == ("add a lista de melhores animes")) {
					messageString.Replace("add a lista de melhores animes", "");
					string filePath = "Lists/bestAnimes.txt";
					messageString.Trim();
					string file = File.ReadAllText(filePath);

					// first, compare if the text to save its not to big
					if (messageString.Length > 48) {
						// ignore the message because it can be spam
						return null;
					}

					// check if the txt its not biggen then 10mb
					FileInfo fileInfo = new FileInfo(file);
					if (fileInfo.Length > 10 * 1000000) {
						return "<@203373041063821313> eu tentei adicionar o texto que o " + e.Author.Username + " digitou mas o arquivo de lista de melhores animes alcançou o tamanho limite. :sob:";
					}
					// see if the anime is already on the list
					if (file.Contains(messageString)) {
						return "O anime " + @"`{messageString}` ja esta na lista de melhores animes.";
					}
					else {
						File.AppendAllText(filePath, Environment.NewLine + messageString);
						return "Adicionado " + @"`{messageString}` a lista de melhores animes. :wink:";
					}
				}
			}


			#endregion

			#region Lists

			// Voice commands list
			if (messageString == "lista de sons" || messageString == "list" || messageString == "lista" || messageString == ",, help" || messageString == ",,help" || messageString == ",help") {
                int stringMaxLength = 1999;
                StringBuilder answerText = new StringBuilder(stringMaxLength);
				answerText.AppendLine("**Posso tocar todos esses sons, toque eles com o comando ',, nomeDoSom':**");
				answerText.AppendLine("```");
				foreach (string s in Directory.GetFiles("Voices/").Select(Path.GetFileNameWithoutExtension)) {
                    if (s.Length >= stringMaxLength) break;
					answerText.Append(s);
					answerText.Append(" | ");
				}
				answerText.AppendLine("```");
				return answerText.ToString().Substring(0, stringMaxLength - 3) + "...";
			}

			// Best Animes List
			if (userSaidHerName) {
				if (messageString == "best animes" || messageString == "melhores animes" || messageString == "lista de melhores animes" || messageString == "lista de animes bons" || messageString == "lista dos melhores animes") {
					string filePath = "Lists/bestAnimes.txt";
					string file = File.ReadAllText(filePath);
					if (!string.IsNullOrEmpty(file)) {
						// return the list
						return "Lista de melhores animes:" + $"{file}";
					}
					else {
						// Create file if not exists
						File.WriteAllText(filePath, "");
					}
				}
			}

			//! MUST BE AT THE LAST
			// see if user sayd only bot name on message with some other things and she has no answer yet
			if (userSaidHerName) {
				string unknownCommandsFileName = "Lists/unknownCommands.txt";
				string textToWrite = messageString + $"	({e.Author.Username})";
				// first, compare if the text to save its not to big
				if (textToWrite.Length > 48) {
					// ignore the message because it can be spam
					return null;
				}

				// check if the txt its not biggen then 10mb
				FileInfo fileInfo = new FileInfo(unknownCommandsFileName);
				if (fileInfo.Length > 10 * 1000000) {
					return "<@203373041063821313> eu tentei adicionar o texto que o " + e.Author.Username + " digitou mas o arquivo de lista de comandos alcançou o tamanho limite. :sob:";
				}

				// get text in string
				string fileContent = File.ReadAllText(unknownCommandsFileName);
				if (fileContent != null) {
					// only write if the unknown text is NOT already on the file
					if (!fileContent.Contains(messageString)) {
						File.AppendAllText(unknownCommandsFileName, textToWrite + Environment.NewLine);
						await rawMessage.CreateReactionAsync(DiscordEmoji.FromName(Client, ":grey_question:"));
					}
				}
				else {
					File.AppendAllText(unknownCommandsFileName, textToWrite + Environment.NewLine);
					await rawMessage.CreateReactionAsync(DiscordEmoji.FromName(Client, ":grey_question:"));
				}
				// return "Ainda não tenho resposta para isso:\n" + "`" + messageString + "`";
				return null;
			}
			#endregion

			// the message has no answer.
			return null;
		}

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

		/// <summary>
		/// Create a new random object and return it.
		/// </summary>
		public static Random Randomize() {
			return new Random();
		}

	}

	}
