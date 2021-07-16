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
			var channelId = this.Context.Channel.Id;
			if (this.Context.Guild.Id == 798667749081481226 && channelId != 802832949460336660) return;
			
			await this.StopHungerGameSimulation();
			if (this._service.PlayingChannels.Contains(this.Context.Channel.Id)) return; // already playing

			var usersAsyncEnum = this.Context.Channel.GetUsersAsync()?.GetAsyncEnumerator();
			if (usersAsyncEnum == null) return;
			var moved = await usersAsyncEnum.MoveNextAsync();
			if (!moved) return;

			var usersList = usersAsyncEnum.Current;
			if (!usersList.Any()) return;

			int numberOfPlayers = 500;

			this._service.PlayingChannels.Add(channelId);
			await this._service.NewHungerGameSimulation(this.Context, usersList, numberOfPlayers);
			this._service.PlayingChannels.Remove(channelId);
		}
		
		[Command("stophungergame"), Alias("shg")]
		[Summary("Stop the Hunger Game simulation running in this channel")]
		public async Task StopHungerGameSimulation() {
			if (this.Context?.Channel == null) return;
			var channelId = this.Context.Channel.Id;
			if (!this._service.PlayingChannels.Remove(channelId)) return;
			await this.ReplyAsync($"*Game will be canceled...*");
		}


		
	}
}
