using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace NyuBot {
	public class HungerGameService : IDisposable {

		#region <<---------- Classes ---------->>
		
		#endregion <<---------- Classes ---------->>
		
		
		

		#region <<---------- Properties ---------->>
		
		private readonly DiscordSocketClient _discord;

		#endregion <<---------- Properties ---------->>


		
		
		#region <<---------- Initializers ---------->>
		
		public HungerGameService(DiscordSocketClient discord) {
			this._discord = discord;
		}

		#endregion <<---------- Initializers ---------->>

		
		
		
		#region <<---------- General ---------->>
		
		public async Task NewHungerGameSimulation(IEnumerable<IUser> users) {
			foreach (var user in users) {
				
			}

		}
		
		#endregion <<---------- General ---------->>

		
		

		#region <<---------- Diposables ---------->>
		
		public void Dispose() {
			
		}

		#endregion <<---------- Diposables ---------->>

	}
}
