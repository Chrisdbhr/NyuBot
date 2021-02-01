using Discord;

namespace NyuBot.Extensions {
	public static class IGuildUserExtensions {
		public static string GetNameSafe(this IGuildUser guildUser) {
			return guildUser == null ? null : guildUser.Nickname ?? guildUser.Username;
		}
		
		public static string GetNameBoldSafe(this IGuildUser guildUser) {
			return guildUser == null ? null : $"**{guildUser.Nickname ?? guildUser.Username}**";
		}
		
		public static string GetNameAndAliasSafe(this IGuildUser guildUser) {
			return guildUser == null ? null : guildUser.Username + (string.IsNullOrEmpty(guildUser.Nickname) ? string.Empty : $"({guildUser.Nickname})");
		}

		public static string GetAvatarUrlSafe(this IGuildUser guildUser) {
			return guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl();
		}

	}
}
