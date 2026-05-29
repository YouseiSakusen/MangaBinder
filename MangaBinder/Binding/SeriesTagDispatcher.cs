using MangaBinder.Tags;

namespace MangaBinder.Binding;

/// <summary>
/// 作品へのタグ付け・タグ外し操作を担う Scoped サービスです。
/// </summary>
public sealed class SeriesTagDispatcher
{
	private readonly SeriesTagStore seriesTagStore;

	/// <summary>
	/// <see cref="SeriesTagDispatcher"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="seriesTagStore">タグ変更追跡ストア。</param>
	public SeriesTagDispatcher(SeriesTagStore seriesTagStore)
	{
		this.seriesTagStore = seriesTagStore;
	}

	/// <summary>
	/// 指定した作品にタグを付与します。
	/// 同じタグが既に付与されている場合は何もしません。
	/// </summary>
	/// <param name="series">対象作品。</param>
	/// <param name="tag">付与するタグ。</param>
	public void AddTag(MangaSeries series, MangaTag tag)
	{
		if (series.Tags.Any(t => t.TagId == tag.TagId))
			return;

		series.Tags.Add(tag);
		this.seriesTagStore.MarkDirty(series);
	}

	/// <summary>
	/// 指定した作品からタグを外します。
	/// 付与されていないタグの削除要求は無視します。
	/// </summary>
	/// <param name="series">対象作品。</param>
	/// <param name="tag">外すタグ。</param>
	public void RemoveTag(MangaSeries series, MangaTag tag)
	{
		var existing = series.Tags.FirstOrDefault(t => t.TagId == tag.TagId);
		if (existing is null)
			return;

		series.Tags.Remove(existing);
		this.seriesTagStore.MarkDirty(series);
	}

	/// <summary>
	/// チェック状態に基づいてタグを付与または削除します。
	/// </summary>
	/// <param name="series">対象作品。</param>
	/// <param name="tag">対象タグ。</param>
	/// <param name="isChecked">true: 付与 / false: 削除。</param>
	public void ApplyTag(MangaSeries series, MangaTag tag, bool isChecked)
	{
		if (isChecked)
			this.AddTag(series, tag);
		else
			this.RemoveTag(series, tag);
	}
}
