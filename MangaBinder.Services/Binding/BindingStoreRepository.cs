namespace MangaBinder.Binding;

/// <summary>
/// <see cref="BindingQueueStore"/> のメモリ操作を担うリポジトリクラスです。
/// DB アクセスは行いません。
/// </summary>
public sealed class BindingStoreRepository
{
	private readonly BindingQueueStore bindingQueueStore;

	/// <summary>
	/// <see cref="BindingStoreRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingQueueStore">製本開始状態ストア。</param>
	public BindingStoreRepository(BindingQueueStore bindingQueueStore)
	{
		this.bindingQueueStore = bindingQueueStore;
	}

	/// <summary>製本対象一覧を取得します。</summary>
	/// <returns>製本対象の読み取り専用リスト。</returns>
	public IReadOnlyList<BindingSeries> GetAll()
		=> this.bindingQueueStore.Queue;

	/// <summary>
	/// 製本対象を追加します。
	/// 同一 SeriesId は重複登録しません。
	/// </summary>
	/// <param name="item">追加する製本対象。</param>
	public void Add(BindingSeries item)
		=> this.bindingQueueStore.Add(item);

	/// <summary>
	/// 指定した SeriesId の製本対象を削除します。
	/// </summary>
	/// <param name="seriesId">削除対象の SeriesId。</param>
	public void Remove(long seriesId)
		=> this.bindingQueueStore.Remove(seriesId);

	/// <summary>
	/// 指定した SeriesId が製本対象に含まれているか判定します。
	/// </summary>
	/// <param name="seriesId">判定する SeriesId。</param>
	/// <returns>含まれている場合 <see langword="true"/>。</returns>
	public bool Contains(long seriesId)
		=> this.bindingQueueStore.Contains(seriesId);

	/// <summary>
	/// 製本対象一覧を指定したリストで一括置換します。
	/// </summary>
	/// <param name="items">新しい製本対象一覧。</param>
	public void ReplaceAll(IEnumerable<BindingSeries> items)
		=> this.bindingQueueStore.ReplaceAll(items);

	/// <summary>製本対象一覧を全件クリアします。</summary>
	public void Clear()
		=> this.bindingQueueStore.Clear();
}
