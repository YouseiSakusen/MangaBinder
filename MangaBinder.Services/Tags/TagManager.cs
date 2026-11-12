namespace MangaBinder.Tags;

/// <summary>
/// タグ機能で Repository と Store を跨ぐ処理を担当する Manager です。
/// 初期ロード時に、タグマスタを DB から取得して <see cref="TagStore"/> へ反映します。
/// タグの追加・名前変更・削除といった CRUD は <see cref="TagEditor"/> が担当します。
/// </summary>
public sealed class TagManager
{
	private readonly TagRepository repository;
	private readonly TagStore tagStore;

	/// <summary>
	/// <see cref="TagManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="repository">タグリポジトリ。</param>
	/// <param name="tagStore">タグマスタの正本ストア。</param>
	public TagManager(TagRepository repository, TagStore tagStore)
	{
		this.repository = repository;
		this.tagStore = tagStore;
	}

	/// <summary>
	/// タグマスタを DB から読み込み、<see cref="TagStore"/> へ反映します。
	/// 作品の <c>MangaSeriesNotifier</c> が共有 <see cref="TagNotifier"/> を参照できるよう、
	/// 作品を <see cref="MangaSeriesStore"/> へ反映する前に呼び出してください。
	/// </summary>
	public void LoadTags()
	{
		var allTags = this.repository.GetAll();
		this.tagStore.ReplaceTags(allTags);
	}
}
