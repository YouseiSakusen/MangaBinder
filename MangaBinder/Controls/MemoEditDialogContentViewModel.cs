using R3;

namespace MangaBinder.Controls;

/// <summary>
/// <see cref="MemoEditDialogContent"/> の ViewModel です。
/// 保存確定まで正本を変更しないための Dialog 専用の一時状態を保持します。
/// </summary>
public class MemoEditDialogContentViewModel : IDisposable
{
	/// <summary>リソース管理用の DisposableBag。</summary>
	private DisposableBag disposableBag;

	/// <summary>編集中のメモを取得します。</summary>
	public BindableReactiveProperty<string> Memo { get; }

	/// <summary>
	/// <see cref="MemoEditDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="initialMemo">編集開始時のメモ。</param>
	public MemoEditDialogContentViewModel(string initialMemo)
	{
		this.Memo = new BindableReactiveProperty<string>(initialMemo)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// リソースを破棄します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
