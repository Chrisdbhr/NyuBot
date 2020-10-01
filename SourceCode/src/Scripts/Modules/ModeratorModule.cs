using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace NyuBot.Modules
{
    [Name("Moderator")]
    [RequireContext(ContextType.Guild)]
    public class ModeratorModule : ModuleBase<SocketCommandContext>
    {
        [Command("kick")]
        [Summary("Kick the specified user.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task Kick([Remainder]SocketGuildUser user)
        {
            await this.ReplyAsync($"cya {user.Mention} :wave:");
            await user.KickAsync();
        }

        [Command("rvc"), Alias("renamevoicechannel")]
        [Summary("Renames a voice channel that user is in.")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task RenameVoiceChannel(string newVcName) {
            if (string.IsNullOrEmpty(newVcName)) return;
            if (!(this.Context.User is SocketGuildUser user)) return;
            var vc = user.VoiceChannel;
            if (vc == null) return;
            var oldName = vc.Name;
            await vc.ModifyAsync(p => p.Name = newVcName);
            
            var embed = new EmbedBuilder {
                Title = "Canal de voz renomeado",
                Description = $"**{oldName}** renomeado para **{newVcName}**"
            };

            await this.ReplyAsync("", false, embed.Build());
        }
    }
}
