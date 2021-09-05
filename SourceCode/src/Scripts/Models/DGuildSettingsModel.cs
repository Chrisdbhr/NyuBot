using System;
using Newtonsoft.Json;

namespace NyuBot.Models {
	
	[Serializable]
	public class DGuildSettingsModel {

		/// <summary>
		/// Text channel to notify when new user joins.
		/// </summary>
		[JsonProperty("joinChannelId")]
		public ulong? JoinChannelId;

		/// <summary>
		/// Text channel to notify when new user leaves (or get banned).
		/// </summary>
		[JsonProperty("leaveChannelId")]
		public ulong? LeaveChannelId;
		
		/// <summary>
		/// Text channelId id to backup messages.
		/// </summary>
		[JsonProperty("attachmentsBackupChannelId")]
		public ulong? AttachmentsBackupChannelId;

		/// <summary>
		/// Array of dynamic renamed voice channels.
		/// </summary>
		[JsonProperty("DynamicVoiceChannels")]
		public ulong?[] DynamicVoiceChannels;
		
		/// <summary>
		/// Enables new user anti spam in guild.
		/// </summary>
		[JsonProperty("enableNewUserAntiSpam")]
		public bool EnableNewUserAntiSpam = true;
		
		/// <summary>
		/// Channel to send hourly notification.
		/// </summary>
		[JsonProperty("channelHourlyMessage")]
		public ulong? HourlyMessageChannelId;
		
		/// <summary>
		/// Channel to send bot logs.
		/// </summary>
		[JsonProperty("channel-bot-logs-id")]
		public ulong? BotLogsTextChannelId;

	}
}
