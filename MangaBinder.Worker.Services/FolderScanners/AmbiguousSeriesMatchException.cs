namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// フォルダスキャン時に、同一タイトルの複数作品候補が存在するなど、
/// 自動判定が不可能な曖昧性が検出された場合に発生する例外です。
/// 候補情報を保持し、将来的な対話的解決や自動判定UI への接続を想定しています。
/// 素材（Material）と製本済み（Binding）の両フォルダスキャナで使用可能な汎用例外です。
/// </summary>
public class AmbiguousSeriesMatchException : Exception
{
	/// <summary>
	/// スキャン対象項目の物理パス（ファイルまたはディレクトリ）。
	/// </summary>
	public string Path { get; }

	/// <summary>
	/// スキャン処理により解析されたタイトル。
	/// </summary>
	public string Title { get; }

	/// <summary>
	/// 正規化後のタイトル内部表現。
	/// </summary>
	public string NormalizedTitleInternal { get; }

	/// <summary>
	/// スキャン処理により解析された作者情報。
	/// </summary>
	public string Author { get; }

	/// <summary>
	/// 候補として検出された MangaSeries の SeriesId 一覧。
	/// </summary>
	public IReadOnlyList<long> CandidateSeriesIds { get; }

	/// <summary>
	/// <see cref="AmbiguousSeriesMatchException"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="path">スキャン対象項目の物理パス。</param>
	/// <param name="title">解析されたタイトル。</param>
	/// <param name="normalizedTitleInternal">正規化後のタイトル内部表現。</param>
	/// <param name="author">解析された作者情報。</param>
	/// <param name="candidateSeriesIds">曖昧性が発生した候補 SeriesId 一覧。</param>
	public AmbiguousSeriesMatchException(
		string path,
		string title,
		string normalizedTitleInternal,
		string author,
		IReadOnlyList<long> candidateSeriesIds)
		: base($"自動判定不能な複数の同一タイトル作品が存在します。Path={path}, Title={title}, NormalizedTitleInternal={normalizedTitleInternal}, Author={author}, CandidateCount={candidateSeriesIds.Count}")
	{
		this.Path = path;
		this.Title = title;
		this.NormalizedTitleInternal = normalizedTitleInternal;
		this.Author = author;
		this.CandidateSeriesIds = candidateSeriesIds;
	}
}
