using Dapper;
using MangaBinder.Jobs.Contexts;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books インポート用リポジトリの実装クラスです。
/// </summary>
public class GoogleBooksImportRepository : IGoogleBooksImportRepository
{
	/// <summary>DB接続文字列。</summary>
	private readonly string connectionString;

	/// <summary>
	/// <see cref="GoogleBooksImportRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="workerContext">Worker 実行コンテキスト。</param>
	public GoogleBooksImportRepository(WorkerContext workerContext)
		=> this.connectionString = workerContext.ConnectionString;

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<MangaSeries>> GetImportTargetsAsync(CancellationToken ct)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" SELECT ");
		sql.AppendLine(" 	  SeriesId ");
		sql.AppendLine(" 	, NormalizedTitleInternal ");
		sql.AppendLine(" 	, Title ");
		sql.AppendLine(" 	, ShortTitle ");
		sql.AppendLine(" 	, Author ");
		sql.AppendLine(" 	, Description ");
		sql.AppendLine(" 	, SeriesCompleted ");
		sql.AppendLine(" 	, IsOwnedCompleted ");
		sql.AppendLine(" 	, StartVolume ");
		sql.AppendLine(" 	, EndVolume ");
		sql.AppendLine(" 	, BoundEndVolume ");
		sql.AppendLine(" 	, OwnedMaxVolume ");
		sql.AppendLine(" 	, NormalizedTitleExternal ");
		sql.AppendLine(" 	, ThumbnailFileName ");
		sql.AppendLine(" 	, ThumbnailStatus ");
		sql.AppendLine(" 	, Publisher ");
		sql.AppendLine(" 	, GoogleBooksImportStatus ");
		sql.AppendLine(" 	, GoogleBooksImportedAt ");
		sql.AppendLine(" 	, GoogleBooksImportMessage ");
		sql.AppendLine(" 	, DescriptionSource ");
		sql.AppendLine(" 	, DescriptionSourceTitle ");
		sql.AppendLine(" 	, HasNestedArchive ");
		sql.AppendLine(" FROM ");
		sql.AppendLine(" 	MangaSeries ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	GoogleBooksImportStatus = @None ");
		sql.AppendLine(" 	AND ( ");
		sql.AppendLine(" 		Author = '' ");
		sql.AppendLine(" 		OR Publisher = '' ");
		sql.AppendLine(" 		OR Description = '' ");
		sql.AppendLine(" 	) ");
		sql.AppendLine(" ORDER BY SeriesId; ");

		using var conn = new SQLiteConnection(this.connectionString);
		await conn.OpenAsync(ct);
		var result = await conn.QueryAsync<MangaSeries>(sql.ToString(), new
		{
			None = (int)GoogleBooksImportStatus.NotImported,
		});
		return result.AsList();
	}

	/// <inheritdoc/>
	public async ValueTask UpdateImportSuccessAsync(
		long seriesId,
		string description,
		string publisher,
		string author,
		string sourceTitle,
		string message,
		CancellationToken ct)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" UPDATE MangaSeries ");
		sql.AppendLine(" SET ");
		// Author は既存値が空の場合のみ更新
		sql.AppendLine(" 	  Author                    = CASE WHEN Author = '' THEN :Author ELSE Author END ");
		// Publisher は既存値が空の場合のみ更新
		sql.AppendLine(" 	, Publisher                = CASE WHEN Publisher = '' THEN :Publisher ELSE Publisher END ");
		// Description は既存値が空の場合のみ更新
		// Description を更新した場合のみ DescriptionSource / DescriptionSourceTitle も更新
		sql.AppendLine(" 	, Description              = CASE WHEN Description = '' THEN :Description ELSE Description END ");
		sql.AppendLine(" 	, DescriptionSource        = CASE WHEN Description = '' THEN :DescriptionSource ELSE DescriptionSource END ");
		sql.AppendLine(" 	, DescriptionSourceTitle   = CASE WHEN Description = '' THEN :DescriptionSourceTitle ELSE DescriptionSourceTitle END ");
		sql.AppendLine(" 	, GoogleBooksImportStatus  = :GoogleBooksImportStatus ");
		sql.AppendLine(" 	, GoogleBooksImportedAt    = DATETIME('now', 'localtime') ");
		sql.AppendLine(" 	, GoogleBooksImportMessage = :GoogleBooksImportMessage ");
		sql.AppendLine(" 	, UpdatedAt                = DATETIME('now', 'localtime') ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	SeriesId = :SeriesId; ");

		using var conn = new SQLiteConnection(this.connectionString);
		await conn.OpenAsync(ct);
		await conn.ExecuteAsync(sql.ToString(), new
		{
			SeriesId                 = seriesId,
			Description              = description,
			Publisher                = publisher,
			Author                   = author,
			DescriptionSource        = (int)DescriptionSource.GoogleBooks,
			DescriptionSourceTitle   = sourceTitle,
			GoogleBooksImportStatus  = (int)GoogleBooksImportStatus.Success,
			GoogleBooksImportMessage = message,
		});
	}

	/// <inheritdoc/>
	public async ValueTask UpdateImportNotFoundAsync(
		long seriesId,
		string message,
		CancellationToken ct)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" UPDATE MangaSeries ");
		sql.AppendLine(" SET ");
		sql.AppendLine(" 	  GoogleBooksImportStatus  = :GoogleBooksImportStatus ");
		sql.AppendLine(" 	, GoogleBooksImportedAt    = DATETIME('now', 'localtime') ");
		sql.AppendLine(" 	, GoogleBooksImportMessage = :GoogleBooksImportMessage ");
		sql.AppendLine(" 	, UpdatedAt                = DATETIME('now', 'localtime') ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	SeriesId = :SeriesId; ");

		using var conn = new SQLiteConnection(this.connectionString);
		await conn.OpenAsync(ct);
		await conn.ExecuteAsync(sql.ToString(), new
		{
			SeriesId                 = seriesId,
			GoogleBooksImportStatus  = (int)GoogleBooksImportStatus.NotFound,
			GoogleBooksImportMessage = message,
		});
	}

	/// <inheritdoc/>
	public async ValueTask UpdateImportFailedAsync(
		long seriesId,
		string message,
		CancellationToken ct)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" UPDATE MangaSeries ");
		sql.AppendLine(" SET ");
		sql.AppendLine(" 	  GoogleBooksImportStatus  = :GoogleBooksImportStatus ");
		sql.AppendLine(" 	, GoogleBooksImportedAt    = DATETIME('now', 'localtime') ");
		sql.AppendLine(" 	, GoogleBooksImportMessage = :GoogleBooksImportMessage ");
		sql.AppendLine(" 	, UpdatedAt                = DATETIME('now', 'localtime') ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	SeriesId = :SeriesId; ");

		using var conn = new SQLiteConnection(this.connectionString);
		await conn.OpenAsync(ct);
		await conn.ExecuteAsync(sql.ToString(), new
		{
			SeriesId                 = seriesId,
			GoogleBooksImportStatus  = (int)GoogleBooksImportStatus.Failed,
			GoogleBooksImportMessage = message,
		});
	}
}

