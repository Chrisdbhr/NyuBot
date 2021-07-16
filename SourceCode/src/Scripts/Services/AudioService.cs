using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using NyuBot.Extensions;
using RestSharp;

namespace NyuBot {
	public class AudioService {

		#region <<---------- Initializers ---------->>

		public AudioService(DiscordSocketClient discord, LoggingService loggingService, IAudioService audioService) {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			this._discord = discord;
			this._log = loggingService;
			this._audioService = audioService;

			try {
				this._discord.Ready += () => this._audioService.InitializeAsync();
			} catch (Exception e) {
				this._log.Error(e.ToString()).CAwait();
			}
		}

		#endregion <<---------- Initializers ---------->>




		#region <<---------- Properties ---------->>

		private readonly DiscordSocketClient _discord;
		private readonly LoggingService _log;
		private readonly IAudioService _audioService;

		private CompositeDisposable _disposable;

		private const string BASE_AUDIOS_URL = "https://media.githubusercontent.com/media/Chrisdbhr/NyuBot/master/Voices/";

		#endregion <<---------- Properties ---------->>




		#region <<---------- Static ---------->>

		public async Task PlaySoundByNameOnAllMostPopulatedAudioChannels(string fileName) {
			try {
				foreach (var sGuild in this._discord.Guilds) {
					SocketVoiceChannel vcWithMorePeople = null;
					foreach (var vc in sGuild.VoiceChannels) {
						if (vcWithMorePeople == null) {
							vcWithMorePeople = vc;
							continue;
						}
						if (vc.Users.Count > vcWithMorePeople.Users.Count) {
							vcWithMorePeople = vc;
						}
					}
					if (vcWithMorePeople == null || vcWithMorePeople.Users.Count <= 0) continue;
					await this.SendAudioAsync(vcWithMorePeople, null, fileName);
				}
			} catch (Exception e) {
				await this._log.Error(e.ToString());
			}
		}

		#endregion <<---------- Static ---------->>




		#region <<---------- Public ---------->>

		public async Task<LavalinkPlayer> ConnectAudio(IVoiceChannel voiceChannel, SocketUserMessage userMessage = null) {
			if (voiceChannel == null) {
				if (userMessage != null) await userMessage.Channel.SendMessageAsync("Please join a voice channel first.");
				return null;
			}
			var guildId = voiceChannel.GuildId;
			return this._audioService.GetPlayer<LavalinkPlayer>(guildId) ?? await this._audioService.JoinAsync(guildId, voiceChannel.Id);
		}

		public async Task LeaveAudio(IGuild guild) {
			this._audioService.GetPlayer<LavalinkPlayer>(guild.Id)?.DisconnectAsync();
		}

		public async Task SendAudioAsync(SocketVoiceChannel voiceChannel, SocketUserMessage userMessage, string fileName) {
			var lavaLinkPlayer = await this.ConnectAudio(voiceChannel);

			if (!fileName.Contains(' ')) {
				;
				var url = BASE_AUDIOS_URL + ChatService.RemoveDiacritics(fileName) + ".mp3";
				if (await this.IsBotMemeFile(url)) {
					var tracks = await this._audioService.GetTracksAsync(url);
					foreach (var track in tracks) {
						await lavaLinkPlayer.PlayAsync(track);
						await lavaLinkPlayer.SetVolumeAsync(0.30f);
					}
					return;
				}
			}

			var ytTrack = (await this._audioService.GetTracksAsync(fileName, SearchMode.YouTube)).FirstOrDefault();
			if (ytTrack == null) {
				await userMessage.AddReactionAsync(new Emoji("❓"));
				return;
			}
			await lavaLinkPlayer.PlayAsync(ytTrack);
			await lavaLinkPlayer.SetVolumeAsync(0.30f);
		}

		public async Task StopAudioAsync(SocketVoiceChannel voiceChannel, ulong guildId) {
			await (this._audioService.GetPlayer<LavalinkPlayer>(guildId)).StopAsync();
		}

		#endregion <<---------- Public ---------->>

		private async Task<bool> IsBotMemeFile(string url) {
			var timeline = await new RestClient(url).ExecuteAsync(new RestRequest(Method.GET));
			return string.IsNullOrEmpty(timeline.ErrorMessage);
		}
		
	}
}
