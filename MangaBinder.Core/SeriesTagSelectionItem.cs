using MangaBinder.Tags;
using R3;

namespace MangaBinder;

/// <summary>
/// タグ付けポップアップで使用するタグ選択項目です。
/// タグマスタは <see cref="TagStore"/> 所有の共有 <see cref="Tags.TagNotifier"/> を参照し、
/// チェック状態はポップアップ表示対象の作品から導出される表示専用の状態です。作品タグの正本ではありません。
/// </summary>
public sealed class SeriesTagSelectionItem : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 対象タグの共有 <see cref="Tags.TagNotifier"/> を取得します。
	/// 所有者は <see cref="TagStore"/> のため、Dispose してはいけません。
	/// </summary>
	public TagNotifier TagNotifier { get; }

	/// <summary>
	/// このタグがポップアップ表示対象の作品に付与されているかどうかを取得します。
	/// 表示専用であり、変更は <see cref="ToggleCommand"/> 経由で作品のタグへ反映します。
	/// </summary>
	public BindableReactiveProperty<bool> IsChecked { get; }

	/// <summary>
	/// チェック操作を通知するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> ToggleCommand { get; }

	/// <summary>
	/// <see cref="SeriesTagSelectionItem"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="tagNotifier">対象タグの共有 Notifier。</param>
	/// <param name="onToggle">チェック操作時に呼び出す処理。</param>
	public SeriesTagSelectionItem(TagNotifier tagNotifier, Action<TagNotifier> onToggle)
	{
		this.TagNotifier = tagNotifier;

		this.IsChecked = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.ToggleCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.ToggleCommand
			.Subscribe(_ => onToggle(this.TagNotifier))
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// このインスタンスが保持するリソースを解放します。共有 <see cref="Tags.TagNotifier"/> は破棄しません。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
