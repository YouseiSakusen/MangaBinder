namespace MangaBinder;

/// <summary>
/// アプリケーション全体のローディング表示を管理するサービスです。
/// 参照カウント管理とネスト対応により、複数の非同期処理が同時に進行している場合でも
/// 正確なローディング状態を保持します。
/// </summary>
public class LoadingService
{
	/// <summary>参照カウント。Begin で インクリメント、decrementReferenceCount で デクリメント。</summary>
	private int referenceCount = 0;

	/// <summary>現在のローディングメッセージ。</summary>
	private string currentMessage = string.Empty;

	/// <summary>
	/// ローディング状態が変更されたときに発火するイベントです。
	/// IsLoading と Message が通知されます。
	/// </summary>
	public event EventHandler<LoadingStateChangedEventArgs>? StateChanged;

	/// <summary>
	/// 現在のローディング状態（IsLoading）を取得します。
	/// </summary>
	public bool IsLoading => this.referenceCount > 0;

	/// <summary>
	/// 現在のローディングメッセージを取得します。
	/// </summary>
	public string CurrentMessage => this.currentMessage;

	/// <summary>
	/// <see cref="LoadingService"/> の新しいインスタンスを初期化します。
	/// </summary>
	public LoadingService()
	{
	}

	/// <summary>
	/// ローディングを開始し、IDisposable を返します。
	/// 返されたインスタンスを Dispose すると、参照カウントがデクリメントされます。
	/// </summary>
	/// <param name="message">表示するローディングメッセージ。</param>
	/// <returns>Dispose で参照カウントをデクリメントする IDisposable インスタンス。</returns>
	public IDisposable Begin(string message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// 参照カウントをインクリメント
		this.referenceCount++;
		this.currentMessage = message;

		// 状態変更イベントを発火
		this.onStateChanged();

		// Dispose 時にカウントをデクリメントするためのオブジェクトを返す
		return new disposableToken(this);
	}

	/// <summary>
	/// 参照カウントをデクリメントし、0 になったら状態を更新します。
	/// </summary>
	private void decrementReferenceCount()
	{
		// 参照カウントをデクリメント
		if (this.referenceCount > 0)
		{
			this.referenceCount--;
		}

		// カウントが 0 になったらメッセージをクリアして状態を通知
		if (this.referenceCount == 0)
		{
			this.currentMessage = string.Empty;
			this.onStateChanged();
		}
		else
		{
			// カウントが 0 でない場合も状態を通知
			this.onStateChanged();
		}
	}

	/// <summary>
	/// ローディング状態変更イベントを発火します。
	/// </summary>
	private void onStateChanged()
	{
		this.StateChanged?.Invoke(this, new LoadingStateChangedEventArgs(this.IsLoading, this.CurrentMessage));
	}

	/// <summary>
	/// Dispose トークン。Begin から返され、Dispose 時に親の参照カウントをデクリメントします。
	/// </summary>
	private sealed class disposableToken : IDisposable
	{
		/// <summary>親の LoadingService。</summary>
		private LoadingService? parent;

		/// <summary>既に Dispose されたかを示すフラグ。二重 Dispose 防止用。</summary>
		private bool isDisposed = false;

		/// <summary>
		/// <see cref="disposableToken"/> の新しいインスタンスを初期化します。
		/// </summary>
		/// <param name="parent">親の LoadingService。</param>
		public disposableToken(LoadingService parent)
		{
			this.parent = parent;
		}

		/// <summary>
		/// 親の LoadingService の参照カウントをデクリメントします。
		/// 二重 Dispose による複数回のデクリメント防止を行います。
		/// </summary>
		public void Dispose()
		{
			if (this.isDisposed)
			{
				return;
			}

			this.isDisposed = true;

			if (this.parent != null)
			{
				this.parent.decrementReferenceCount();
				this.parent = null;
			}
		}
	}
}

/// <summary>
/// ローディング状態変更イベントの引数です。
/// </summary>
public class LoadingStateChangedEventArgs : EventArgs
{
	/// <summary>
	/// ローディング状態を取得します。true でローディング中、false で完了。
	/// </summary>
	public bool IsLoading { get; }

	/// <summary>
	/// ローディングメッセージを取得します。
	/// </summary>
	public string Message { get; }

	/// <summary>
	/// <see cref="LoadingStateChangedEventArgs"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="isLoading">ローディング状態。</param>
	/// <param name="message">ローディングメッセージ。</param>
	public LoadingStateChangedEventArgs(bool isLoading, string message)
	{
		this.IsLoading = isLoading;
		this.Message = message;
	}
}
