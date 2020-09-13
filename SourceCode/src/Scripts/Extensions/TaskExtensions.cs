using System.Threading.Tasks;

namespace NyuBot.Extensions {
	public static class TaskExtensions {
		public static async void CAwait(this Task task) {
			await task;
		}
	}
}
