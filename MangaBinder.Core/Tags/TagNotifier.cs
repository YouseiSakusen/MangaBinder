using R3;

namespace MangaBinder.Tags;

/// <summary>
/// 1つの <see cref="MangaTag"/> を保持する Reactive な共有 Notifier です。
/// <see cref="TagStore"/> がタグごとに1つだけ生成・所有し、各作品の <c>MangaSeriesNotifier.Tags</c> や
/// タグ管理画面が同一インスタンスを共有して参照します。
/// タグ名などの更新時は同じ <see cref="MangaTag"/> インスタンスを更新し、<c>Tag.ForceNotify()</c> で通知します。
/// Dispose は所有者である <see cref="TagStore"/> が行い、利用側は Dispose してはいけません。
/// </summary>
public sealed class TagNotifier : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 保持している <see cref="MangaTag"/> インスタンスを取得します。
	/// 内容更新後は <c>ForceNotify()</c> で変更を通知します。
	/// </summary>
	public BindableReactiveProperty<MangaTag> Tag { get; }

	/// <summary>
	/// <see cref="TagNotifier"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="tag">保持する正本の <see cref="MangaTag"/> インスタンス。</param>
	public TagNotifier(MangaTag tag)
	{
		this.Tag = new BindableReactiveProperty<MangaTag>(tag)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// このインスタンスが保持するリソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
