using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace NyuBot.Modules {
	public class AudioModule : ModuleBase<ICommandContext>
	{
		// Scroll down further for the AudioService.
		// Like, way down
		private readonly AudioService _service;

		// Remember to add an instance of the AudioService
		// to your IServiceCollection when you initialize your bot
		public AudioModule(AudioService service) {
			this._service = service;
		}

		// You *MUST* mark these commands with 'RunMode.Async'
		// otherwise the bot will not respond until the Task times out.
		[Command("join", RunMode = RunMode.Async)][Alias("j")]
		public async Task JoinCmd() {
			if (!(this.Context.User is IVoiceState voiceState)) return;
			await this._service.JoinAudio(this.Context.Guild, voiceState.VoiceChannel);
		}

		// Remember to add preconditions to your commands,
		// this is merely the minimal amount necessary.
		// Adding more commands of your own is also encouraged.
		[Command("leave", RunMode = RunMode.Async)][Alias("l")]
		public async Task LeaveCmd() {
			await this._service.LeaveAudio(this.Context.Guild);
		}
    
		[Command(",", RunMode = RunMode.Async)]
		public async Task PlayCmd([Remainder] params string[] song) {
			if (!(this.Context.User is IVoiceState voiceState)) return;
			await this._service.JoinAudio(this.Context.Guild, voiceState.VoiceChannel);
			await this._service.SendAudioAsync(this.Context.Guild, string.Join(string.Empty, song));
			//await this._service.LeaveAudio(this.Context.Guild);
		}
	}

}