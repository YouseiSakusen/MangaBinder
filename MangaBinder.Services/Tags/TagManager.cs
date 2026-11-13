namespace MangaBinder.Tags;

/// <summary>
/// タグ機能で Repository と Store を跨ぐ処理を担当する Manager です。
/// 初期ロード時に、タグマスタを DB から取得して <see cref="TagStore"/> へ反映します。
/// 作品へのタグの付与・解除では、<see cref="TagStore"/> が所有する正本の <see cref="MangaTag"/> を作品へ反映します。
/// タグの追加・名前変更・削除といった CRUD は <see cref="TagEditor"/> が担当します。
/// DB への保存は行いません。
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

	/// <summary>
	/// 作品へタグを付与します。
	/// <see cref="TagStore"/> が所有する正本の <see cref="MangaTag"/> を <see cref="MangaSeries.Tags"/> へ追加します。
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <param name="tagId">付与するタグの ID。</param>
	/// <exception cref="InvalidOperationException">既に同じタグが付与されている場合。</exception>
	public void AddTagToSeries(MangaSeries series, long tagId)
	{
		if (series.Tags.Any(t => t.TagId == tagId))
		{
			throw new InvalidOperationException($"作品には既にタグが付与されています。TagId={tagId}");
		}

		var canonicalTag = this.tagStore.GetTagNotifier(tagId).Tag.Value;
		series.Tags.Add(canonicalTag);
	}

	/// <summary>
	/// 作品からタグを解除します。
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <param name="tagId">解除するタグの ID。</param>
	/// <exception cref="InvalidOperationException">作品にタグが付与されていない場合。</exception>
	public void RemoveTagFromSeries(MangaSeries series, long tagId)
	{
		var target = series.Tags.FirstOrDefault(t => t.TagId == tagId)
			?? throw new InvalidOperationException($"作品にタグが付与されていません。TagId={tagId}");

		series.Tags.Remove(target);
	}
}
