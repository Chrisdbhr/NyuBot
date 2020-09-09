using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using NyuBot.HungerGames;

namespace NyuBot.Modules {
	[Name("Hunger Games")]
	public class HungerGameModule : ModuleBase<SocketCommandContext> {
	
		private readonly HungerGameService _service;

		public HungerGameModule(HungerGameService service) {
			this._service = service;
		}

		[Command("newhungergame"), Alias("nhg")]
		[Summary("Start a new Hunger Game simulation")]
		public async Task NewHungerGameSimulation() {
			if (this.Context?.Channel == null) return;
			var matchJsonNode = await JsonCache.LoadJsonAsync($"Games/HungerGames/{this.Context.Channel}");
			if (matchJsonNode != null) return; // already playing

			var usersAsyncEnum = this.Context.Channel.GetUsersAsync()?.GetEnumerator();
			if (usersAsyncEnum == null) return;
			var moved = await usersAsyncEnum.MoveNext();
			if (!moved) return;

			var usersList = usersAsyncEnum.Current;
			if (!usersList.Any()) return;

			int numberOfPlayers = 100;
			
			await this._service.NewHungerGameSimulation(this.Context, usersList, numberOfPlayers);
		}
		
		[Command("stophungergame"), Alias("shg")]
		[Summary("Stop the Hunger Game simulation running in this channel")]
		public async Task StopHungerGameSimulation() {
			if (this.Context?.Channel == null) return;
			var deleted = await JsonCache.DeleteAsync($"Games/HungerGames/{this.Context.Channel}");
			if (!deleted) return;
			await this.ReplyAsync($"*Game will be canceled...*");
		}


		
	}
}
