using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NyuBot.Extensions;
using SimpleJSON;

namespace NyuBot {
	public static class JsonCache {

		#region <<---------- Properties ---------->>
		
		public const string ROOT_FOLDER = "JsonData/";
		private static readonly StringBuilder sb = new StringBuilder();
		
		#endregion <<---------- Properties ---------->>


		

		#region <<---------- Public ---------->>

		public static async Task SaveJsonAsync(string path, JSONNode jsonNode) {
			if (string.IsNullOrEmpty(path) || jsonNode == null) return;
			
			var filePathWithExtension = $"{ROOT_FOLDER}{path}.json";

			// create folder
			Directory.CreateDirectory(Path.GetDirectoryName(filePathWithExtension));
			
			// write
			await using (StreamWriter writer = File.CreateText(filePathWithExtension)) {
				var jsonString = jsonNode.ToString();
				if (jsonString.Length <= 2) return;
				jsonString = jsonString.ReplaceAt(0,'{').ReplaceAt(jsonString.Length - 1, '}');
				await writer.WriteAsync(jsonString);
			}
		}

		public static async Task<List<JSONNode>> GetAllJsonInsideFolderAsync(string directoryPath, bool recursive = false, string fileName = "") {
			var jsonNodeList = new List<JSONNode>();
			try {
				directoryPath = Path.GetDirectoryName(directoryPath);
				if (!Directory.Exists(directoryPath)) return jsonNodeList;
				var filesPath = Directory.GetFiles(directoryPath, $"{fileName}*.json", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
				foreach (var filePath in filesPath) {
					if (string.IsNullOrEmpty(filePath)) continue;
					var json = await LoadJsonAsync(filePath);
					if(json != null) jsonNodeList.Add(json);
				}
				return jsonNodeList;
			} catch (Exception e) {
				Console.WriteLine($"Exception at {nameof(GetAllJsonInsideFolderAsync)}:\n{e}");
			}
			return jsonNodeList;
		}
		
		public static async Task<JSONNode> LoadJsonAsync(string filePath) {
			try {
				await LoadJsonToStringBuilderAsync(filePath);
				return JSON.Parse(sb.ToString());
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			return string.Empty;
		}
		
		public static async Task<JSONNode> LoadValueAsync(string filePath, string key) {
			try {
				await LoadJsonToStringBuilderAsync(filePath);
				var jsonNode = JSON.Parse(sb.ToString());
				return jsonNode[key];
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			return string.Empty;
		}

		public static async Task<bool> DeleteAsync(string path) {
			path = path.Replace('\\', '/').Trim();
			try {
				var filePath = $"{ROOT_FOLDER}{path}.json";
				if (File.Exists(filePath)) {
					File.Delete(filePath);
					return true;
				}
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			return false;
		}

		/// <summary>
		/// Returns TRUE if the folder is deleted.
		/// </summary>
		public static bool DeleteFolder(string folderPath) {
			try {
				folderPath = Path.GetDirectoryName( $"{ROOT_FOLDER}{folderPath}");
				bool isInvalid = Path.GetInvalidPathChars().Any(folderPath.Contains);

				if (isInvalid || string.IsNullOrEmpty(folderPath) || string.IsNullOrWhiteSpace(folderPath)) return false;

				if (Directory.Exists(folderPath)) {
					Directory.Delete(folderPath, true);
					return Directory.Exists(folderPath);
				}
			} catch (Exception e) {
				Console.WriteLine($"Exception at {nameof(DeleteFolder)}: \n{e}");
			}
			return false;
		}

		#endregion <<---------- Public ---------->>
		

		
		
		#region <<---------- Private ---------->>
		
		private static async Task LoadJsonToStringBuilderAsync(string filePath) {
			filePath = filePath.Replace('\\', '/').Trim();
			try {
				sb.Clear();
				
				var filePathWithExtension = $"{ROOT_FOLDER}{filePath}.json";
				if (!File.Exists(filePathWithExtension)) return;
				
				await using (var sourceStream = new FileStream(filePathWithExtension, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous)) {
  
					byte[] buffer = new byte[0x1000];
					int numRead = 0;
					while ((numRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) != 0) {
						sb.Append(Encoding.UTF8.GetString(buffer, 0, numRead));
					}
				}
			} catch (Exception e) {
				Console.WriteLine(e);
			}
		}

		#endregion <<---------- Private ---------->>

	}
}
