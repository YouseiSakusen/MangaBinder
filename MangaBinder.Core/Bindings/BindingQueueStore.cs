using ObservableCollections;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本開始状態のメモリ上の正となる Singleton ストアです。
/// </summary>
public sealed class BindingQueueStore
{
	private readonly ObservableList<BindingSeries> queue = new();

	/// <summary>製本対象一覧を取得します。</summary>
	public ObservableList<BindingSeries> Queue => this.queue;

	/// <summary>
	/// 製本対象を追加します。
	/// 同一 SeriesId は重複登録しません。
	/// </summary>
	/// <param name="item">追加する製本対象。</param>
	public void Add(BindingSeries item)
	{
		if (this.queue.Any(x => x.Series.SeriesId == item.Series.SeriesId))
			return;

		this.queue.Add(item);
	}

	/// <summary>
	/// 指定した SeriesId の製本対象を削除します。
	/// </summary>
	/// <param name="seriesId">削除対象の SeriesId。</param>
	public void Remove(long seriesId)
	{
		var target = this.queue.FirstOrDefault(x => x.Series.SeriesId == seriesId);
		if (target is not null)
			this.queue.Remove(target);
	}

	/// <summary>
	/// 指定した SeriesId が製本対象に含まれているか判定します。
	/// </summary>
	/// <param name="seriesId">判定する SeriesId。</param>
	/// <returns>含まれている場合 <see langword="true"/>。</returns>
	public bool Contains(long seriesId)
		=> this.queue.Any(x => x.Series.SeriesId == seriesId);

	/// <summary>
	/// 製本対象一覧を指定したリストで一括置換します。
	/// </summary>
	/// <param name="items">新しい製本対象一覧。</param>
	public void ReplaceAll(IEnumerable<BindingSeries> items)
	{
		this.queue.Clear();
		this.queue.AddRange(items);
	}

	/// <summary>製本対象一覧を全件クリアします。</summary>
	public void Clear()
		=> this.queue.Clear();
}
