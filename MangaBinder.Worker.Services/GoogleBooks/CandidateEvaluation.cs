namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books 候補の評価結果を表す record struct です。
/// </summary>
public readonly record struct CandidateEvaluation(
	bool HasCategory,
	bool HasSeries,
	bool IsExcluded,
	bool HasDescription,
	bool DescriptionContainsJapanese,
	bool StrictMatch,
	bool PartialMatch,
	bool DifferentSubtitle,
	double FuzzyScore,
	int? OrderNumber,
	string Reason)
{
	/// <summary>採用判定を取得します。</summary>
	public bool IsAccepted => this.Reason == "Accepted";
}
