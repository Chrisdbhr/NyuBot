using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using NyuBot.Extensions;

namespace NyuBot {
	public class VoiceService {

		#region <<---------- Initializers ---------->>

		public VoiceService(DiscordSocketClient discord) {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			this._discord = discord;

			Observable.Timer(TimeSpan.FromHours(1)).Repeat().Subscribe(async _ => {
				await this.CheckForRenameVoiceChannels();
			}).AddTo(this._disposable);
		}

		#endregion <<---------- Initializers ---------->>




		#region <<---------- Properties ---------->>

		private readonly DiscordSocketClient _discord;
		private CompositeDisposable _disposable;

		#endregion <<---------- Properties ---------->>



		#region <<---------- Repeating ---------->>

		private async Task CheckForRenameVoiceChannels() {
			
		}

		#endregion <<---------- Repeating ---------->>
		
	}
}
