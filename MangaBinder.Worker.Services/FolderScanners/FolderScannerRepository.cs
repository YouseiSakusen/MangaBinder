using System.Data;
using System.Data.SQLite;
using System.Text;
using Dapper;
using MangaBinder.Bindings;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// SQLite を使用したフォルダスキャン用リポジトリの実装クラスです。
/// </summary>
public class FolderScannerRepository : IFolderScannerRepository
{
    /// <summary>DB接続文字列。</summary>
    private readonly string connectionString;

    /// <summary>ロガー。</summary>
    private readonly ILogger logger;

    /// <summary>
    /// <see cref="FolderScannerRepository"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="workerContext">Worker 実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    public FolderScannerRepository(WorkerContext workerContext, ILogger<FolderScannerRepository> logger)
    {
        this.connectionString = workerContext.ConnectionString;
        this.logger = logger;
    }

    /// <summary>
    /// 指定された役割のスキャン対象フォルダパス一覧を非同期で取得します。
    /// </summary>
    /// <param name="role">フォルダの役割を表す値。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>フォルダのフルパス一覧。</returns>
    public async ValueTask<IEnumerable<string>> GetSourceFoldersAsync(int role, CancellationToken ct)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	FolderPath ");
        sql.AppendLine(" FROM ");
        sql.AppendLine(" 	SourceFolders ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 	Role = :Role; ");

        using var conn = new SQLiteConnection(this.connectionString);
        await conn.OpenAsync(ct);
        return await conn.QueryAsync<string>(sql.ToString(), new { Role = role });
    }

    /// <summary>
    /// 素材スキャン結果を 1 件単位で UPSERT 保存します。
    /// Author は更新対象から除外し、EndVolume / IsOwnedCompleted 等を反映します。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async ValueTask<MangaSeries> SaveMaterialSeriesAsync(MangaSeries series, CancellationToken ct)
    {
        using var conn = new SQLiteConnection(this.connectionString);
        await conn.OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        long seriesId;
        try
        {
            seriesId = await this.UpsertMaterialSeriesAsync(conn, tx, series);
            await this.SyncSourcesAsync(conn, tx, seriesId, series.Sources, new[] { (int)FolderRole.Material });
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return await this.GetSeriesWithSourcesAsync(conn, seriesId);
    }

    /// <summary>
    /// 製本済みスキャン結果を 1 件単位で UPSERT 保存します。
    /// Author を上書きし、BoundEndVolume / SeriesCompleted 等を反映します。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async ValueTask<MangaSeries> SaveBindingSeriesAsync(MangaSeries series, CancellationToken ct)
    {
        using var conn = new SQLiteConnection(this.connectionString);
        await conn.OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        long seriesId;
        try
        {
            seriesId = await this.UpsertBindingSeriesAsync(conn, tx, series);
            await this.SyncSourcesAsync(conn, tx, seriesId, series.Sources, new[] { (int)FolderRole.Binding, (int)FolderRole.DefaultBinding });
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return await this.GetSeriesWithSourcesAsync(conn, seriesId);
    }

    /// <summary>
    /// 素材スキャン向けの MangaSeries UPSERT を実行し、SeriesId を返します。
    /// Author は INSERT 時のみ設定され、UPDATE 時は除外して既存値を維持します。
    /// フォルダ名由来の情報（Title, ShortTitle, SeriesCompleted, IsOwnedCompleted, StartVolume, EndVolume）はスキャン結果で上書きします。
    /// OwnedMaxVolume は IsOwnedMaxVolumeManuallyEdited フラグで保護します。
    /// </summary>
    private async ValueTask<long> UpsertMaterialSeriesAsync(SQLiteConnection conn, SQLiteTransaction tx, MangaSeries series)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" INSERT INTO MangaSeries ( ");
        sql.AppendLine(" 	  NormalizedTitleInternal ");
        sql.AppendLine(" 	, Title ");
        sql.AppendLine(" 	, ShortTitle ");
        sql.AppendLine(" 	, SeriesCompleted ");
        sql.AppendLine(" 	, IsOwnedCompleted ");
        sql.AppendLine(" 	, IsSourceMissing ");
        sql.AppendLine(" 	, StartVolume ");
        sql.AppendLine(" 	, EndVolume ");
        sql.AppendLine(" 	, OwnedMaxVolume ");
        sql.AppendLine(" 	, UpdatedAt ");
        sql.AppendLine(" 	, Memo ");
        sql.AppendLine(" 	, ManuallyEditedAt ");
        sql.AppendLine(" 	, IsOwnedMaxVolumeManuallyEdited ");
        sql.AppendLine(" ) VALUES ( ");
        sql.AppendLine(" 	  :NormalizedTitleInternal ");
        sql.AppendLine(" 	, :Title ");
        sql.AppendLine(" 	, :ShortTitle ");
        sql.AppendLine(" 	, :SeriesCompleted ");
        sql.AppendLine(" 	, :IsOwnedCompleted ");
        sql.AppendLine(" 	, 0 ");
        sql.AppendLine(" 	, :StartVolume ");
        sql.AppendLine(" 	, :EndVolume ");
        sql.AppendLine(" 	, :OwnedMaxVolume ");
        sql.AppendLine(" 	, DATETIME('now', 'localtime') ");
        sql.AppendLine(" 	, '' ");
        sql.AppendLine(" 	, NULL ");
        sql.AppendLine(" 	, 0 ");
        sql.AppendLine(" ) ");
        sql.AppendLine(" ON CONFLICT (NormalizedTitleInternal) DO UPDATE SET ");
        // フォルダ名由来の正規情報としてスキャン結果で上書き
        sql.AppendLine(" 	  Title             = excluded.Title ");
        sql.AppendLine(" 	, ShortTitle        = excluded.ShortTitle ");
        sql.AppendLine(" 	, SeriesCompleted   = excluded.SeriesCompleted ");
        sql.AppendLine(" 	, IsOwnedCompleted  = excluded.IsOwnedCompleted ");
        sql.AppendLine(" 	, IsSourceMissing   = 0 ");
        // StartVolume と EndVolume はフォルダ名由来の正規情報として直接更新
        sql.AppendLine(" 	, StartVolume       = excluded.StartVolume ");
        sql.AppendLine(" 	, EndVolume         = excluded.EndVolume ");
        // OwnedMaxVolume は IsOwnedMaxVolumeManuallyEdited で保護
        sql.AppendLine(" 	, OwnedMaxVolume    = CASE ");
        sql.AppendLine(" 	                        WHEN IsOwnedMaxVolumeManuallyEdited = 1 THEN OwnedMaxVolume ");
        sql.AppendLine(" 	                        WHEN OwnedMaxVolume IS NULL THEN excluded.OwnedMaxVolume ");
        sql.AppendLine(" 	                        WHEN excluded.OwnedMaxVolume IS NULL THEN OwnedMaxVolume ");
        sql.AppendLine(" 	                        ELSE MAX(OwnedMaxVolume, excluded.OwnedMaxVolume) ");
        sql.AppendLine(" 	                      END ");
        sql.AppendLine(" 	, UpdatedAt         = DATETIME('now', 'localtime') ");
        sql.AppendLine(" RETURNING SeriesId; ");

        return await conn.QuerySingleAsync<long>(sql.ToString(), new
        {
            series.NormalizedTitleInternal,
            series.Title,
            series.ShortTitle,
            series.SeriesCompleted,
            series.IsOwnedCompleted,
            series.StartVolume,
            series.EndVolume,
            series.OwnedMaxVolume,
        }, tx);
    }

    /// <summary>
    /// 製本済みスキャン向けの MangaSeries UPSERT を実行し、SeriesId を返します。
    /// 既存作品に一致した場合、メタ情報（Title、ShortTitle、Author、SeriesCompleted、StartVolume、EndVolume）は上書きしません。
    /// 更新対象は BoundEndVolume と UpdatedAt のみです。
    /// </summary>
    private async ValueTask<long> UpsertBindingSeriesAsync(SQLiteConnection conn, SQLiteTransaction tx, MangaSeries series)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" INSERT INTO MangaSeries ( ");
        sql.AppendLine(" 	  NormalizedTitleInternal ");
        sql.AppendLine(" 	, Title ");
        sql.AppendLine(" 	, ShortTitle ");
        sql.AppendLine(" 	, Author ");
        sql.AppendLine(" 	, SeriesCompleted ");
        sql.AppendLine(" 	, StartVolume ");
        sql.AppendLine(" 	, EndVolume ");
        sql.AppendLine(" 	, BoundEndVolume ");
        sql.AppendLine(" 	, UpdatedAt ");
        sql.AppendLine(" 	, Memo ");
        sql.AppendLine(" 	, ManuallyEditedAt ");
        sql.AppendLine(" 	, IsOwnedMaxVolumeManuallyEdited ");
        sql.AppendLine(" ) VALUES ( ");
        sql.AppendLine(" 	  :NormalizedTitleInternal ");
        sql.AppendLine(" 	, :Title ");
        sql.AppendLine(" 	, :ShortTitle ");
        sql.AppendLine(" 	, :Author ");
        sql.AppendLine(" 	, :SeriesCompleted ");
        sql.AppendLine(" 	, :StartVolume ");
        sql.AppendLine(" 	, :EndVolume ");
        sql.AppendLine(" 	, :BoundEndVolume ");
        sql.AppendLine(" 	, DATETIME('now', 'localtime') ");
        sql.AppendLine(" 	, '' ");
        sql.AppendLine(" 	, NULL ");
        sql.AppendLine(" 	, 0 ");
        sql.AppendLine(" ) ");
        sql.AppendLine(" ON CONFLICT (NormalizedTitleInternal) DO UPDATE SET ");
        // 既存作品の場合、メタ情報は上書きしない
        // 更新対象は BoundEndVolume と UpdatedAt のみ
        sql.AppendLine(" 	  BoundEndVolume    = CASE ");
        sql.AppendLine(" 	                        WHEN BoundEndVolume IS NULL THEN excluded.BoundEndVolume ");
        sql.AppendLine(" 	                        WHEN excluded.BoundEndVolume IS NULL THEN BoundEndVolume ");
        sql.AppendLine(" 	                        ELSE MAX(BoundEndVolume, excluded.BoundEndVolume) ");
        sql.AppendLine(" 	                      END ");
        sql.AppendLine(" 	, UpdatedAt         = DATETIME('now', 'localtime') ");
        sql.AppendLine(" RETURNING SeriesId; ");

        return await conn.QuerySingleAsync<long>(sql.ToString(), new
        {
            series.NormalizedTitleInternal,
            series.Title,
            series.ShortTitle,
            series.Author,
            series.SeriesCompleted,
            series.StartVolume,
            series.EndVolume,
            series.BoundEndVolume,
        }, tx);
    }

    /// <summary>
    /// 指定された SeriesId・役割に一致する MangaSources を削除します。
    /// </summary>
    /// <param name="conn">DB接続。</param>
    /// <param name="tx">トランザクション。</param>
    /// <param name="seriesId">削除対象の作品ID。</param>
    /// <param name="roles">削除対象の役割値の配列。</param>
    private async ValueTask DeleteSourcesByRoleAsync(SQLiteConnection conn, SQLiteTransaction tx, long seriesId, int[] roles)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" DELETE FROM MangaSources ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 		SeriesId = :SeriesId ");
        sql.AppendLine(" 	AND Role IN :Roles; ");

        await conn.ExecuteAsync(sql.ToString(), new { SeriesId = seriesId, Roles = roles }, tx);
    }

    /// <summary>
    /// Sources リストの内容を MangaSources テーブルに一括 INSERT します。
    /// </summary>
    /// <param name="conn">DB接続。</param>
    /// <param name="tx">トランザクション。</param>
    /// <param name="seriesId">親の作品ID。</param>
    /// <param name="sources">登録する所在情報リスト。</param>
    private async ValueTask InsertSourcesAsync(SQLiteConnection conn, SQLiteTransaction tx, long seriesId, List<MangaSource> sources)
    {
        if (sources.Count == 0)
            return;

        var sql = new StringBuilder();
        sql.AppendLine(" INSERT INTO MangaSources ( ");
        sql.AppendLine(" 	  SeriesId ");
        sql.AppendLine(" 	, Path ");
        sql.AppendLine(" 	, Role ");
        sql.AppendLine(" ) VALUES ( ");
        sql.AppendLine(" 	  :SeriesId ");
        sql.AppendLine(" 	, :Path ");
        sql.AppendLine(" 	, :Role ");
        sql.AppendLine(" ); ");

        var rows = sources.Select(s => new { SeriesId = seriesId, s.Path, s.Role });
        await conn.ExecuteAsync(sql.ToString(), rows, tx);
    }

    /// <summary>
    /// MangaSources を差分同期します。
    /// 既存の MangaSources と スキャン結果を比較し、追加・削除を実施します。
    /// SourceId を維持するため、DELETE/INSERT ではなく差分同期で対応します。
    /// 削除対象の Source に紐づくアーカイブキャッシュ（MaterialArchiveEntries、MaterialArchives）も削除します。
    /// </summary>
    /// <param name="conn">DB接続。</param>
    /// <param name="tx">トランザクション。</param>
    /// <param name="seriesId">対象の作品ID。</param>
    /// <param name="newSources">スキャン結果の Sources。</param>
    /// <param name="roles">対象とする役割（Material等）。</param>
    private async ValueTask SyncSourcesAsync(SQLiteConnection conn, SQLiteTransaction tx, long seriesId, List<MangaSource> newSources, int[] roles)
    {
        // DB から既存の MangaSources を取得
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	  SourceId ");
        sql.AppendLine(" 	, SeriesId ");
        sql.AppendLine(" 	, Path ");
        sql.AppendLine(" 	, Role ");
        sql.AppendLine(" FROM MangaSources ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 	SeriesId = :SeriesId ");
        sql.AppendLine(" 	AND Role IN :Roles; ");

        var existingSourceRows = (await conn.QueryAsync(
            sql.ToString(),
            new { SeriesId = seriesId, Roles = roles },
            tx)).ToList();

        // 既存 Sources を (SeriesId, Role, Path) で Dictionary 化
        // Value には SourceId の情報を保持
        var existingDict = new Dictionary<(long, int, string), long>();
        foreach (var row in existingSourceRows)
        {
            var sourceId = (long)row.SourceId;
            var existingSeriesId = (long)row.SeriesId;
            var existingRole = (int)row.Role;
            var existingPath = (string)row.Path;
            existingDict.Add((existingSeriesId, existingRole, existingPath), sourceId);
        }

        // スキャン結果を走査
        foreach (var newSource in newSources)
        {
            var key = (seriesId, (int)newSource.Role, newSource.Path);
            if (existingDict.ContainsKey(key))
            {
                // 既存 → Dictionary から除去（削除対象外）
                existingDict.Remove(key);
            }
            else
            {
                // 新規 → INSERT
                var insertSql = new StringBuilder();
                insertSql.AppendLine(" INSERT INTO MangaSources ( ");
                insertSql.AppendLine(" 	  SeriesId ");
                insertSql.AppendLine(" 	, Path ");
                insertSql.AppendLine(" 	, Role ");
                insertSql.AppendLine(" ) VALUES ( ");
                insertSql.AppendLine(" 	  :SeriesId ");
                insertSql.AppendLine(" 	, :Path ");
                insertSql.AppendLine(" 	, :Role ");
                insertSql.AppendLine(" ); ");

                await conn.ExecuteAsync(insertSql.ToString(), new { SeriesId = seriesId, newSource.Path, newSource.Role }, tx);
            }
        }

        // Dictionary に残った要素 → DELETE
        if (existingDict.Count > 0)
        {
            var sourceIdsToDelete = existingDict.Values.ToList();

            // MangaSources 削除前に、関連するアーカイブキャッシュを削除
            // 削除順: MaterialArchiveEntries → MaterialArchives → MangaSources

            // 1. MaterialArchiveEntries の削除
            var deleteEntriesSql = new StringBuilder();
            deleteEntriesSql.AppendLine(" DELETE FROM MaterialArchiveEntries ");
            deleteEntriesSql.AppendLine(" WHERE MaterialArchiveId IN ( ");
            deleteEntriesSql.AppendLine(" 	SELECT MaterialArchiveId FROM MaterialArchives ");
            deleteEntriesSql.AppendLine(" 	WHERE SourceId IN :SourceIds ");
            deleteEntriesSql.AppendLine(" ); ");
            await conn.ExecuteAsync(deleteEntriesSql.ToString(), new { SourceIds = sourceIdsToDelete }, tx);

            // 2. MaterialArchives の削除
            var deleteArchivesSql = new StringBuilder();
            deleteArchivesSql.AppendLine(" DELETE FROM MaterialArchives ");
            deleteArchivesSql.AppendLine(" WHERE SourceId IN :SourceIds; ");
            await conn.ExecuteAsync(deleteArchivesSql.ToString(), new { SourceIds = sourceIdsToDelete }, tx);

            // 3. MangaSources の削除
            var deleteSql = new StringBuilder();
            deleteSql.AppendLine(" DELETE FROM MangaSources ");
            deleteSql.AppendLine(" WHERE SourceId IN :SourceIds; ");
            await conn.ExecuteAsync(deleteSql.ToString(), new { SourceIds = sourceIdsToDelete }, tx);
        }
    }

    /// <summary>
    /// 指定された SeriesId に対応する MangaSeries と MangaSources を取得します。
    /// 保存後のDB最新状態を再構築するために使用します。
    /// </summary>
    /// <param name="conn">DB接続。</param>
    /// <param name="seriesId">取得対象の作品ID。</param>
    /// <returns>DB最新状態の <see cref="MangaSeries"/>。</returns>
    private async ValueTask<MangaSeries> GetSeriesWithSourcesAsync(SQLiteConnection conn, long seriesId)
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
        seriesSql.AppendLine(" 	, IsSourceMissing ");
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
        seriesSql.AppendLine(" 	, DescriptionSource ");
        seriesSql.AppendLine(" 	, DescriptionSourceTitle ");
        seriesSql.AppendLine(" 	, HasNestedArchive ");
        seriesSql.AppendLine(" FROM ");
        seriesSql.AppendLine(" 	MangaSeries ");
        seriesSql.AppendLine(" WHERE ");
        seriesSql.AppendLine(" 	SeriesId = :SeriesId; ");

        var series = await conn.QuerySingleAsync<MangaSeries>(seriesSql.ToString(), new { SeriesId = seriesId });

        var sourcesSql = new StringBuilder();
        sourcesSql.AppendLine(" SELECT ");
        sourcesSql.AppendLine(" 	  SourceId ");
        sourcesSql.AppendLine(" 	, SeriesId ");
        sourcesSql.AppendLine(" 	, Path ");
        sourcesSql.AppendLine(" 	, Role ");
        sourcesSql.AppendLine(" FROM ");
        sourcesSql.AppendLine(" 	MangaSources ");
        sourcesSql.AppendLine(" WHERE ");
        sourcesSql.AppendLine(" 	SeriesId = :SeriesId; ");

        var sources = await conn.QueryAsync<MangaSource>(sourcesSql.ToString(), new { series.SeriesId });
        series.Sources.AddRange(sources);

        return series;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async ValueTask<bool> HasLimitExceededAsync(CancellationToken ct)
    {
        const string sql = " SELECT COUNT(*) FROM MangaSeries WHERE ThumbnailStatus = :LimitExceeded; ";

        using var conn = new SQLiteConnection(this.connectionString);
        await conn.OpenAsync(ct);
        var count = await conn.ExecuteScalarAsync<int>(sql, new { LimitExceeded = (int)ThumbnailStatus.LimitExceeded });
        return count > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<Dictionary<long, MangaSource>> GetSourcesByFolderRoleAsync(int role, IEnumerable<string> sourceFolderPaths, CancellationToken ct)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	  SourceId ");
        sql.AppendLine(" 	, SeriesId ");
        sql.AppendLine(" 	, Path ");
        sql.AppendLine(" 	, Role ");
        sql.AppendLine(" FROM MangaSources ");
        sql.AppendLine(" WHERE Role = :Role; ");

        using var conn = new SQLiteConnection(this.connectionString);
        await conn.OpenAsync(ct);
        var allSources = (await conn.QueryAsync<MangaSource>(sql.ToString(), new { Role = role })).ToList();

        // 指定されたフォルダパス配下に存在するソースのみをフィルタリング
        var result = new Dictionary<long, MangaSource>();
        var folderPathList = sourceFolderPaths.ToList();

        foreach (var source in allSources)
        {
            // Path がいずれかのフォルダパス配下に存在するかを確認
            var sourcePath = source.Path;
            foreach (var folderPath in folderPathList)
            {
                // フォルダパスの末尾のセパレーターを正規化
                var normalizedFolderPath = folderPath.EndsWith(Path.DirectorySeparatorChar.ToString()) 
                    ? folderPath 
                    : folderPath + Path.DirectorySeparatorChar;

                if (sourcePath.StartsWith(normalizedFolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    result[source.SourceId] = source;
                    break;
                }
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask DeleteSourcesByIdAsync(IEnumerable<long> sourceIds, CancellationToken ct)
    {
        var ids = sourceIds.ToList();
        if (ids.Count == 0)
            return;

        var sql = new StringBuilder();
        sql.AppendLine(" DELETE FROM MangaSources ");
        sql.AppendLine(" WHERE SourceId IN :SourceIds; ");

        using var conn = new SQLiteConnection(this.connectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(sql.ToString(), new { SourceIds = ids });
    }
}
