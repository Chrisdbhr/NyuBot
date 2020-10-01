using System;
using System.Reactive.Disposables;

namespace NyuBot.Extensions {
	public static class DisposableExtensions {
		public static void AddTo(this IDisposable disposable, CompositeDisposable compositeDisposable) {
			compositeDisposable.Add(disposable);
		}
	}
}
