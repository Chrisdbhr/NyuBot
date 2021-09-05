using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using NyuBot.Extensions;

namespace NyuBot {
	public class SleepService {

		public SleepService(DiscordSocketClient discord, DatabaseService db, LoggingService loggingService) {
			return;
			this._discord = discord;
			this._db = db;
			this._log = loggingService;
			this._discord.MessageReceived += this.MessageReceived;
			this._discord.UserUpdated += async (oldUser, newUser) => {
				if (!(newUser is SocketGuildUser socketGuildUser)) return;
				if (socketGuildUser.IsBot || socketGuildUser.IsWebhook) return;
				if (oldUser.Status == UserStatus.Offline && newUser.Status != UserStatus.Offline) {
					await this.InformUserWakeUp(socketGuildUser);
				}
			};
			this._discord.UserVoiceStateUpdated += async (user, oldState, newState) => {
				if (!(user is SocketGuildUser socketGuildUser)) return;
				if (oldState.VoiceChannel == null && newState.VoiceChannel != null) {
					await this.InformUserWakeUp(socketGuildUser);
				}
			};
		}

		private const string COLLECTION_AMIMIR = "amimir";
		private readonly DiscordSocketClient _discord;
		private readonly DatabaseService _db;
		private readonly LoggingService _log;
		
		
		
		
		private async Task MessageReceived(SocketMessage socketMessage) {
			if (!(socketMessage is SocketUserMessage userMessage)) return;
			if (userMessage.Source != MessageSource.User) return;

			// check user sleeping
			if (userMessage.Content?.ToLower().Replace(" ", string.Empty) == "amimir") {
				await this.SetUserIsSleeping(userMessage);
				return;
			}

			await this.InformUserWakeUp(userMessage.Author as SocketGuildUser);
		}

		private async Task SetUserIsSleeping(SocketUserMessage msg) {
			var filter = Builders<BsonDocument>.Filter.Eq("idUser", msg.Author.Id.ToString());

			var update = Builders<BsonDocument>.Update
											   .Set("isSleeping", true)
											   .Set("channelId", msg.Channel.Id.ToString())
											   .Set("sleepStartTime", DateTime.UtcNow.AddHours(-3));
			
			var a = await this._db.UpdateData(COLLECTION_AMIMIR, filter, update);
			if (!a.IsAcknowledged) {
				await msg.AddReactionAsync(new Emoji("❌"));
				await this._log.Error("Error included Bson on db:\n "+update.ToBsonDocument());
				return;
			}
			
			await msg.AddReactionAsync(new Emoji("💤"));
		}
		
		private async Task InformUserWakeUp(SocketGuildUser user) {
			if (user == null) return;
			
			var filter = Builders<BsonDocument>.Filter.Eq("idUser", user.Id.ToString());
			var data = await this._db.GetData(COLLECTION_AMIMIR, filter);
			if (data == null) return;
			if (!data["isSleeping"].AsBoolean) return;
			
			var sleepTime = data["sleepStartTime"].ToUniversalTime();
			var totalSleepTime = DateTime.UtcNow.AddHours(-3) - sleepTime;
			
			var embed = new EmbedBuilder {
				Title = $"Parece que {user.GetNameSafe()} acordou",
				Description = $"{user.Mention} dormiu um total de: {totalSleepTime.ToString(@"hh\:mm")}",
				ThumbnailUrl = user.GetAvatarUrlSafe()
			};
			embed.Color = Color.LightOrange;
			
			embed.AddField(new EmbedFieldBuilder {
				Name = "Horas que foi a mimir",
				Value = sleepTime.ToString(@"hh\:mm tt")
			});
			
			await this._db.UpdateData(COLLECTION_AMIMIR, filter, Builders<BsonDocument>.Update.Set("isSleeping", false));

			// message on server
			try {
				var channel = user.Guild.GetTextChannel(Convert.ToUInt64(data["channelId"].AsString));
				if (channel != null) {
					await channel.SendMessageAsync(string.Empty, false, embed.Build());
				}
				return;
			} catch (Exception e) {
				await this._log.Error(e.ToString());
			}
			
			// DM
			try {
				var dm = await user.CreateDMChannelAsync();
				await dm.SendMessageAsync("Acordou!", false, embed.Build());
			} catch (Exception e) {
				await this._log.Error(e.ToString());
			}
		}
		
		private async Task PrivateMessageReceivedAsync(SocketUserMessage socketUserMessage, IDMChannel dmChannel) {
			
		}
		
	}
}
