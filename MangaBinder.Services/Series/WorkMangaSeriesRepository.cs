using Dapper;
using MangaBinder.Settings;
using MangaBinder.Tags;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Series;

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
	/// WorkMangaSeriesTags も読み込み、series.Tags へ設定されます。
	/// 並び順は WorkId 昇順（古い登録順）とします。
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
		seriesSql.AppendLine(" 	WorkMangaSeries ");
		seriesSql.AppendLine(" ORDER BY ");
		seriesSql.AppendLine(" 	WorkId; ");

		var workTagsSql = new StringBuilder();
		workTagsSql.AppendLine(" SELECT ");
		workTagsSql.AppendLine(" 	  wt.WorkId ");
		workTagsSql.AppendLine(" 	, t.TagId ");
		workTagsSql.AppendLine(" 	, t.Name ");
		workTagsSql.AppendLine(" 	, t.DisplayOrder ");
		workTagsSql.AppendLine(" 	, t.ShowOnSeriesCard ");
		workTagsSql.AppendLine(" FROM ");
		workTagsSql.AppendLine(" 	WorkMangaSeriesTags wt ");
		workTagsSql.AppendLine(" INNER JOIN MangaTags t ON ");
		workTagsSql.AppendLine(" 	t.TagId = wt.TagId ");
		workTagsSql.AppendLine(" ORDER BY ");
		workTagsSql.AppendLine(" 	  wt.WorkId ");
		workTagsSql.AppendLine(" 	, t.DisplayOrder ");
		workTagsSql.AppendLine(" 	, t.TagId; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		var seriesList = (await connection.QueryAsync<MangaSeries>(seriesSql.ToString())).AsList();

		// WorkMangaSeriesTags + MangaTags を一括取得（N+1禁止）
		var workTags = await connection.QueryAsync<(int WorkId, long TagId, string Name, int DisplayOrder, bool ShowOnSeriesCard)>(
			workTagsSql.ToString());

		var seriesDict = seriesList.ToDictionary(s => s.WorkId);

		// メモリ上でタグを紐付け
		foreach (var row in workTags)
		{
			if (!seriesDict.TryGetValue(row.WorkId, out var series))
				continue;

			var tag = new MangaTag
			{
				TagId = row.TagId,
				Name = row.Name,
				DisplayOrder = row.DisplayOrder,
				ShowOnSeriesCard = row.ShowOnSeriesCard,
			};
			series.Tags.Add(tag);
		}

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
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		var workId = await this.InsertWorkSeriesInternalAsync(connection, null, series);

		// 採番された WorkId を series.WorkId に反映する
		series.WorkId = workId;

		return workId;
	}

	/// <summary>
	/// 作品本体を INSERT する共通 private メソッドです。
	/// SQLiteConnection と SQLiteTransaction（nullable）を受け取り、その上で INSERT を実行します。
	/// 採番された WorkId を返します。
	/// </summary>
	/// <param name="connection">DB接続。</param>
	/// <param name="transaction">トランザクション（null の場合は非トランザクション実行）。</param>
	/// <param name="series">保存対象の MangaSeries オブジェクト。</param>
	/// <returns>採番された WorkId。</returns>
	private async ValueTask<int> InsertWorkSeriesInternalAsync(
		SQLiteConnection connection,
		SQLiteTransaction? transaction,
		MangaSeries series)
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
		}, transaction);

		return workId;
	}

	/// <summary>
	/// WorkMangaSeries テーブルの作品情報を UPDATE します。
	/// WorkId をキーに更新対象を特定します。
	/// </summary>
	/// <param name="series">更新対象の MangaSeries オブジェクト。WorkId が設定されている必要があります。</param>
	public async ValueTask UpdateAsync(MangaSeries series)
	{
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		await this.UpdateWorkSeriesInternalAsync(connection, null, series);
	}

	/// <summary>
	/// 作品本体を UPDATE する共通 private メソッドです。
	/// SQLiteConnection と SQLiteTransaction（nullable）を受け取り、その上で UPDATE を実行します。
	/// </summary>
	/// <param name="connection">DB接続。</param>
	/// <param name="transaction">トランザクション（null の場合は非トランザクション実行）。</param>
	/// <param name="series">更新対象の MangaSeries オブジェクト。WorkId が設定されている必要があります。</param>
	/// <returns>完了時にコンプリートする ValueTask。</returns>
	private async ValueTask UpdateWorkSeriesInternalAsync(
		SQLiteConnection connection,
		SQLiteTransaction? transaction,
		MangaSeries series)
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
		}, transaction);
	}

	/// <summary>
	/// 作品本体を INSERT し、タグを保存します。
	/// この処理は 1 つの DB 接続と 1 つのトランザクション内で実行されます。
	/// 採番された WorkId は <paramref name="series"/> の WorkId プロパティに反映されます。
	/// 途中でエラーが発生した場合はロールバックされます。
	/// </summary>
	/// <param name="series">保存対象の MangaSeries オブジェクト。</param>
	/// <returns>完了時にコンプリートする ValueTask。</returns>
	public async ValueTask InsertWorkSeriesWithTagsInTransactionAsync(MangaSeries series)
	{
		var deleteSql = new StringBuilder();
		deleteSql.AppendLine(" DELETE FROM WorkMangaSeriesTags ");
		deleteSql.AppendLine(" WHERE ");
		deleteSql.AppendLine(" 	WorkId = :WorkId; ");

		var insertTagSql = new StringBuilder();
		insertTagSql.AppendLine(" INSERT INTO WorkMangaSeriesTags ( ");
		insertTagSql.AppendLine(" 	  WorkId ");
		insertTagSql.AppendLine(" 	, TagId ");
		insertTagSql.AppendLine(" ) VALUES ( ");
		insertTagSql.AppendLine(" 	  :WorkId ");
		insertTagSql.AppendLine(" 	, :TagId ");
		insertTagSql.AppendLine(" ); ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		using var transaction = connection.BeginTransaction();
		try
		{
			// 作品本体を INSERT（共通メソッド）
			var workId = await this.InsertWorkSeriesInternalAsync(connection, transaction, series);

			// 採番された WorkId を series に反映
			series.WorkId = workId;

			// 既存タグを削除（新規挿入時は存在しないが削除しても無害）
			await connection.ExecuteAsync(
				deleteSql.ToString(),
				new { WorkId = workId },
				transaction);

			// TagId > 0 のタグのみ保存（未保存タグ TagId=0 は除外）
			var validTags = series.Tags.Where(t => t.TagId > 0).ToList();
			foreach (var tag in validTags)
			{
				await connection.ExecuteAsync(
					insertTagSql.ToString(),
					new { WorkId = workId, TagId = tag.TagId },
					transaction);
			}

			transaction.Commit();
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	/// <summary>
	/// 作品本体を UPDATE し、タグを保存します。
	/// この処理は 1 つの DB 接続と 1 つのトランザクション内で実行されます。
	/// 途中でエラーが発生した場合はロールバックされます。
	/// </summary>
	/// <param name="series">更新対象の MangaSeries オブジェクト。WorkId が設定されている必要があります。</param>
	/// <returns>完了時にコンプリートする ValueTask。</returns>
	public async ValueTask UpdateWorkSeriesWithTagsInTransactionAsync(MangaSeries series)
	{
		var deleteSql = new StringBuilder();
		deleteSql.AppendLine(" DELETE FROM WorkMangaSeriesTags ");
		deleteSql.AppendLine(" WHERE ");
		deleteSql.AppendLine(" 	WorkId = :WorkId; ");

		var insertTagSql = new StringBuilder();
		insertTagSql.AppendLine(" INSERT INTO WorkMangaSeriesTags ( ");
		insertTagSql.AppendLine(" 	  WorkId ");
		insertTagSql.AppendLine(" 	, TagId ");
		insertTagSql.AppendLine(" ) VALUES ( ");
		insertTagSql.AppendLine(" 	  :WorkId ");
		insertTagSql.AppendLine(" 	, :TagId ");
		insertTagSql.AppendLine(" ); ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		using var transaction = connection.BeginTransaction();
		try
		{
			// 作品本体を UPDATE（共通メソッド）
			await this.UpdateWorkSeriesInternalAsync(connection, transaction, series);

			// 既存タグを削除
			await connection.ExecuteAsync(
				deleteSql.ToString(),
				new { WorkId = series.WorkId },
				transaction);

			// TagId > 0 のタグのみ保存（未保存タグ TagId=0 は除外）
			var validTags = series.Tags.Where(t => t.TagId > 0).ToList();
			foreach (var tag in validTags)
			{
				await connection.ExecuteAsync(
					insertTagSql.ToString(),
					new { WorkId = series.WorkId, TagId = tag.TagId },
					transaction);
			}

			transaction.Commit();
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	/// <summary>
	/// 指定した登録待ち作品一覧のタグを WorkMangaSeriesTags テーブルへ保存します。
	/// 各作品の既存レコードを DELETE してから series.Tags を INSERT します。
	/// TagId &lt;= 0 のタグは保存対象外となります（未保存タグの防御）。
	/// </summary>
	/// <param name="seriesList">保存対象の登録待ち作品一覧。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	public async ValueTask SaveWorkTagsAsync(
		IEnumerable<MangaSeries> seriesList,
		CancellationToken cancellationToken = default)
	{
		var deleteSql = new StringBuilder();
		deleteSql.AppendLine(" DELETE FROM WorkMangaSeriesTags ");
		deleteSql.AppendLine(" WHERE ");
		deleteSql.AppendLine(" 	WorkId = :WorkId; ");

		var insertSql = new StringBuilder();
		insertSql.AppendLine(" INSERT INTO WorkMangaSeriesTags ( ");
		insertSql.AppendLine(" 	  WorkId ");
		insertSql.AppendLine(" 	, TagId ");
		insertSql.AppendLine(" ) VALUES ( ");
		insertSql.AppendLine(" 	  :WorkId ");
		insertSql.AppendLine(" 	, :TagId ");
		insertSql.AppendLine(" ); ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync(cancellationToken);

		using var transaction = connection.BeginTransaction();
		try
		{
			foreach (var series in seriesList)
			{
				await connection.ExecuteAsync(
					deleteSql.ToString(),
					new { WorkId = series.WorkId },
					transaction);

				// TagId > 0 のタグのみ保存（未保存タグ TagId=0 は除外）
				var validTags = series.Tags.Where(t => t.TagId > 0).ToList();
				foreach (var tag in validTags)
				{
					await connection.ExecuteAsync(
						insertSql.ToString(),
						new { WorkId = series.WorkId, TagId = tag.TagId },
						transaction);
							}
						}

						transaction.Commit();
					}
					catch
					{
						transaction.Rollback();
						throw;
					}
				}

				/// <summary>
				/// 指定された WorkId のレコードを WorkMangaSeries テーブルから削除します。
				/// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
				/// </summary>
				/// <param name="connection">DB接続。</param>
				/// <param name="transaction">トランザクション。</param>
				/// <param name="workId">削除対象の WorkId。</param>
				/// <returns>完了時にコンプリートする ValueTask。</returns>
				public async ValueTask DeleteWorkSeriesByIdInTransactionAsync(
					SQLiteConnection connection,
					SQLiteTransaction transaction,
					int workId)
				{
					var deleteSql = new StringBuilder();
					deleteSql.AppendLine(" DELETE FROM WorkMangaSeries ");
					deleteSql.AppendLine(" WHERE ");
					deleteSql.AppendLine(" 	WorkId = :WorkId; ");

					await connection.ExecuteAsync(
						deleteSql.ToString(),
						new { WorkId = workId },
						transaction);
				}

				/// <summary>
				/// 指定された WorkId のレコードを WorkMangaSeriesTags テーブルから削除します。
				/// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
				/// </summary>
				/// <param name="connection">DB接続。</param>
				/// <param name="transaction">トランザクション。</param>
				/// <param name="workId">削除対象の WorkId。</param>
				/// <returns>完了時にコンプリートする ValueTask。</returns>
				public async ValueTask DeleteWorkSeriesTagsByIdInTransactionAsync(
					SQLiteConnection connection,
					SQLiteTransaction transaction,
					int workId)
				{
					var deleteSql = new StringBuilder();
					deleteSql.AppendLine(" DELETE FROM WorkMangaSeriesTags ");
					deleteSql.AppendLine(" WHERE ");
					deleteSql.AppendLine(" 	WorkId = :WorkId; ");

					await connection.ExecuteAsync(
						deleteSql.ToString(),
						new { WorkId = workId },
						transaction);
				}
				}
