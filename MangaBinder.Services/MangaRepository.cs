using Dapper;
using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Jobs;
using MangaBinder.Settings;
using MangaBinder.Tags;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder;

/// <summary>
/// <see cref="MangaSeries"/> の取得を担う Repository クラスです。
/// </summary>
public class MangaRepository
{
    /// <summary>アプリケーション設定。</summary>
    private readonly AppSettings appSettings;

    /// <summary>
    /// <see cref="MangaRepository"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="appSettings">アプリケーション設定。</param>
    public MangaRepository(AppSettings appSettings)
    {
        this.appSettings = appSettings;
    }

	/// <summary>
	/// 全 <see cref="MangaSeries"/> を <see cref="MangaSource"/>・<see cref="MangaTag"/>・<see cref="BindingHistory"/> 込みで取得します。
	/// 並び順は MangaSeriesStore が管理するため、作品単位の ORDER BY は使用しません。
	/// </summary>
	/// <returns><see cref="MangaSeries"/> の読み取り専用リスト。</returns>
	public async ValueTask<IReadOnlyList<MangaSeries>> GetAllSeriesAsync()
	{
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		return await queryJoinedSeriesAsync(connection, string.Empty, null);
	}

    /// <summary>
    /// Home 画面の表示状態を取得します。
    /// </summary>
    /// <returns>Home 画面の状態情報。</returns>
    public async ValueTask<HomeStateInformation> GetHomeStateInformationAsync()
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	  (SELECT COUNT(*) FROM MangaSeries) AS SeriesCount ");
        sql.AppendLine(" 	, (SELECT COUNT(*) > 0 FROM SourceFolders WHERE Role = 0) AS HasMaterialSourceFolder ");
        sql.AppendLine(" 	, (SELECT COUNT(*) > 0 FROM JobQueue WHERE Type = :MaterialScan AND Status = :Success) AS HasCompletedMaterialFolderScanJob; ");

        using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
        await connection.OpenAsync();

        var row = await connection.QuerySingleAsync(
            sql.ToString(),
            new
            {
                MaterialScan = (int)JobType.MaterialScan,
                Success      = (int)JobStatus.Success,
            });

        var seriesCount                    = (int)(long)row.SeriesCount;
        var hasMaterialSourceFolder        = (long)row.HasMaterialSourceFolder != 0;
        var hasCompletedMaterialFolderScan = (long)row.HasCompletedMaterialFolderScanJob != 0;

        var kind = HomeEmptyStateKind.None;
        if (seriesCount == 0)
        {
            if (!hasMaterialSourceFolder)
                kind = HomeEmptyStateKind.MaterialFolderNotRegistered;
            else if (hasCompletedMaterialFolderScan)
                kind = HomeEmptyStateKind.MaterialFolderScanCompletedButNoSeries;
        }

        var info = new HomeStateInformation();
        info.SeriesCount.Value                    = seriesCount;
        info.HasMaterialSourceFolder.Value        = hasMaterialSourceFolder;
        info.HasCompletedMaterialFolderScanJob.Value = hasCompletedMaterialFolderScan;
        info.EmptyStateKind.Value                 = kind;

        return info;
    }

	/// <summary>
	/// 指定した SeriesId の <see cref="MangaSeries"/> を <see cref="MangaSource"/>・<see cref="MangaTag"/>・<see cref="BindingHistory"/> 込みで取得します。
	/// </summary>
	/// <param name="seriesId">取得対象の SeriesId。</param>
	/// <returns>取得した <see cref="MangaSeries"/>。該当する作品が見つからない場合は null を返します。</returns>
	public async ValueTask<MangaSeries?> GetSeriesAsync(long seriesId)
	{
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		var seriesList = await queryJoinedSeriesAsync(
			connection,
			" WHERE s.SeriesId = :SeriesId ",
			new { SeriesId = seriesId });

		return seriesList.Count == 0 ? null : seriesList[0];
	}

	/// <summary>
	/// 作品・素材所在・タグ・製本履歴を1回の JOIN クエリで取得し、同一 SeriesId を同一インスタンスへ集約します。
	/// 製本履歴は作品ごとに HistoryId 降順で最新3件までです。
	/// </summary>
	/// <param name="connection">オープン済みの接続。</param>
	/// <param name="whereClause">WHERE 句（不要な場合は空文字列）。</param>
	/// <param name="parameters">SQL パラメーター。</param>
	private static async ValueTask<List<MangaSeries>> queryJoinedSeriesAsync(
		SQLiteConnection connection,
		string whereClause,
		object? parameters)
	{
		var selectSql = new StringBuilder();
		selectSql.AppendLine(" SELECT ");
		selectSql.AppendLine(" 	  s.SeriesId ");
		selectSql.AppendLine(" 	, s.NormalizedTitleInternal ");
		selectSql.AppendLine(" 	, s.Title ");
		selectSql.AppendLine(" 	, s.ShortTitle ");
		selectSql.AppendLine(" 	, s.ThumbnailFileName ");
		selectSql.AppendLine(" 	, s.Author ");
		selectSql.AppendLine(" 	, s.Description ");
		selectSql.AppendLine(" 	, s.SeriesCompleted ");
		selectSql.AppendLine(" 	, s.IsOwnedCompleted ");
		selectSql.AppendLine(" 	, s.IsSourceMissing ");
		selectSql.AppendLine(" 	, s.StartVolume ");
		selectSql.AppendLine(" 	, s.EndVolume ");
		selectSql.AppendLine(" 	, s.BoundEndVolume ");
		selectSql.AppendLine(" 	, s.OwnedMaxVolume ");
		selectSql.AppendLine(" 	, s.NormalizedTitleExternal ");
		selectSql.AppendLine(" 	, s.UpdatedAt ");
		selectSql.AppendLine(" 	, s.ThumbnailStatus ");
		selectSql.AppendLine(" 	, s.Publisher ");
		selectSql.AppendLine(" 	, s.GoogleBooksImportStatus ");
		selectSql.AppendLine(" 	, s.GoogleBooksImportedAt ");
		selectSql.AppendLine(" 	, s.GoogleBooksImportMessage ");
		selectSql.AppendLine(" 	, s.DescriptionSource ");
		selectSql.AppendLine(" 	, s.DescriptionSourceTitle ");
		selectSql.AppendLine(" 	, s.HasNestedArchive ");
		selectSql.AppendLine(" 	, s.Memo ");
		selectSql.AppendLine(" 	, s.ManuallyEditedAt ");
		selectSql.AppendLine(" 	, s.IsOwnedMaxVolumeManuallyEdited ");
		selectSql.AppendLine(" 	, s.IsIncomplete ");
		selectSql.AppendLine(" 	, s.MaterialFolderCreatedAt ");
		selectSql.AppendLine(" 	, s.UpdateSource ");
		selectSql.AppendLine(" 	, src.SourceId ");
		selectSql.AppendLine(" 	, src.SeriesId ");
		selectSql.AppendLine(" 	, src.Path ");
		selectSql.AppendLine(" 	, src.Role ");
		selectSql.AppendLine(" 	, t.TagId ");
		selectSql.AppendLine(" 	, t.Name ");
		selectSql.AppendLine(" 	, t.DisplayOrder ");
		selectSql.AppendLine(" 	, t.ShowOnSeriesCard ");
		selectSql.AppendLine(" 	, h.HistoryId ");
		selectSql.AppendLine(" 	, h.SeriesId ");
		selectSql.AppendLine(" 	, h.BoundAt ");
		selectSql.AppendLine(" 	, h.VolumeRange ");
		selectSql.AppendLine(" 	, h.OutputFileName ");
		selectSql.AppendLine(" 	, h.OutputFolderPath ");
		selectSql.AppendLine(" FROM ");
		selectSql.AppendLine(" 	MangaSeries s ");
		selectSql.AppendLine(" LEFT JOIN MangaSources src ON ");
		selectSql.AppendLine(" 	src.SeriesId = s.SeriesId ");
		selectSql.AppendLine(" LEFT JOIN MangaSeriesTags st ON ");
		selectSql.AppendLine(" 	st.SeriesId = s.SeriesId ");
		selectSql.AppendLine(" LEFT JOIN MangaTags t ON ");
		selectSql.AppendLine(" 	t.TagId = st.TagId ");
		selectSql.AppendLine(" LEFT JOIN ( ");
		selectSql.AppendLine(" 	SELECT ");
		selectSql.AppendLine(" 		  bh.HistoryId ");
		selectSql.AppendLine(" 		, bh.SeriesId ");
		selectSql.AppendLine(" 		, bh.BoundAt ");
		selectSql.AppendLine(" 		, bh.VolumeRange ");
		selectSql.AppendLine(" 		, bh.OutputFileName ");
		selectSql.AppendLine(" 		, bh.OutputFolderPath ");
		selectSql.AppendLine(" 	FROM ");
		selectSql.AppendLine(" 		BindingHistory bh ");
		selectSql.AppendLine(" 	WHERE ");
		selectSql.AppendLine(" 		bh.HistoryId IN ( ");
		selectSql.AppendLine(" 			SELECT x.HistoryId FROM BindingHistory x ");
		selectSql.AppendLine(" 			WHERE x.SeriesId = bh.SeriesId ");
		selectSql.AppendLine(" 			ORDER BY x.HistoryId DESC ");
		selectSql.AppendLine(" 			LIMIT 3 ");
		selectSql.AppendLine(" 		) ");
		selectSql.AppendLine(" ) h ON ");
		selectSql.AppendLine(" 	h.SeriesId = s.SeriesId ");

		var orderBySql = new StringBuilder();
		orderBySql.AppendLine(" ORDER BY ");
		orderBySql.AppendLine(" 	  s.SeriesId ");
		orderBySql.AppendLine(" 	, src.Role ");
		orderBySql.AppendLine(" 	, src.Path ");
		orderBySql.AppendLine(" 	, t.DisplayOrder ");
		orderBySql.AppendLine(" 	, t.TagId ");
		orderBySql.AppendLine(" 	, h.HistoryId DESC; ");

		var seriesList = new List<MangaSeries>();
		var seriesDict = new Dictionary<long, MangaSeries>();
		var sourceIds = new HashSet<long>();
		var tagKeys = new HashSet<(long SeriesId, long TagId)>();
		var historyIds = new HashSet<long>();

		await connection.QueryAsync<MangaSeries, MangaSource, MangaTag, BindingHistory, MangaSeries>(
			selectSql.ToString() + whereClause + orderBySql.ToString(),
			(row, source, tag, history) =>
			{
				if (!seriesDict.TryGetValue(row.SeriesId, out var series))
				{
					series = row;
					seriesDict.Add(series.SeriesId, series);
					seriesList.Add(series);
				}

				if (source is not null && sourceIds.Add(source.SourceId))
					series.Sources.Add(source);

				if (tag is not null && tagKeys.Add((series.SeriesId, tag.TagId)))
					series.Tags.Add(tag);

				if (history is not null && historyIds.Add(history.HistoryId))
					series.BindingHistories.Add(history);

				return series;
			},
			parameters,
			splitOn: "SourceId,TagId,HistoryId");

		return seriesList;
	}

    /// <summary>
    /// 指定した作品一覧のタグを MangaSeriesTags テーブルへ保存します。
    /// 各作品の既存レコードを DELETE してから series.Tags を INSERT します。
    /// TagId &lt;= 0 のタグは保存対象外となります（未保存タグの防御）。
    /// </summary>
    /// <param name="seriesList">保存対象の作品一覧。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    public async ValueTask SaveSeriesTagsAsync(
        IEnumerable<MangaSeries> seriesList,
        CancellationToken cancellationToken = default)
    {
        var deleteSql = new StringBuilder();
        deleteSql.AppendLine(" DELETE FROM MangaSeriesTags ");
        deleteSql.AppendLine(" WHERE ");
        deleteSql.AppendLine(" 	SeriesId = :SeriesId; ");

        var insertSql = new StringBuilder();
        insertSql.AppendLine(" INSERT INTO MangaSeriesTags ( ");
        insertSql.AppendLine(" 	  SeriesId ");
        insertSql.AppendLine(" 	, TagId ");
        insertSql.AppendLine(" ) VALUES ( ");
        insertSql.AppendLine(" 	  :SeriesId ");
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
                    new { SeriesId = series.SeriesId },
                    transaction);

                // TagId > 0 のタグのみ保存（未保存タグ TagId=0 は除外）
                var validTags = series.Tags.Where(t => t.TagId > 0).ToList();
                foreach (var tag in validTags)
                {
                    await connection.ExecuteAsync(
                        insertSql.ToString(),
                        new { SeriesId = series.SeriesId, TagId = tag.TagId },
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
    /// 既存の正式作品（MangaSeries）を UPDATE します。
    /// 画面で編集可能な項目のみが更新対象です。
    /// GoogleBooks 関連、ThumbnailStatus、BoundEndVolume、HasNestedArchive、IsSourceMissing など
    /// システム管理項目は更新されません。
    /// </summary>
    /// <param name="series">更新対象の MangaSeries。SeriesId != 0 である必要があります。</param>
    /// <summary>
    /// MangaSeries テーブルのレコードを更新します。
    /// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
    /// </summary>
    /// <param name="connection">DB接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="series">更新対象の MangaSeries。</param>
    /// <param name="updateSource">更新元を表す文字列（例：EditorPage など）。</param>
    /// <returns>完了時にコンプリートする ValueTask。</returns>
    public async ValueTask UpdateSeriesAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        MangaSeries series,
        string updateSource)
    {
        if (series.SeriesId == 0)
            throw new InvalidOperationException("UpdateSeriesAsync は新規作品（SeriesId=0）では実行できません。");

        var sql = new StringBuilder();
        sql.AppendLine(" UPDATE MangaSeries ");
        sql.AppendLine(" SET ");
        sql.AppendLine(" 	  Title = :Title ");
        sql.AppendLine(" 	, Author = :Author ");
        sql.AppendLine(" 	, Publisher = :Publisher ");
        sql.AppendLine(" 	, Description = :Description ");
        sql.AppendLine(" 	, DescriptionSource = :DescriptionSource ");
        sql.AppendLine(" 	, DescriptionSourceTitle = :DescriptionSourceTitle ");
        sql.AppendLine(" 	, Memo = :Memo ");
        sql.AppendLine(" 	, StartVolume = :StartVolume ");
        sql.AppendLine(" 	, EndVolume = :EndVolume ");
        sql.AppendLine(" 	, SeriesCompleted = :SeriesCompleted ");
        sql.AppendLine(" 	, IsOwnedCompleted = :IsOwnedCompleted ");
        sql.AppendLine(" 	, OwnedMaxVolume = :OwnedMaxVolume ");
        sql.AppendLine(" 	, IsOwnedMaxVolumeManuallyEdited = :IsOwnedMaxVolumeManuallyEdited ");
        sql.AppendLine(" 	, IsIncomplete = :IsIncomplete ");
        sql.AppendLine(" 	, MaterialFolderCreatedAt = :MaterialFolderCreatedAt ");
        sql.AppendLine(" 	, ManuallyEditedAt = :ManuallyEditedAt ");
        sql.AppendLine(" 	, UpdatedAt = DATETIME('now', 'localtime') ");
        sql.AppendLine(" 	, UpdateSource = :UpdateSource ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 	SeriesId = :SeriesId; ");

        await connection.ExecuteAsync(
            sql.ToString(),
            new
            {
                SeriesId = series.SeriesId,
                Title = series.Title,
                Author = series.Author,
                Publisher = series.Publisher,
                Description = series.Description,
                DescriptionSource = series.DescriptionSource,
                DescriptionSourceTitle = series.DescriptionSourceTitle,
                Memo = series.Memo,
                StartVolume = series.StartVolume,
                EndVolume = series.EndVolume,
                SeriesCompleted = series.SeriesCompleted,
                IsOwnedCompleted = series.IsOwnedCompleted,
                OwnedMaxVolume = series.OwnedMaxVolume,
                IsOwnedMaxVolumeManuallyEdited = series.IsOwnedMaxVolumeManuallyEdited,
                IsIncomplete = series.IsIncomplete,
                MaterialFolderCreatedAt = series.MaterialFolderCreatedAt,
                ManuallyEditedAt = series.ManuallyEditedAt,
                UpdateSource = updateSource,
            },
            transaction: transaction);
    }

    /// <summary>
    /// MangaSources テーブルへ新規レコードを挿入します。
    /// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
    /// </summary>
    /// <param name="connection">DB接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="seriesId">親の MangaSeries の SeriesId。</param>
    /// <param name="path">素材フォルダまたはファイルの物理フルパス。</param>
    /// <param name="role">フォルダの役割。</param>
    /// <returns>挿入された MangaSource の SourceId。</returns>
    public async ValueTask<long> InsertMangaSourceAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        long seriesId,
        string path,
        FolderRole role)
    {
        var insertSql = new StringBuilder();
        insertSql.AppendLine(" INSERT INTO MangaSources ( ");
        insertSql.AppendLine(" 	  SeriesId ");
        insertSql.AppendLine(" 	, Path ");
        insertSql.AppendLine(" 	, Role ");
        insertSql.AppendLine(" ) VALUES ( ");
        insertSql.AppendLine(" 	  :SeriesId ");
        insertSql.AppendLine(" 	, :Path ");
        insertSql.AppendLine(" 	, :Role ");
        insertSql.AppendLine(" ) ");
        insertSql.AppendLine(" RETURNING SourceId; ");

        var sourceId = await connection.QuerySingleAsync<long>(
            insertSql.ToString(),
            new
            {
                SeriesId = seriesId,
                Path = path,
                Role = (int)role,
            },
            transaction);

        return sourceId;
    }

    /// <summary>
    /// MangaSeriesTags テーブルのレコードを置換します（DELETE → INSERT）。
    /// 既存作品更新時の使用を想定しており、外部から接続とトランザクションを受け取ります。
    /// </summary>
    /// <param name="connection">DB接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="seriesId">親の MangaSeries の SeriesId。</param>
    /// <param name="tags">保存するタグ一覧。TagId > 0 のもののみが対象。</param>
    /// <returns>完了時にコンプリートする ValueTask。</returns>
    public async ValueTask ReplaceSeriesTagsInTransactionAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        long seriesId,
        IEnumerable<MangaTag> tags)
    {
        // 既存タグを削除
        var deleteSql = " DELETE FROM MangaSeriesTags WHERE SeriesId = :SeriesId; ";
        await connection.ExecuteAsync(
            deleteSql,
            new { SeriesId = seriesId },
            transaction);

        // 新しいタグを挿入
        var insertSql = new StringBuilder();
        insertSql.AppendLine(" INSERT INTO MangaSeriesTags ( ");
        insertSql.AppendLine(" 	  SeriesId ");
        insertSql.AppendLine(" 	, TagId ");
        insertSql.AppendLine(" ) VALUES ( ");
        insertSql.AppendLine(" 	  :SeriesId ");
        insertSql.AppendLine(" 	, :TagId ");
        insertSql.AppendLine(" ); ");

        // TagId > 0 のタグのみ保存（未保存タグ TagId=0 は除外）
        var validTags = tags.Where(t => t.TagId > 0).ToList();
        foreach (var tag in validTags)
        {
            await connection.ExecuteAsync(
                insertSql.ToString(),
                new { SeriesId = seriesId, TagId = tag.TagId },
                transaction);
        }
    }

    /// <summary>
    /// MangaSeries のサムネイル情報（ThumbnailFileName, ThumbnailStatus）を更新します。
    /// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
    /// </summary>
    /// <param name="connection">DB接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="seriesId">更新対象の SeriesId。</param>
    /// <param name="thumbnailFileName">新しいサムネイルファイル名。</param>
    /// <param name="thumbnailStatus">新しいサムネイル状態。</param>
    /// <returns>完了時にコンプリートする ValueTask。</returns>
    public async ValueTask UpdateSeriesThumbnailAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        long seriesId,
        string thumbnailFileName,
        MangaBinder.Bindings.ThumbnailStatus thumbnailStatus)
    {
        var updateSql = new StringBuilder();
        updateSql.AppendLine(" UPDATE MangaSeries ");
        updateSql.AppendLine(" SET ");
        updateSql.AppendLine(" 	  ThumbnailFileName = :ThumbnailFileName ");
        updateSql.AppendLine(" 	, ThumbnailStatus = :ThumbnailStatus ");
        updateSql.AppendLine(" WHERE ");
        updateSql.AppendLine(" 	SeriesId = :SeriesId; ");

        await connection.ExecuteAsync(
            updateSql.ToString(),
            new
            {
                ThumbnailFileName = thumbnailFileName,
                ThumbnailStatus = (int)thumbnailStatus,
                SeriesId = seriesId,
            },
            transaction);
    }

    /// <summary>
    /// MangaSeries の素材フォルダ作成日時（MaterialFolderCreatedAt）を更新します。
    /// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
    /// </summary>
    /// <param name="connection">DB接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="seriesId">更新対象の SeriesId。</param>
    /// <param name="materialFolderCreatedAt">素材フォルダの作成日時。</param>
    /// <returns>完了時にコンプリートする ValueTask。</returns>
    public async ValueTask UpdateMaterialFolderCreatedAtAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        long seriesId,
        DateTime? materialFolderCreatedAt)
    {
        var updateSql = new StringBuilder();
        updateSql.AppendLine(" UPDATE MangaSeries ");
        updateSql.AppendLine(" SET ");
        updateSql.AppendLine(" 	  MaterialFolderCreatedAt = :MaterialFolderCreatedAt ");
        updateSql.AppendLine(" WHERE ");
        updateSql.AppendLine(" 	SeriesId = :SeriesId; ");

        await connection.ExecuteAsync(
            updateSql.ToString(),
            new
            {
                MaterialFolderCreatedAt = materialFolderCreatedAt,
                SeriesId = seriesId,
            },
            transaction);
    }

    /// <summary>
    /// 新規 MangaSeries をインサートし、採番された SeriesId を返します。
    /// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
    /// </summary>
    /// <param name="connection">DB接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="series">挿入する MangaSeries。SeriesId は上書きされます。</param>
    /// <param name="updateSource">更新元を表す文字列（例：EditorPage、MaterialFolderScanner など）。</param>
    /// <returns>採番された SeriesId。</returns>
    public async ValueTask<long> InsertSeriesInTransactionAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        MangaSeries series,
        string updateSource)
    {
        var insertSql = new StringBuilder();
        insertSql.AppendLine(" INSERT INTO MangaSeries ( ");
        insertSql.AppendLine(" 	  NormalizedTitleInternal ");
        insertSql.AppendLine(" 	, Title ");
        insertSql.AppendLine(" 	, ShortTitle ");
        insertSql.AppendLine(" 	, Author ");
        insertSql.AppendLine(" 	, Description ");
        insertSql.AppendLine(" 	, SeriesCompleted ");
        insertSql.AppendLine(" 	, IsOwnedCompleted ");
        insertSql.AppendLine(" 	, StartVolume ");
        insertSql.AppendLine(" 	, EndVolume ");
        insertSql.AppendLine(" 	, OwnedMaxVolume ");
        insertSql.AppendLine(" 	, NormalizedTitleExternal ");
        insertSql.AppendLine(" 	, ThumbnailFileName ");
        insertSql.AppendLine(" 	, ThumbnailStatus ");
        insertSql.AppendLine(" 	, Publisher ");
        insertSql.AppendLine(" 	, GoogleBooksImportStatus ");
        insertSql.AppendLine(" 	, DescriptionSource ");
        insertSql.AppendLine(" 	, Memo ");
        insertSql.AppendLine(" 	, HasNestedArchive ");
        insertSql.AppendLine(" 	, IsIncomplete ");
        insertSql.AppendLine(" 	, MaterialFolderCreatedAt ");
        insertSql.AppendLine(" 	, ManuallyEditedAt ");
        insertSql.AppendLine(" 	, UpdateSource ");
        insertSql.AppendLine(" ) VALUES ( ");
        insertSql.AppendLine(" 	  :NormalizedTitleInternal ");
        insertSql.AppendLine(" 	, :Title ");
        insertSql.AppendLine(" 	, :ShortTitle ");
        insertSql.AppendLine(" 	, :Author ");
        insertSql.AppendLine(" 	, :Description ");
        insertSql.AppendLine(" 	, :SeriesCompleted ");
        insertSql.AppendLine(" 	, :IsOwnedCompleted ");
        insertSql.AppendLine(" 	, :StartVolume ");
        insertSql.AppendLine(" 	, :EndVolume ");
        insertSql.AppendLine(" 	, :OwnedMaxVolume ");
        insertSql.AppendLine(" 	, :NormalizedTitleExternal ");
        insertSql.AppendLine(" 	, :ThumbnailFileName ");
        insertSql.AppendLine(" 	, :ThumbnailStatus ");
        insertSql.AppendLine(" 	, :Publisher ");
        insertSql.AppendLine(" 	, :GoogleBooksImportStatus ");
        insertSql.AppendLine(" 	, :DescriptionSource ");
        insertSql.AppendLine(" 	, :Memo ");
        insertSql.AppendLine(" 	, :HasNestedArchive ");
        insertSql.AppendLine(" 	, :IsIncomplete ");
        insertSql.AppendLine(" 	, :MaterialFolderCreatedAt ");
        insertSql.AppendLine(" 	, :ManuallyEditedAt ");
        insertSql.AppendLine(" 	, :UpdateSource ");
        insertSql.AppendLine(" ) ");
        insertSql.AppendLine(" RETURNING SeriesId; ");

        var seriesId = await connection.QuerySingleAsync<long>(insertSql.ToString(), new
        {
            NormalizedTitleInternal = MangaTitleHelper.NormalizeTitleInternal(series.Title),
            series.Title,
            series.ShortTitle,
            series.Author,
            series.Description,
            series.SeriesCompleted,
            series.IsOwnedCompleted,
            series.StartVolume,
            series.EndVolume,
            series.OwnedMaxVolume,
            series.NormalizedTitleExternal,
            ThumbnailFileName = string.Empty,
            ThumbnailStatus = (int)ThumbnailStatus.None,
            series.Publisher,
            GoogleBooksImportStatus = (int)GoogleBooksImportStatus.NotImported,
            DescriptionSource = (int)DescriptionSource.None,
            series.Memo,
            series.HasNestedArchive,
            series.IsIncomplete,
            series.MaterialFolderCreatedAt,
            series.ManuallyEditedAt,
            UpdateSource = updateSource,
        }, transaction);

        return seriesId;
    }

    /// <summary>
    /// MangaSeriesTags テーブルへタグをインサートします。
    /// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
    /// TagId &lt;= 0 のタグは保存対象外となります（未保存タグの防御）。
    /// </summary>
    /// <param name="connection">DB接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="seriesId">親の MangaSeries の SeriesId。</param>
    /// <param name="tags">保存するタグの一覧。</param>
    /// <returns>完了時にコンプリートする ValueTask。</returns>
    public async ValueTask InsertSeriesTagsInTransactionAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        long seriesId,
        IEnumerable<MangaTag> tags)
    {
        var insertSql = new StringBuilder();
        insertSql.AppendLine(" INSERT INTO MangaSeriesTags ( ");
        insertSql.AppendLine(" 	  SeriesId ");
        insertSql.AppendLine(" 	, TagId ");
        insertSql.AppendLine(" ) VALUES ( ");
        insertSql.AppendLine(" 	  :SeriesId ");
        insertSql.AppendLine(" 	, :TagId ");
        insertSql.AppendLine(" ); ");

        // TagId > 0 のタグのみ保存（未保存タグ TagId=0 は除外）
        var validTags = tags.Where(t => t.TagId > 0).ToList();
        foreach (var tag in validTags)
        {
            await connection.ExecuteAsync(
                insertSql.ToString(),
                new { SeriesId = seriesId, TagId = tag.TagId },
                transaction);
        }
    }

    /// <summary>
    /// MangaSources テーブルの Path をSourceId をキーとして更新します。
    /// トランザクション内での実行を想定しており、外部から接続とトランザクションを受け取ります。
    /// </summary>
    /// <param name="connection">DB接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="sourceId">更新対象の SourceId。</param>
    /// <param name="newPath">新しいフォルダパス。</param>
    /// <returns>完了時にコンプリートする ValueTask。</returns>
    public async ValueTask UpdateMangaSourcePathAsync(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        long sourceId,
        string newPath)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" UPDATE MangaSources ");
        sql.AppendLine(" SET ");
        sql.AppendLine(" 	  Path = :Path ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 	SourceId = :SourceId; ");

        await connection.ExecuteAsync(
            sql.ToString(),
            new
            {
                SourceId = sourceId,
                Path = newPath,
            },
            transaction);
    }

    /// <summary>
    /// 正式登録済み作品を削除します。
    /// MaterialArchiveEntries、MaterialArchives、MangaSeriesTags、MangaSources、MangaSeries をトランザクション内で順に削除します。
    /// </summary>
    /// <param name="seriesId">削除対象の SeriesId。</param>
    /// <returns>完了時にコンプリートする ValueTask。</returns>
    public async ValueTask DeleteSeriesAsync(long seriesId)
    {
        using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. MaterialArchiveEntries を削除
            // MaterialArchives.SeriesId が対象の seriesId である MaterialArchiveId に紐づく行を削除
            var deleteArchiveEntriesSql = new StringBuilder();
            deleteArchiveEntriesSql.AppendLine(" DELETE FROM MaterialArchiveEntries ");
            deleteArchiveEntriesSql.AppendLine(" WHERE ");
            deleteArchiveEntriesSql.AppendLine(" 	MaterialArchiveId IN ( ");
            deleteArchiveEntriesSql.AppendLine(" 		SELECT MaterialArchiveId FROM MaterialArchives ");
            deleteArchiveEntriesSql.AppendLine(" 		WHERE SeriesId = :SeriesId ");
            deleteArchiveEntriesSql.AppendLine(" 	); ");

            await connection.ExecuteAsync(deleteArchiveEntriesSql.ToString(), new { SeriesId = seriesId }, transaction);

            // 2. MaterialArchives を削除
            var deleteArchivesSql = new StringBuilder();
            deleteArchivesSql.AppendLine(" DELETE FROM MaterialArchives ");
            deleteArchivesSql.AppendLine(" WHERE ");
            deleteArchivesSql.AppendLine(" 	SeriesId = :SeriesId; ");

            await connection.ExecuteAsync(deleteArchivesSql.ToString(), new { SeriesId = seriesId }, transaction);

            // 3. MangaSeriesTags を削除
            var deleteSeriesTagsSql = new StringBuilder();
            deleteSeriesTagsSql.AppendLine(" DELETE FROM MangaSeriesTags ");
            deleteSeriesTagsSql.AppendLine(" WHERE ");
            deleteSeriesTagsSql.AppendLine(" 	SeriesId = :SeriesId; ");

            await connection.ExecuteAsync(deleteSeriesTagsSql.ToString(), new { SeriesId = seriesId }, transaction);

            // 4. MangaSources を削除
            var deleteSourcesSql = new StringBuilder();
            deleteSourcesSql.AppendLine(" DELETE FROM MangaSources ");
            deleteSourcesSql.AppendLine(" WHERE ");
            deleteSourcesSql.AppendLine(" 	SeriesId = :SeriesId; ");

            await connection.ExecuteAsync(deleteSourcesSql.ToString(), new { SeriesId = seriesId }, transaction);

            // 4.5. BindingHistory を削除（外部キー制約の自動削除には依存しない）
            var deleteBindingHistorySql = new StringBuilder();
            deleteBindingHistorySql.AppendLine(" DELETE FROM BindingHistory ");
            deleteBindingHistorySql.AppendLine(" WHERE ");
            deleteBindingHistorySql.AppendLine(" SeriesId = :SeriesId; ");

            await connection.ExecuteAsync(deleteBindingHistorySql.ToString(), new { SeriesId = seriesId }, transaction);

            // 5. MangaSeries を削除
            var deleteSeriesSql = new StringBuilder();
            deleteSeriesSql.AppendLine(" DELETE FROM MangaSeries ");
            deleteSeriesSql.AppendLine(" WHERE ");
            deleteSeriesSql.AppendLine(" 	SeriesId = :SeriesId; ");

            await connection.ExecuteAsync(deleteSeriesSql.ToString(), new { SeriesId = seriesId }, transaction);

            // すべての DELETE が成功した場合のみ Commit
            transaction.Commit();
        }
        catch
        {
            // 失敗時は Rollback して例外を再送出
            transaction.Rollback();
            throw;
        }
    }
}
