using System;
using System.Linq;
using System.Threading;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using NyuBot.Extensions;
using RestSharp;
using RestSharp.Extensions;

namespace NyuBot.Modules {
    
    [Name("Moderator")]
    [RequireContext(ContextType.Guild)]
    public class ModeratorModule : ModuleBase<SocketCommandContext> {

        [Command("say"), Alias("s")]
        [Summary("Make the bot say something")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public Task Say([Remainder]string text) => this.ReplyAsync(text);
        
        [Group("set"), Name("Set commands")]
        [RequireContext(ContextType.Guild)]
        public class Set : ModuleBase
        {
            [Command("nick"), Priority(1)]
            [Summary("Change your nickname to the specified text")]
            [RequireUserPermission(GuildPermission.ChangeNickname)]
            public Task Nick([Remainder]string name) => this.Nick(this.Context.User as SocketGuildUser, name);

            [Command("nick"), Priority(0)]
            [Summary("Change another user's nickname to the specified text")]
            [RequireUserPermission(GuildPermission.ManageNicknames)]
            public async Task Nick(SocketGuildUser user, [Remainder]string name)
            {
                await user.ModifyAsync(x => x.Nickname = name);
                await this.ReplyAsync($"{user.Mention} I changed your name to **{name}**");
            }
        }
        
        [Command("kick")]
        [Summary("Kick the specified user.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task Kick([Remainder]SocketGuildUser user)
        {
            await this.ReplyAsync($"cya {user.Mention} :wave:");
            await user.KickAsync();
        }

        [Command("rc"), Alias("renamevoicechannel")]
        [Summary("Renames a voice channel that user is in.")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task RenameVoiceChannel(params string[] newNameArray) {
            if (newNameArray.Length <= 0) return;
            if (!(this.Context.User is SocketGuildUser user)) return;
            var vc = user.VoiceChannel;
            if (vc == null) return;
            var newName = string.Join(' ', newNameArray);
            newName = newName.FirstCharToUpper();
            await vc.ModifyAsync(p => p.Name = newName);
            await this.Context.Message.AddReactionAsync(new Emoji("✔"));
            await Task.Delay(TimeSpan.FromSeconds(5));
            await this.Context.Message.DeleteAsync();
        }

        [Command("randomimg")]
        public async Task GetRandomImg() {
            
            var client = new RestClient("https://picsum.photos/96");
            var request = new RestRequest(Method.GET);
            var timeline = await client.ExecuteAsync(request, CancellationToken.None);
            
            var embed = new EmbedBuilder {
                Title = "Random image",
                Description = "from picsum.photos",
                ThumbnailUrl = timeline.ResponseUri.OriginalString
            };

            await this.ReplyAsync("", false, embed.Build());
        }
        
    }
}
