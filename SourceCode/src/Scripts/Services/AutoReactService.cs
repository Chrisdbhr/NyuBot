using System.Threading.Tasks;
using Discord.WebSocket;

namespace NyuBot {
	public class AutoReactService {

		public AutoReactService(DiscordSocketClient discord) {
			this._discord = discord;
			this._discord.MessageReceived += this.OnMessageReceived;
		}
		

		
		
		private readonly DiscordSocketClient _discord;

		
		
		
		private async Task OnMessageReceived(SocketMessage message) {
			if (message.Attachments.Count <= 0) return;
			
		}
		
	}
}
