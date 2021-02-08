using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace NyuBot.Modules {
	public class AudioModule : ModuleBase<ICommandContext>
	{
		private readonly AudioService _service;

		public AudioModule(AudioService service) {
			this._service = service;
		}

		[Command("join")][Alias("j")]
		public async Task JoinCmd() {
			if (!(this.Context.User is SocketGuildUser voiceState)) return;
			await this._service.ConnectAudio(voiceState.VoiceChannel, this.Context.Message as SocketUserMessage);
		}

		[Command("leave")][Alias("l")]
		public async Task LeaveCmd() {
			await this._service.LeaveAudio(this.Context.Guild);
		}
    
		[Command("play")][Alias("p")]
		[Summary("Plays a song in voice channel")]
		public async Task PlayCmd(params string[] song) {
			if (!(this.Context.User is SocketGuildUser voiceState)) return;
			await this._service.SendAudioAsync(voiceState.VoiceChannel, this.Context.Message as SocketUserMessage, string.Join(' ', song));
		}
		
		[Command("stop")]
		[Summary("Stop song playing")]
		public async Task Stop() {
			if (!(this.Context.User is SocketGuildUser voiceState)) return;
			await this._service.StopAudioAsync(voiceState.VoiceChannel, this.Context.Guild.Id);
		}

	}

}