using Dapper;
using MangaBinder.Settings;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder;

/// <summary>
/// <see cref="MangaSeries"/> (WorkMangaSeries テーブル) の取得を担う Repository クラスです。
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
	/// </summary>
	/// <returns>タイトル昇順で並んだ <see cref="MangaSeries"/> の読み取り専用リスト。</returns>
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
		seriesSql.AppendLine(" 	WorkMangaSeries ");
		seriesSql.AppendLine(" ORDER BY ");
		seriesSql.AppendLine(" 	NormalizedTitleInternal; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		var seriesList = (await connection.QueryAsync<MangaSeries>(seriesSql.ToString())).AsList();

		return seriesList;
	}
}
