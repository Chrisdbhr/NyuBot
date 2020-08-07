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
		
	}
}
