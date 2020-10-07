using System;
using System.Linq;

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
		
		public static string FirstCharToUpper(this string input) {
			switch (input)
			{
				case null: throw new ArgumentNullException(nameof(input));
				case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
				default: return input.First().ToString().ToUpper() + input.Substring(1);
			}
		}

		public static string CJoin(this string[] input, char separator = ' ') {
			return string.Join(separator, input);
		}
	}
}
