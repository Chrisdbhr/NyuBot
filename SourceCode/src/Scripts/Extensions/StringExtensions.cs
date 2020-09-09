using System;

namespace NyuBot.Extensions {
	public static class StringExtensions {
		
		public static string ReplaceAt(this string input, int index, char newChar)  {
			if (input == null) {
				throw new ArgumentNullException(nameof(input));
			}
			char[] chars = input.ToCharArray();
			chars[index] = newChar;
			return new string(chars);
		}

		public static string CSubstring(this string input, int startIndex, int length) {
			if (string.IsNullOrEmpty(input)) return null;
			if (length >= input.Length) {
				return input;
			}
			return input.Substring(startIndex, length);
		}
		
	}
}
