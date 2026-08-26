namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 素材フォルダスキャン時に、複数の同一タイトル作品が存在する場合など、
/// 自動判定が不可能な状態を表す例外です。
/// スキャン対象素材を人間が選択する UI への接続を想定しています。
/// </summary>
public class AmbiguousSeriesMatchException : Exception
{
	/// <summary>
	/// 素材フォルダの物理パス。
	/// </summary>
	public string Path { get; }

	/// <summary>
	/// スキャン時に解析されたタイトル。
	/// </summary>
	public string Title { get; }

	/// <summary>
	/// 正規化されたタイトル。
	/// </summary>
	public string NormalizedTitleInternal { get; }

	/// <summary>
	/// スキャン時に解析された作者。
	/// </summary>
	public string Author { get; }

	/// <summary>
	/// マッチ候補となった MangaSeries の SeriesId 一覧。
	/// </summary>
	public IReadOnlyList<long> CandidateSeriesIds { get; }

	/// <summary>
	/// <see cref="AmbiguousSeriesMatchException"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="path">素材フォルダの物理パス。</param>
	/// <param name="title">スキャン時に解析されたタイトル。</param>
	/// <param name="normalizedTitleInternal">正規化されたタイトル。</param>
	/// <param name="author">スキャン時に解析された作者。</param>
	/// <param name="candidateSeriesIds">マッチ候補の SeriesId 一覧。</param>
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
