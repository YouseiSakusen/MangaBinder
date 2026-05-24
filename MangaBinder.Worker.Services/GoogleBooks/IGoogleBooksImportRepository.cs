namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books インポートのデータアクセス境界インターフェースです。
/// </summary>
public interface IGoogleBooksImportRepository
{
	/// <summary>
	/// Google Books インポート対象の作品一覧を非同期で取得します。
	/// </summary>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>インポート対象の作品一覧。</returns>
	ValueTask<IReadOnlyList<MangaSeries>> GetImportTargetsAsync(CancellationToken ct);

	/// <summary>
	/// Google Books インポート成功時に MangaSeries を更新します。
	/// </summary>
	/// <param name="seriesId">更新対象の作品ID。</param>
	/// <param name="description">取得したあらすじ。</param>
	/// <param name="publisher">取得した出版社名。</param>
	/// <param name="author">取得した著者名（DB側が空の場合のみ反映）。</param>
	/// <param name="sourceTitle">採用した Google Books タイトル。</param>
	/// <param name="message">インポートメッセージ。</param>
	/// <param name="ct">キャンセルトークン。</param>
	ValueTask UpdateImportSuccessAsync(
		long seriesId,
		string description,
		string publisher,
		string author,
		string sourceTitle,
		string message,
		CancellationToken ct);

	/// <summary>
	/// Google Books 採用候補なし時に MangaSeries を更新します。
	/// </summary>
	/// <param name="seriesId">更新対象の作品ID。</param>
	/// <param name="message">インポートメッセージ（Reason / ReasonSummary）。</param>
	/// <param name="ct">キャンセルトークン。</param>
	ValueTask UpdateImportNotFoundAsync(
		long seriesId,
		string message,
		CancellationToken ct);

	/// <summary>
	/// Google Books インポート失敗時に MangaSeries を更新します。
	/// </summary>
	/// <param name="seriesId">更新対象の作品ID。</param>
	/// <param name="message">インポートメッセージ（例外メッセージ等）。</param>
	/// <param name="ct">キャンセルトークン。</param>
	ValueTask UpdateImportFailedAsync(
		long seriesId,
		string message,
		CancellationToken ct);
}
