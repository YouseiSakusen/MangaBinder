namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books 候補の選択結果を表すクラスです。
/// 複数ページにわたる候補全体を評価した結果を保持します。
/// </summary>
public sealed class GoogleBooksCandidateSelectionResult
{
	/// <summary>採用候補を取得します。採用候補がない場合は null。</summary>
	public NormalizedVolumeInfo? Candidate { get; init; }

	/// <summary>
	/// 代表 Reason を取得します。
	/// 採用候補がある場合は "Accepted"、ない場合は優先順位に従った代表理由。
	/// </summary>
	public string Reason { get; init; } = string.Empty;

	/// <summary>
	/// 全候補の Reason を集計した要約文字列を取得します。
	/// 例: "NoCategory:14; NotFirstVolume:5; ExcludedTitle:3"
	/// </summary>
	public string ReasonSummary { get; init; } = string.Empty;

	/// <summary>評価した候補の総数を取得します。</summary>
	public int CandidateCount { get; init; }

	/// <summary>採用条件を満たした候補の件数を取得します。</summary>
	public int AcceptedCount { get; init; }
}
