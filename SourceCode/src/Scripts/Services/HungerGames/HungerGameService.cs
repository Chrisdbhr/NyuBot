using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NyuBot.Extensions;
using SimpleJSON;

namespace NyuBot.HungerGames {
	public class HungerGameService : IDisposable {

		#region <<---------- Properties ---------->>
		
		private readonly DiscordSocketClient _discord;
		private readonly TimeSpan _timeToWaitEachMessage = TimeSpan.FromSeconds(5);

		#endregion <<---------- Properties ---------->>


		
		
		#region <<---------- Initializers ---------->>
		
		public HungerGameService(DiscordSocketClient discord) {
			this._discord = discord;
		}

		#endregion <<---------- Initializers ---------->>


		

		#region <<---------- JSON Keys ---------->>

		private const string JKEY_CHANNEL_MATCHS_INFO_PREFIX = "Games/HungerGames/";
		
		#endregion <<---------- JSON Keys ---------->>


		
		
		#region <<---------- Enums ---------->>

		private enum GameEndReason {
			victory, allDied, timeOut
		}
		
		#endregion <<---------- Enums ---------->>
		
		
		
		
		#region <<---------- General ---------->>
		
		public async Task NewHungerGameSimulation(SocketCommandContext context, IReadOnlyCollection<IUser> users, int numberOfPlayers) {
			var characters = new List<Character>();
			foreach (var user in users) {
				if (!(user is SocketGuildUser guildUser)) continue;
				if (guildUser.IsBot || guildUser.Status != UserStatus.Online) continue;
				characters.Add(new Character {
					User = guildUser
				});
			}

			if (characters.Count <= 1) return;

			characters = characters.Take(numberOfPlayers).ToList();
			
			// new match
			var embed = new EmbedBuilder {
				Color = Color.Green,
				Title = $"Nova partida de Hunger Games (Battle Royale)",
				Description = "Considerando apenas usuários ONLINE"
			};
			embed.AddField("Vivos", characters.Count(x => !x.IsDead), true);
			embed.AddField("Participantes", characters.Count, true);
			
			// footer version
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			if (version == null) version = new Version(1, 0, 0);
			var build = version.Build;
			embed.WithFooter($"Hunger Games & Battle Royale Simulation - © CHRISdbhr", "https://chrisdbhr.github.io/images/avatar.png");

			await context.Channel.SendMessageAsync(string.Empty, false, embed.Build());
			
			// save that match is running
			var json = await JsonCache.LoadJsonAsync($"{JKEY_CHANNEL_MATCHS_INFO_PREFIX}{context.Channel}") ?? new JSONObject();
			json["matchInProgress"] = true;
			await JsonCache.SaveJsonAsync($"{JKEY_CHANNEL_MATCHS_INFO_PREFIX}{context.Channel}", json);
			
			// game task
			await this.ProcessTurn(context, characters);
			
			// game finished
			json = await JsonCache.LoadJsonAsync($"{JKEY_CHANNEL_MATCHS_INFO_PREFIX}{context.Channel}");
			if (json == null) return;
			json["matchInProgress"] = false;
		}

		private async Task ProcessTurn(SocketCommandContext context, IReadOnlyCollection<Character> allCharacters) {

			// settings
			int maxTurns = 100;
	
			// game loop
			int currentTurn = 1;
			while (currentTurn < maxTurns) {
				EmbedBuilder embed;
	
				// is game canceled?
				var matchJson = await JsonCache.LoadJsonAsync($"{JKEY_CHANNEL_MATCHS_INFO_PREFIX}{context.Channel}");
				if (matchJson != null && matchJson["matchInProgress"] == false) {
					embed = new EmbedBuilder {
						Color = Color.Orange,
						Title = $"Jogo cancelado"
					};

					await Task.Delay(this._timeToWaitEachMessage);
					await context.Channel.SendMessageAsync(string.Empty, false, embed.Build());
					return;
				}
				
				
				// new turn
				embed = new EmbedBuilder {
					Color = Color.Default,
					Title = $"Rodada #{currentTurn}"
				};
				embed.AddField("Vivos", allCharacters.Count(x => !x.IsDead), true);
				embed.AddField("Participantes", allCharacters.Count, true);

				var mostKill = allCharacters.Aggregate((i1,i2) => i1.Kills > i2.Kills ? i1 : i2);
				if (mostKill != null && mostKill.Kills > 1){
					embed.AddField(mostKill.User.GetNameSafe(), $"matou mais, com {mostKill.Kills} mortes");
				}

				await Task.Delay(this._timeToWaitEachMessage);
				await context.Channel.SendMessageAsync(string.Empty, false, embed.Build());
				
				
				// foreach character
				foreach (var currentChar in allCharacters) {
					if (currentChar == null || currentChar.IsDead) continue;
					
					// get all alive
					var alive = allCharacters.Where(x => x != null && !x.IsDead).ToArray();
				
					// check for finish conditions
					if (alive.Length == 1) {
						// winner
						await this.EndGame(context, GameEndReason.victory, allCharacters, alive[0]);
						return;
					}

					// everyone died
					if(alive.Length <= 0) {
						await this.EndGame(context, GameEndReason.allDied, allCharacters);
						return;
					}
					
					await Task.Delay(this._timeToWaitEachMessage);
					await currentChar.Act(context, allCharacters);
				}
				currentTurn += 1;
			}

			
			// max turns reached, end game
			await this.EndGame(context, GameEndReason.timeOut, allCharacters);
		}

		private async Task EndGame(SocketCommandContext context, GameEndReason reason, IReadOnlyCollection<Character> allCharacters, Character winner = null) {
			await Task.Delay(this._timeToWaitEachMessage);
			
			var embed = new EmbedBuilder {
				Title = "Fim de Jogo!"
			};
			

			switch (reason) {
				case GameEndReason.victory:
					embed.Color = Color.Green;
					
					// winner info
					if (winner == null) break;

					embed.WithThumbnailUrl(winner.User.GetAvatarUrl());

					embed.AddField("Vencedor", winner.User.Mention, true);
					
					// kills
					if (winner.Kills > 0) {
						embed.AddField("Mortes", $"Matou {winner.Kills} no total", true);
					}
					else {
						embed.AddField("Mortes", $"Ganhou sem matar ninguém!", true);
					}

					break;
				case GameEndReason.timeOut:
					embed.Color = Color.DarkOrange;
					embed.AddField("Catástrofe!", "Uma bomba atômica caiu e matou a todos!");
					break;
				case GameEndReason.allDied:
				default: // all died
					embed.Color = Color.DarkOrange;
					embed.AddField("Todos morreram", "Ninguém ganhou");
					break;
			}
			
			// more kills
			var mostKill = allCharacters.Aggregate((i1,i2) => i1.Kills > i2.Kills ? i1 : i2);
			if (mostKill != null && mostKill.Kills > 1) {
				var name = mostKill.User.GetNameBoldSafe();
				embed.AddField("Matador", $"{name} matou mais nessa partida, com {mostKill.Kills} mortes");
			}

			await context.Channel.SendMessageAsync(string.Empty, false, embed.Build());
		}

		#endregion <<---------- General ---------->>

		
		

		#region <<---------- Diposables ---------->>
		
		public void Dispose() {
			
		}

		#endregion <<---------- Diposables ---------->>

	}
}
