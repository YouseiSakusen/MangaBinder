using MangaBinder.Bindings;

namespace MangaBinder;

/// <summary>
/// Home 画面の初期化と BindingQueue 復元を統一する Manager クラスです。
/// </summary>
public class MangaSeriesManager
{
	/// <summary>MangaSeries の取得を担う Repository。</summary>
	private readonly MangaRepository mangaRepository;

	/// <summary>BindingQueue の SeriesId 復元を担う Repository。</summary>
	private readonly BindingQueueRepository bindingQueueRepository;

	/// <summary>製本開始状態 Dispatcher。</summary>
	private readonly BindingQueueDispatcher bindingQueueDispatcher;

	/// <summary>
	/// <see cref="MangaSeriesManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaRepository">MangaSeries の取得を担う Repository。</param>
	/// <param name="bindingQueueRepository">BindingQueue の SeriesId 復元を担う Repository。</param>
	/// <param name="bindingQueueDispatcher">製本開始状態 Dispatcher。</param>
	public MangaSeriesManager(
		MangaRepository mangaRepository,
		BindingQueueRepository bindingQueueRepository,
		BindingQueueDispatcher bindingQueueDispatcher)
	{
		this.mangaRepository = mangaRepository;
		this.bindingQueueRepository = bindingQueueRepository;
		this.bindingQueueDispatcher = bindingQueueDispatcher;
	}

	/// <summary>
	/// Home 画面初期化時に全 MangaSeries を取得し、BindingQueue を復元します。
	/// MangaRepository で取得した MangaSeries インスタンスを正とし、
	/// BindingQueue に積む BindingSeries.Series も同一インスタンスを使用します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>タイトル昇順で並んだ MangaSeries のリスト。</returns>
	public async ValueTask<List<MangaSeries>> GetAllSeriesAsync(CancellationToken cancellationToken = default)
	{
		// 1. MangaRepository から MangaSeries 一覧を取得する
		var allSeriesReadOnly = await this.mangaRepository.GetAllSeriesAsync();
		var allSeries = allSeriesReadOnly.ToList();

		// 2. BindingQueue から SeriesId 一覧を取得する
		var queuedSeriesIds = await this.bindingQueueRepository.GetQueuedSeriesIdsAsync(cancellationToken);

		// 3. MangaSeries 一覧から SeriesId が一致するインスタンスを探し、BindingSeries を構築する
		var bindingSeriesList = new List<BindingSeries>();
		foreach (var seriesId in queuedSeriesIds)
		{
			var matchedSeries = allSeries.FirstOrDefault(s => s.SeriesId == seriesId);
			if (matchedSeries != null)
			{
				var bindingSeries = new BindingSeries
				{
					Series = matchedSeries,
					Status = BindingStartStatus.Configuring,
					CurrentStep = 0,
					AddedAt = DateTime.Now,
					UpdatedAt = DateTime.Now,
				};
				bindingSeriesList.Add(bindingSeries);
			}
		}

		// 4. BindingQueueDispatcher に復元したBindingQueue一覧を設定する
		this.bindingQueueDispatcher.ReplaceAll(bindingSeriesList);

		// 5. MangaSeries 一覧を返す
		return allSeries;
	}
}
