using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using NyuBot.Extensions;

namespace NyuBot {
	public class AudioService {

		#region <<---------- Initializers ---------->>

		public AudioService(DiscordSocketClient discord) {
			this._disposable?.Dispose();
			this._disposable = new CompositeDisposable();

			discord = this._discord;
			
			////Todo implement
			// Observable.Timer(TimeSpan.FromSeconds(15)).Repeat().Subscribe(async _ => {
			// 	await this.CheckForRenaming();
			// }).AddTo(this._disposable);
		}
		
		#endregion <<---------- Initializers ---------->>
		
		
		
		
		#region <<---------- Properties ---------->>
		
		private const int AUDIO_BITRATE = 16000;
		private readonly ConcurrentDictionary<ulong, IAudioClient> ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();

		private readonly DiscordSocketClient _discord;
		private CompositeDisposable _disposable;

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
					await this.JoinAudio(sGuild, vcWithMorePeople);
					await this.SendAudioAsync(sGuild, fileName);
				}
			} catch (Exception e) {
				Console.WriteLine(e);
			}
		}
		
		#endregion <<---------- Static ---------->>

		
		

		#region <<---------- Public ---------->>

		public async Task JoinAudio(IGuild guild, IVoiceChannel target) {
			IAudioClient client;
			if (this.ConnectedChannels.TryGetValue(guild.Id, out client)) {
				return;
			}
			if (target.Guild.Id != guild.Id) {
				return;
			}

			var audioClient = await target.ConnectAsync();

			if (this.ConnectedChannels.TryAdd(guild.Id, audioClient)) {
				// If you add a method to log happenings from this service,
				// you can uncomment these commented lines to make use of that.
				//await Log(LogSeverity.Info, $"Connected to voice on {guild.Name}.");
			}
		}

		public async Task LeaveAudio(IGuild guild) {
			IAudioClient client;
			if (ConnectedChannels.TryRemove(guild.Id, out client)) {
				await client.StopAsync();

				//await Log(LogSeverity.Info, $"Disconnected from voice on {guild.Name}.");
			}
		}

		public async Task SendAudioAsync(IGuild guild, string path) {
			
			path = "Voices\\" + path + ".mp3";

			// Your task: Get a full path to the file if the value of 'path' is only a filename.
			if (!File.Exists(path)) {
				await Console.Out.WriteLineAsync($"Can't find file from path '{path}'");
				return;
			}
			
			IAudioClient client;
			if (ConnectedChannels.TryGetValue(guild.Id, out client)) {
				//await Log(LogSeverity.Debug, $"Starting playback of {path} in {guild.Name}");
				using (var ffmpeg = CreateProcess(path))
					using (var stream = client.CreatePCMStream(AudioApplication.Voice)) {
						try {
							await ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream);
						} catch (Exception e) {
							Console.WriteLine(e);
						}
						finally {
							await stream.FlushAsync();
						}
					}
			}
		}

		#endregion <<---------- Public ---------->>

		
		
		
		#region <<---------- Private ---------->>

		private Process CreateProcess(string path) {
			return Process.Start(new ProcessStartInfo {
				FileName = "ffmpeg.exe",
				Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -filter:a \"volume=0.4\" -ac 2 -f s16le -ar 48000 pipe:1",
				UseShellExecute = false,
				RedirectStandardOutput = true
			});
		}

		#endregion <<---------- Private ---------->>

		


		#region <<---------- Voice Renaming ---------->>

		private async Task CheckForRenaming() {
			var jsonArray = await JsonCache.LoadJsonAsync("Voice/DynamicVoiceChannels");
			if (jsonArray == null) return;

			for (int i = 0; i < jsonArray.Count; i++) {
				if (!(this._discord.GetChannel(Convert.ToUInt64(jsonArray[i].Value)) is IVoiceChannel voiceChannel)) continue;
				await this.DecideName(voiceChannel);
			}
		}

		private async Task DecideName(IVoiceChannel voiceChannel) {
			
			// get all users in voice channel async
			// var usersAsyncEnum = voiceChannel.GetUsersAsync();
			
			// //process users
			// foreach (var user in usersList) {
			// 	Console.WriteLine("Implement DecideName().process users");
			// }
			
		}
		
		#endregion <<---------- Voice Renaming ---------->>
		
	}
}
