namespace MangaBinder.Series;

/// <summary>
/// 作品メモの保存を担う Manager です。
/// DB 保存成功後に Store 正本の MangaSeries へ反映し、変更通知を流します。
/// </summary>
public class SeriesMemoManager
{
	/// <summary>MangaSeries データの取得・操作を担う Repository。</summary>
	private readonly MangaRepository mangaRepository;

	/// <summary>
	/// <see cref="SeriesMemoManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaRepository">MangaSeries データの取得・操作を担う Repository。</param>
	public SeriesMemoManager(MangaRepository mangaRepository)
	{
		this.mangaRepository = mangaRepository;
	}

	/// <summary>
	/// 指定作品のメモを保存します。
	/// DB 更新後に共有VMの MangaSeries へ反映し、変更通知を1回流します。
	/// </summary>
	/// <param name="seriesViewModel">対象の共有VM。</param>
	/// <param name="memo">新しいメモ。</param>
	/// <returns>完了時にコンプリートする ValueTask。</returns>
	public async ValueTask SaveMemoAsync(MangaSeriesViewModel seriesViewModel, string memo)
	{
		var series = seriesViewModel.Series.Value;

		var manuallyEditedAt = DateTime.Now;

		await this.mangaRepository.UpdateSeriesMemoAsync(series.SeriesId, memo, manuallyEditedAt, Constants.ViewNames.MemoEditDialog);

		series.Memo = memo;
		series.ManuallyEditedAt = manuallyEditedAt;

		seriesViewModel.Series.ForceNotify();
	}
}
