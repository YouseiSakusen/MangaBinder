namespace MangaBinder.Series;

/// <summary>
/// 作品メモの保存を担う Manager です。
/// DB 保存成功後に Store 正本の MangaSeries へ反映し、変更通知を流します。
/// </summary>
public class SeriesMemoManager
{
	/// <summary>MangaSeries データの取得・操作を担う Repository。</summary>
	private readonly MangaRepository mangaRepository;

	/// <summary>MangaSeries の正本リストを管理するストア。</summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>
	/// <see cref="SeriesMemoManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaRepository">MangaSeries データの取得・操作を担う Repository。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	public SeriesMemoManager(MangaRepository mangaRepository, MangaSeriesStore mangaSeriesStore)
	{
		this.mangaRepository = mangaRepository;
		this.mangaSeriesStore = mangaSeriesStore;
	}

	/// <summary>
	/// 指定作品のメモを保存します。
	/// DB 更新後に Store 正本の同一インスタンスへ反映し、変更通知を1回流します。
	/// </summary>
	/// <param name="seriesId">対象の SeriesId。</param>
	/// <param name="memo">新しいメモ。</param>
	/// <returns>完了時にコンプリートする ValueTask。</returns>
	/// <exception cref="InvalidOperationException">Store に対象作品が存在しない場合。</exception>
	public async ValueTask SaveMemoAsync(long seriesId, string memo)
	{
		var storeViewModel = this.mangaSeriesStore.FindViewModelById(seriesId)
			?? throw new InvalidOperationException($"Store から SeriesId {seriesId} の正式作品が見つかりません。");

		var manuallyEditedAt = DateTime.Now;

		await this.mangaRepository.UpdateSeriesMemoAsync(seriesId, memo, manuallyEditedAt, Constants.ViewNames.MemoEditDialog);

		// DB 保存成功後に Store 正本へ反映
		var storeInstance = storeViewModel.Series.Value;
		storeInstance.Memo = memo;
		storeInstance.ManuallyEditedAt = manuallyEditedAt;

		this.mangaSeriesStore.NotifySeriesChanged(seriesId);
	}
}
