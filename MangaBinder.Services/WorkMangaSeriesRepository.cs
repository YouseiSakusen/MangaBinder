using Dapper;
using MangaBinder.Settings;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder;

/// <summary>
/// <see cref="MangaSeries"/> (WorkMangaSeries テーブル) の取得・保存を担う Repository クラスです。
/// WorkMangaSeries は MangaSeries の仮登録版として位置付けられています。
/// </summary>
public class WorkMangaSeriesRepository
{
	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="WorkMangaSeriesRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	public WorkMangaSeriesRepository(AppSettings appSettings)
	{
		this.appSettings = appSettings;
	}

	/// <summary>
	/// 全 WorkMangaSeries を取得します。
	/// WorkMangaSeries は MangaSeries へマッピングされ、WorkId のみ値が設定されます。
	/// SeriesId は 0 のままとなります。
	/// 並び順は MangaSeriesStore が管理するため、ORDER BY は使用しません。
	/// </summary>
	/// <returns><see cref="MangaSeries"/> の読み取り専用リスト。</returns>
	public async ValueTask<IReadOnlyList<MangaSeries>> GetAllAsync()
	{
		var seriesSql = new StringBuilder();
		seriesSql.AppendLine(" SELECT ");
		seriesSql.AppendLine(" 	  0 AS SeriesId ");
		seriesSql.AppendLine(" 	, NormalizedTitleInternal ");
		seriesSql.AppendLine(" 	, Title ");
		seriesSql.AppendLine(" 	, ShortTitle ");
		seriesSql.AppendLine(" 	, ThumbnailFileName ");
		seriesSql.AppendLine(" 	, Author ");
		seriesSql.AppendLine(" 	, Description ");
		seriesSql.AppendLine(" 	, SeriesCompleted ");
		seriesSql.AppendLine(" 	, IsOwnedCompleted ");
		seriesSql.AppendLine(" 	, IsSourceMissing ");
		seriesSql.AppendLine(" 	, StartVolume ");
		seriesSql.AppendLine(" 	, EndVolume ");
		seriesSql.AppendLine(" 	, BoundEndVolume ");
		seriesSql.AppendLine(" 	, OwnedMaxVolume ");
		seriesSql.AppendLine(" 	, NormalizedTitleExternal ");
		seriesSql.AppendLine(" 	, UpdatedAt ");
		seriesSql.AppendLine(" 	, ThumbnailStatus ");
		seriesSql.AppendLine(" 	, Publisher ");
		seriesSql.AppendLine(" 	, GoogleBooksImportStatus ");
		seriesSql.AppendLine(" 	, GoogleBooksImportedAt ");
		seriesSql.AppendLine(" 	, GoogleBooksImportMessage ");
		seriesSql.AppendLine(" 	, DescriptionSource ");
		seriesSql.AppendLine(" 	, DescriptionSourceTitle ");
		seriesSql.AppendLine(" 	, HasNestedArchive ");
		seriesSql.AppendLine(" 	, Memo ");
		seriesSql.AppendLine(" 	, WorkId ");
		seriesSql.AppendLine(" FROM ");
		seriesSql.AppendLine(" 	WorkMangaSeries; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		var seriesList = (await connection.QueryAsync<MangaSeries>(seriesSql.ToString())).AsList();

		return seriesList;
	}

	/// <summary>
	/// WorkMangaSeries テーブルへ新規作品情報を INSERT します。
	/// 採番された WorkId は <paramref name="series"/> の WorkId プロパティに反映されます。
	/// </summary>
	/// <param name="series">保存対象の MangaSeries オブジェクト。</param>
	/// <returns>採番された WorkId。</returns>
	public async ValueTask<int> InsertAsync(MangaSeries series)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" INSERT INTO WorkMangaSeries ( ");
		sql.AppendLine(" 	  NormalizedTitleInternal ");
		sql.AppendLine(" 	, Title ");
		sql.AppendLine(" 	, ShortTitle ");
		sql.AppendLine(" 	, ThumbnailFileName ");
		sql.AppendLine(" 	, Author ");
		sql.AppendLine(" 	, Description ");
		sql.AppendLine(" 	, SeriesCompleted ");
		sql.AppendLine(" 	, IsOwnedCompleted ");
		sql.AppendLine(" 	, IsSourceMissing ");
		sql.AppendLine(" 	, StartVolume ");
		sql.AppendLine(" 	, EndVolume ");
		sql.AppendLine(" 	, BoundEndVolume ");
		sql.AppendLine(" 	, OwnedMaxVolume ");
		sql.AppendLine(" 	, NormalizedTitleExternal ");
		sql.AppendLine(" 	, ThumbnailStatus ");
		sql.AppendLine(" 	, Publisher ");
		sql.AppendLine(" 	, GoogleBooksImportStatus ");
		sql.AppendLine(" 	, GoogleBooksImportedAt ");
		sql.AppendLine(" 	, GoogleBooksImportMessage ");
		sql.AppendLine(" 	, DescriptionSource ");
		sql.AppendLine(" 	, DescriptionSourceTitle ");
		sql.AppendLine(" 	, HasNestedArchive ");
		sql.AppendLine(" 	, Memo ");
		sql.AppendLine(" ) VALUES ( ");
		sql.AppendLine(" 	  :NormalizedTitleInternal ");
		sql.AppendLine(" 	, :Title ");
		sql.AppendLine(" 	, :ShortTitle ");
		sql.AppendLine(" 	, :ThumbnailFileName ");
		sql.AppendLine(" 	, :Author ");
		sql.AppendLine(" 	, :Description ");
		sql.AppendLine(" 	, :SeriesCompleted ");
		sql.AppendLine(" 	, :IsOwnedCompleted ");
		sql.AppendLine(" 	, :IsSourceMissing ");
		sql.AppendLine(" 	, :StartVolume ");
		sql.AppendLine(" 	, :EndVolume ");
		sql.AppendLine(" 	, :BoundEndVolume ");
		sql.AppendLine(" 	, :OwnedMaxVolume ");
		sql.AppendLine(" 	, :NormalizedTitleExternal ");
		sql.AppendLine(" 	, :ThumbnailStatus ");
		sql.AppendLine(" 	, :Publisher ");
		sql.AppendLine(" 	, :GoogleBooksImportStatus ");
		sql.AppendLine(" 	, :GoogleBooksImportedAt ");
		sql.AppendLine(" 	, :GoogleBooksImportMessage ");
		sql.AppendLine(" 	, :DescriptionSource ");
		sql.AppendLine(" 	, :DescriptionSourceTitle ");
		sql.AppendLine(" 	, :HasNestedArchive ");
		sql.AppendLine(" 	, :Memo ");
		sql.AppendLine(" ); ");
		sql.AppendLine(" SELECT CAST(last_insert_rowid() AS INT); ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		var workId = await connection.ExecuteScalarAsync<int>(sql.ToString(), new
		{
			NormalizedTitleInternal = series.NormalizedTitleInternal,
			Title = series.Title,
			ShortTitle = series.ShortTitle,
			ThumbnailFileName = series.ThumbnailFileName,
			Author = series.Author,
			Description = series.Description,
			SeriesCompleted = series.SeriesCompleted,
			IsOwnedCompleted = series.IsOwnedCompleted,
			IsSourceMissing = series.IsSourceMissing,
			StartVolume = series.StartVolume,
			EndVolume = series.EndVolume,
			BoundEndVolume = series.BoundEndVolume,
			OwnedMaxVolume = series.OwnedMaxVolume,
			NormalizedTitleExternal = series.NormalizedTitleExternal,
			ThumbnailStatus = (int)series.ThumbnailStatus,
			Publisher = series.Publisher,
			GoogleBooksImportStatus = (int)series.GoogleBooksImportStatus,
			GoogleBooksImportedAt = series.GoogleBooksImportedAt,
			GoogleBooksImportMessage = series.GoogleBooksImportMessage,
			DescriptionSource = (int)series.DescriptionSource,
			DescriptionSourceTitle = series.DescriptionSourceTitle,
			HasNestedArchive = series.HasNestedArchive,
			Memo = series.Memo,
		});

		// 採番された WorkId を series.WorkId に反映する
		series.WorkId = workId;

		return workId;
	}

	/// <summary>
	/// WorkMangaSeries テーブルの作品情報を UPDATE します。
	/// WorkId をキーに更新対象を特定します。
	/// </summary>
	/// <param name="series">更新対象の MangaSeries オブジェクト。WorkId が設定されている必要があります。</param>
	public async ValueTask UpdateAsync(MangaSeries series)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" UPDATE WorkMangaSeries ");
		sql.AppendLine(" SET ");
		sql.AppendLine(" 	  NormalizedTitleInternal = :NormalizedTitleInternal ");
		sql.AppendLine(" 	, Title = :Title ");
		sql.AppendLine(" 	, ShortTitle = :ShortTitle ");
		sql.AppendLine(" 	, ThumbnailFileName = :ThumbnailFileName ");
		sql.AppendLine(" 	, Author = :Author ");
		sql.AppendLine(" 	, Description = :Description ");
		sql.AppendLine(" 	, SeriesCompleted = :SeriesCompleted ");
		sql.AppendLine(" 	, IsOwnedCompleted = :IsOwnedCompleted ");
		sql.AppendLine(" 	, IsSourceMissing = :IsSourceMissing ");
		sql.AppendLine(" 	, StartVolume = :StartVolume ");
		sql.AppendLine(" 	, EndVolume = :EndVolume ");
		sql.AppendLine(" 	, BoundEndVolume = :BoundEndVolume ");
		sql.AppendLine(" 	, OwnedMaxVolume = :OwnedMaxVolume ");
		sql.AppendLine(" 	, NormalizedTitleExternal = :NormalizedTitleExternal ");
		sql.AppendLine(" 	, ThumbnailStatus = :ThumbnailStatus ");
		sql.AppendLine(" 	, Publisher = :Publisher ");
		sql.AppendLine(" 	, GoogleBooksImportStatus = :GoogleBooksImportStatus ");
		sql.AppendLine(" 	, GoogleBooksImportedAt = :GoogleBooksImportedAt ");
		sql.AppendLine(" 	, GoogleBooksImportMessage = :GoogleBooksImportMessage ");
		sql.AppendLine(" 	, DescriptionSource = :DescriptionSource ");
		sql.AppendLine(" 	, DescriptionSourceTitle = :DescriptionSourceTitle ");
		sql.AppendLine(" 	, HasNestedArchive = :HasNestedArchive ");
		sql.AppendLine(" 	, Memo = :Memo ");
		sql.AppendLine(" 	, UpdatedAt = DATETIME('now', 'localtime') ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	WorkId = :WorkId; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		await connection.ExecuteAsync(sql.ToString(), new
		{
			NormalizedTitleInternal = series.NormalizedTitleInternal,
			Title = series.Title,
			ShortTitle = series.ShortTitle,
			ThumbnailFileName = series.ThumbnailFileName,
			Author = series.Author,
			Description = series.Description,
			SeriesCompleted = series.SeriesCompleted,
			IsOwnedCompleted = series.IsOwnedCompleted,
			IsSourceMissing = series.IsSourceMissing,
			StartVolume = series.StartVolume,
			EndVolume = series.EndVolume,
			BoundEndVolume = series.BoundEndVolume,
			OwnedMaxVolume = series.OwnedMaxVolume,
			NormalizedTitleExternal = series.NormalizedTitleExternal,
			ThumbnailStatus = (int)series.ThumbnailStatus,
			Publisher = series.Publisher,
			GoogleBooksImportStatus = (int)series.GoogleBooksImportStatus,
			GoogleBooksImportedAt = series.GoogleBooksImportedAt,
			GoogleBooksImportMessage = series.GoogleBooksImportMessage,
			DescriptionSource = (int)series.DescriptionSource,
			DescriptionSourceTitle = series.DescriptionSourceTitle,
			HasNestedArchive = series.HasNestedArchive,
			Memo = series.Memo,
			WorkId = series.WorkId,
		});
	}
}

