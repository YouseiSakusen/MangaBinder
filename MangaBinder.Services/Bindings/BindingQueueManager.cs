using MangaBinder.Series;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本待ちに関する操作を担当する Scoped の Manager です。
/// 製本待ちの正本は <see cref="BindingStore.Queue"/> であり、このクラスは状態を保持せず Queue を操作するだけです。
/// 共有 Notifier の <see cref="MangaSeriesNotifier.IsBindingQueued"/> は Queue の変更通知に追従するため、ここでは更新しません。
/// 通常の追加・削除では DB を更新しません。DB への保存はアプリ終了時に一括で行います。
/// </summary>
public sealed class BindingQueueManager
{
	private readonly BindingStore bindingStore;
	private readonly MangaSeriesStore mangaSeriesStore;
	private readonly BindingQueueRepository bindingQueueRepository;

	/// <summary>
	/// <see cref="BindingQueueManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本待ち Queue の正本を保持する Store。</param>
	/// <param name="mangaSeriesStore">共有 Notifier を所有する作品 Store。</param>
	/// <param name="bindingQueueRepository">BindingQueue テーブルの SeriesId を取得する Repository。</param>
	public BindingQueueManager(
		BindingStore bindingStore,
		MangaSeriesStore mangaSeriesStore,
		BindingQueueRepository bindingQueueRepository)
	{
		this.bindingStore = bindingStore;
		this.mangaSeriesStore = mangaSeriesStore;
		this.bindingQueueRepository = bindingQueueRepository;
	}

	/// <summary>
	/// 作品を製本待ちへ追加します。同一 SeriesId は重複登録しません。
	/// </summary>
	/// <param name="notifier">追加する作品の、<see cref="MangaSeriesStore"/> が所有する共有 Notifier。</param>
	public void Add(MangaSeriesNotifier notifier)
		=> this.bindingStore.AddToQueue(this.createBindingSeries(notifier));

	/// <summary>
	/// 指定した SeriesId の作品を製本待ちから削除します。
	/// </summary>
	/// <param name="seriesId">削除対象の SeriesId。</param>
	public void Remove(long seriesId)
		=> this.bindingStore.RemoveFromQueue(seriesId);

	/// <summary>製本待ちを全件クリアします。</summary>
	public void Clear()
		=> this.bindingStore.ClearQueue();

	/// <summary>
	/// 指定した SeriesId が製本待ちに登録されているか判定します。
	/// </summary>
	/// <param name="seriesId">判定する SeriesId。</param>
	/// <returns>登録されている場合 <see langword="true"/>。</returns>
	public bool Contains(long seriesId)
		=> this.bindingStore.ContainsInQueue(seriesId);

	/// <summary>
	/// DB の BindingQueue から製本待ちを復元し、<see cref="BindingStore.Queue"/> を置換します。
	/// <see cref="MangaSeriesStore"/> の構築が完了した後に呼び出してください。
	/// <see cref="MangaSeriesStore"/> に存在しない SeriesId は復元対象から除外します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	public async ValueTask RestoreAsync(CancellationToken cancellationToken = default)
	{
		var queuedSeriesIds = await this.bindingQueueRepository.GetQueuedSeriesIdsAsync(cancellationToken);

		// MangaSeriesStore の共有 Notifier から BindingSeries を構築する
		var bindingSeriesList = new List<BindingSeries>();
		foreach (var seriesId in queuedSeriesIds)
		{
			var notifier = this.mangaSeriesStore.FindNotifierById(seriesId);
			if (notifier != null)
			{
				bindingSeriesList.Add(this.createBindingSeries(notifier));
			}
		}

		this.bindingStore.ReplaceQueue(bindingSeriesList);
	}

	/// <summary>
	/// 現在の <see cref="BindingStore.Queue"/> を DB へ保存します。
	/// アプリ終了時にまとめて実行する想定で、追加・削除のたびには保存しません。
	/// </summary>
	public async ValueTask SaveAsync()
	{
		await this.bindingQueueRepository.SaveAsync(this.bindingStore.Queue);
	}

	/// <summary>
	/// 共有 Notifier を参照する製本待ち用の <see cref="BindingSeries"/> を生成します。
	/// </summary>
	/// <param name="notifier">共有 Notifier。</param>
	/// <returns>製本待ち用の <see cref="BindingSeries"/>。</returns>
	private BindingSeries createBindingSeries(MangaSeriesNotifier notifier)
		=> new()
		{
			SeriesNotifier = notifier,
			Status = BindingStartStatus.Configuring,
			CurrentStep = 0,
			AddedAt = DateTime.Now,
			UpdatedAt = DateTime.Now,
		};
}
