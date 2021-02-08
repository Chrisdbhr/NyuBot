using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using NyuBot.Extensions;

namespace NyuBot {
	public class VoiceService {

		#region <<---------- Initializers ---------->>

		public VoiceService(DiscordSocketClient discord, LoggingService loggingService) {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			this._discord = discord;
			this._log = loggingService;

			this._discord.UserVoiceStateUpdated += this.OnUserVoiceChannelStateUpdate;
		}

		#endregion <<---------- Initializers ---------->>




		#region <<---------- Properties ---------->>

		private readonly DiscordSocketClient _discord;
		private readonly LoggingService _log;
		private CompositeDisposable _disposable;

		#endregion <<---------- Properties ---------->>




		#region <<---------- Callbacks ---------->>
		
		private async Task OnUserVoiceChannelStateUpdate(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState) {
			if (!(user is SocketGuildUser guildUser)) return;

			var sb = new StringBuilder();
			
			sb.Append($"[{guildUser.Guild?.Name}] ");
			sb.Append(guildUser.GetNameAndAliasSafe());

			// saiu do canal de voz
			if (oldState.VoiceChannel != null && newState.VoiceChannel == null) {
				sb.Append($" saiu do canal de voz [{oldState.VoiceChannel.Name}]");
			}

			// entrou no canal de voz
			else if (oldState.VoiceChannel == null && newState.VoiceChannel != null) {
				sb.Append($" entrou no canal de voz [{newState.VoiceChannel.Name}]");
			}
			
			else if (oldState.VoiceChannel != newState.VoiceChannel && oldState.VoiceChannel != null && newState.VoiceChannel != null) {
				sb.Append($" mudou do [{oldState.VoiceChannel.Name}] para [{newState.VoiceChannel.Name}]");
			}
			else {
				return;
			}

			await this._log.Info(sb.ToString());
		}
		
		#endregion <<---------- Callbacks ---------->>
		
	}
}
