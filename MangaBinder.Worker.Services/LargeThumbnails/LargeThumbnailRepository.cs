using System.Data.SQLite;
using System.Text;
using Dapper;
using MangaBinder.Bindings;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Jobs.LargeThumbnails;

/// <summary>
/// 大容量サムネイル作成ジョブ用のリポジトリクラスです。
/// </summary>
public class LargeThumbnailRepository
{
	/// <summary>DB接続文字列。</summary>
	private readonly string connectionString;

	/// <summary>ロガー。</summary>
	private readonly ILogger<LargeThumbnailRepository> logger;

	/// <summary>
	/// <see cref="LargeThumbnailRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="workerContext">Worker 実行コンテキスト。</param>
	/// <param name="logger">ロガー。</param>
	public LargeThumbnailRepository(WorkerContext workerContext, ILogger<LargeThumbnailRepository> logger)
	{
		this.connectionString = workerContext.ConnectionString;
		this.logger = logger;
	}

	/// <summary>
	/// <see cref="ThumbnailStatus.LimitExceeded"/> または <see cref="ThumbnailStatus.Failed"/> の
	/// <see cref="MangaSeries"/> を Sources 付きで取得します。
	/// </summary>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>対象作品の一覧。</returns>
	public async ValueTask<IReadOnlyList<MangaSeries>> GetTargetSeriesAsync(CancellationToken ct)
	{
		var seriesSql = new StringBuilder();
		seriesSql.AppendLine(" SELECT ");
		seriesSql.AppendLine(" 	  SeriesId ");
		seriesSql.AppendLine(" 	, NormalizedTitleInternal ");
		seriesSql.AppendLine(" 	, Title ");
		seriesSql.AppendLine(" 	, Author ");
		seriesSql.AppendLine(" 	, Description ");
		seriesSql.AppendLine(" 	, SeriesCompleted ");
		seriesSql.AppendLine(" 	, IsOwnedCompleted ");
		seriesSql.AppendLine(" 	, StartVolume ");
		seriesSql.AppendLine(" 	, EndVolume ");
		seriesSql.AppendLine(" 	, BoundEndVolume ");
		seriesSql.AppendLine(" 	, OwnedMaxVolume ");
		seriesSql.AppendLine(" 	, NormalizedTitleExternal ");
		seriesSql.AppendLine(" 	, ShortTitle ");
		seriesSql.AppendLine(" 	, ThumbnailFileName ");
		seriesSql.AppendLine(" 	, ThumbnailStatus ");
		seriesSql.AppendLine(" 	, Publisher ");
		seriesSql.AppendLine(" 	, GoogleBooksImportStatus ");
		seriesSql.AppendLine(" 	, GoogleBooksImportedAt ");
		seriesSql.AppendLine(" 	, GoogleBooksImportMessage ");
		seriesSql.AppendLine(" FROM ");
		seriesSql.AppendLine(" 	MangaSeries ");
		seriesSql.AppendLine(" WHERE ");
		seriesSql.AppendLine(" 	ThumbnailStatus IN (:LimitExceeded, :Failed, :ArchiveInArchive); ");

		var sourcesSql = new StringBuilder();
		sourcesSql.AppendLine(" SELECT ");
		sourcesSql.AppendLine(" 	  Role ");
		sourcesSql.AppendLine(" 	, Path ");
		sourcesSql.AppendLine(" FROM ");
		sourcesSql.AppendLine(" 	MangaSources ");
		sourcesSql.AppendLine(" WHERE ");
		sourcesSql.AppendLine(" 	SeriesId = :SeriesId; ");

		using var conn = new SQLiteConnection(this.connectionString);
		await conn.OpenAsync(ct);

		var seriesList = (await conn.QueryAsync<MangaSeries>(seriesSql.ToString(), new
		{
			LimitExceeded = (int)ThumbnailStatus.LimitExceeded,
			Failed = (int)ThumbnailStatus.Failed,
			ArchiveInArchive = (int)ThumbnailStatus.ArchiveInArchive,
		})).ToList();

		foreach (var series in seriesList)
		{
			var sources = await conn.QueryAsync<MangaSource>(sourcesSql.ToString(), new { SeriesId = series.SeriesId });
			series.Sources.AddRange(sources);
		}

		return seriesList.AsReadOnly();
	}

	/// <summary>
	/// サムネイル作成結果を DB へ反映します。
	/// </summary>
	/// <param name="seriesId">更新対象の作品ID。</param>
	/// <param name="thumbnailFileName">サムネイルファイル名。</param>
	/// <param name="thumbnailStatus">更新後のサムネイルステータス。</param>
	/// <param name="ct">キャンセルトークン。</param>
	public async ValueTask UpdateThumbnailAsync(long seriesId, string thumbnailFileName, ThumbnailStatus thumbnailStatus, CancellationToken ct)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" UPDATE MangaSeries ");
		sql.AppendLine(" SET ");
		sql.AppendLine(" 	  ThumbnailFileName = :ThumbnailFileName ");
		sql.AppendLine(" 	, ThumbnailStatus = :ThumbnailStatus ");
		sql.AppendLine(" 	, UpdatedAt = DATETIME('now', 'localtime') ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	SeriesId = :SeriesId; ");

		using var conn = new SQLiteConnection(this.connectionString);
		await conn.OpenAsync(ct);
		await conn.ExecuteAsync(sql.ToString(), new
		{
			ThumbnailFileName = thumbnailFileName,
			ThumbnailStatus = (int)thumbnailStatus,
			SeriesId = seriesId,
		});
	}
}
