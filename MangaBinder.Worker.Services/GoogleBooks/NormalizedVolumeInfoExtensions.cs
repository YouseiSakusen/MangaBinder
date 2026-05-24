namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// <see cref="NormalizedVolumeInfo"/> の拡張メソッドを提供します。
/// </summary>
public static class NormalizedVolumeInfoExtensions
{
	/// <summary>Comics / Manga カテゴリを含むか判定します。</summary>
	private static readonly string[] ComicsKeywords = ["Comics", "Manga", "Comic", "\u30b3\u30df\u30c3\u30af", "\u6f2b\u753b"];

	/// <summary>
	/// Comics カテゴリを含むかどうかを返します。
	/// </summary>
	public static bool HasComicsCategory(this NormalizedVolumeInfo info)
		=> info.Categories.Any(c => ComicsKeywords.Any(k =>
			c.Contains(k, StringComparison.OrdinalIgnoreCase)));

	/// <summary>
	/// SeriesInfo が設定されているかどうかを返します。
	/// </summary>
	public static bool HasSeries(this NormalizedVolumeInfo info)
		=> info.SeriesInfo is not null;

	/// <summary>
	/// タイトルが指定クエリ文字列を含むかどうかを返します（部分一致）。
	/// </summary>
	public static bool MatchesQuery(this NormalizedVolumeInfo info, string query)
		=> !string.IsNullOrWhiteSpace(query)
		   && info.Title.Contains(query, StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// 指定フィルタを用いて候補を評価します。
	/// </summary>
	public static CandidateEvaluation Evaluate(
		this NormalizedVolumeInfo info,
		GoogleBooksVolumeFilter filter,
		string queryNorm)
		=> filter.Evaluate(info, queryNorm);

	/// <summary>
	/// デバッグ用文字列表現を返します。
	/// </summary>
	public static string ToDebugString(this NormalizedVolumeInfo info)
		=> $"[NVI] Title={info.Title} Publisher={info.Publisher} OrderNumber={info.OrderNumber?.ToString() ?? "-"} " +
		   $"HasSeries={info.HasSeries()} HasCategory={info.HasComicsCategory()} " +
		   $"DescLen={info.Description.Length} InfoLink={info.InfoLink}";
}
