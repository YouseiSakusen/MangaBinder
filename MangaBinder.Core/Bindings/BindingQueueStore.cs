using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本開始状態のメモリ上の正となる Singleton ストアです。
/// </summary>
public sealed class BindingQueueStore : IDisposable
{
	private DisposableBag disposableBag = new();
	private readonly ObservableList<BindingSeries> queue = new();

	/// <summary>製本対象一覧を取得します。</summary>
	public ObservableList<BindingSeries> Queue => this.queue;

	/// <summary>現在の製本対象件数を取得します。</summary>
	public BindableReactiveProperty<int> Count { get; }

	/// <summary>現在の製本対象が空であるかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> IsEmpty { get; }

	/// <summary>
	/// <see cref="BindingQueueStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	public BindingQueueStore()
	{
		var initialCount = this.queue.Count;

		// Count と IsEmpty を初期化
		this.Count = new BindableReactiveProperty<int>(initialCount)
			.AddTo(ref this.disposableBag);

		this.IsEmpty = new BindableReactiveProperty<bool>(initialCount == 0)
			.AddTo(ref this.disposableBag);

		// Queue の件数変更を監視して Count / IsEmpty を更新
		this.queue.ObserveCountChanged()
			.Subscribe(count =>
			{
				this.Count.Value = count;
				this.IsEmpty.Value = count == 0;
			})
			.AddTo(ref this.disposableBag);
	}

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

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
