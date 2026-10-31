using R3;

namespace MangaBinder.Settings;

/// <summary>
/// アプリケーション全体の状態を保持するクラスです。
/// </summary>
public class ApplicationStatus : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>初回の必須フォルダ設定がDBへ保存済みかどうかを取得します。DB保存成功後にのみ true へ更新します。</summary>
	public BindableReactiveProperty<bool> IsInitialFolderSetupCompleted { get; }

	/// <summary>初回の必須フォルダ設定が未完了かどうかを取得します。</summary>
	public IReadOnlyBindableReactiveProperty<bool> IsInitialFolderSetupPending { get; }

	/// <summary>
	/// <see cref="ApplicationStatus"/> の新しいインスタンスを初期化します。
	/// </summary>
	public ApplicationStatus()
	{
		this.IsInitialFolderSetupCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.IsInitialFolderSetupPending = this.IsInitialFolderSetupCompleted
			.Select(completed => !completed)
			.ToReadOnlyBindableReactiveProperty(true)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose() => this.disposableBag.Dispose();
}
