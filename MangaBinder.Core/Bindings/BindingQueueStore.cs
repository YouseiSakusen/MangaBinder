using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 旧経路（BindingQueueDispatcher / BindingStoreRepository）および既存利用箇所のための互換窓口です。
/// 製本待ちの正本は <see cref="BindingStore.Queue"/> であり、このクラスは独自の状態を持たずすべて <see cref="BindingStore"/> へ委譲します。
/// 作品管理機能の Reactive 化完了後に削除する予定です。
/// </summary>
public sealed class BindingQueueStore
{
	private readonly BindingStore bindingStore;

	/// <summary>製本対象一覧（<see cref="BindingStore.Queue"/> と同一インスタンス）を取得します。</summary>
	public ObservableList<BindingSeries> Queue => this.bindingStore.Queue;

	/// <summary>現在の製本対象件数（<see cref="BindingStore.QueueCount"/> と同一インスタンス）を取得します。</summary>
	public BindableReactiveProperty<int> Count => this.bindingStore.QueueCount;

	/// <summary>現在の製本対象が空であるかどうか（<see cref="BindingStore.IsQueueEmpty"/> と同一インスタンス）を取得します。</summary>
	public BindableReactiveProperty<bool> IsEmpty => this.bindingStore.IsQueueEmpty;

	/// <summary>
	/// <see cref="BindingQueueStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本待ちの正本を保持する Store。</param>
	public BindingQueueStore(BindingStore bindingStore)
	{
		this.bindingStore = bindingStore;
	}

	/// <summary>
	/// 製本対象を追加します。
	/// 同一 SeriesId は重複登録しません。
	/// </summary>
	/// <param name="item">追加する製本対象。</param>
	public void Add(BindingSeries item)
		=> this.bindingStore.AddToQueue(item);

	/// <summary>
	/// 指定した SeriesId の製本対象を削除します。
	/// </summary>
	/// <param name="seriesId">削除対象の SeriesId。</param>
	public void Remove(long seriesId)
		=> this.bindingStore.RemoveFromQueue(seriesId);

	/// <summary>
	/// 指定した SeriesId が製本対象に含まれているか判定します。
	/// </summary>
	/// <param name="seriesId">判定する SeriesId。</param>
	/// <returns>含まれている場合 <see langword="true"/>。</returns>
	public bool Contains(long seriesId)
		=> this.bindingStore.ContainsInQueue(seriesId);

	/// <summary>
	/// 製本対象一覧を指定したリストで一括置換します。
	/// </summary>
	/// <param name="items">新しい製本対象一覧。</param>
	public void ReplaceAll(IEnumerable<BindingSeries> items)
		=> this.bindingStore.ReplaceQueue(items);

	/// <summary>製本対象一覧を全件クリアします。</summary>
	public void Clear()
		=> this.bindingStore.ClearQueue();
}
