namespace MangaBinder;

/// <summary>
/// MangaSeries の正本リストを管理する Singleton ストアです。
/// アプリケーション全体で同一の MangaSeries インスタンスを参照するための中央集中管理を提供します。
/// </summary>
public sealed class MangaSeriesStore
{
	private readonly List<MangaSeries> series = new();

	/// <summary>
	/// 全ての MangaSeries を取得します。
	/// </summary>
	/// <returns>MangaSeries の読み取り専用リスト。</returns>
	public IReadOnlyList<MangaSeries> GetAll()
		=> this.series.AsReadOnly();

	/// <summary>
	/// MangaSeries の一覧を指定したリストで一括置換します。
	/// </summary>
	/// <param name="newSeries">新しい MangaSeries 一覧。</param>
	public void ReplaceAll(IEnumerable<MangaSeries> newSeries)
	{
		this.series.Clear();
		this.series.AddRange(newSeries);
	}

	/// <summary>
	/// 指定した MangaSeries を追加します。
	/// 同一 SeriesId は重複登録しません。
	/// </summary>
	/// <param name="item">追加する MangaSeries。</param>
	public void Add(MangaSeries item)
	{
		if (this.series.Any(x => x.SeriesId == item.SeriesId))
			return;

		this.series.Add(item);
	}

	/// <summary>
	/// 指定した SeriesId で MangaSeries を検索します。
	/// </summary>
	/// <param name="seriesId">検索する SeriesId。</param>
	/// <returns>見つかった場合は該当の MangaSeries、見つからない場合は null。</returns>
	public MangaSeries? FindById(long seriesId)
		=> this.series.FirstOrDefault(x => x.SeriesId == seriesId);

	/// <summary>
	/// ストア内の全ての MangaSeries をクリアします。
	/// </summary>
	public void Clear()
		=> this.series.Clear();
}
