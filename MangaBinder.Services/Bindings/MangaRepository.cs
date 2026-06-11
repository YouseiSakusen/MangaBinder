using Dapper;
using MangaBinder.Jobs;
using MangaBinder.Settings;
using MangaBinder.Tags;
using System.Data.SQLite;

namespace MangaBinder.Bindings;

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
    /// 全 <see cref="MangaSeries"/> を <see cref="MangaSource"/> 込みで取得します。
    /// </summary>
    /// <returns>タイトル昇順で並んだ <see cref="MangaSeries"/> の読み取り専用リスト。</returns>
    public async ValueTask<IReadOnlyList<MangaSeries>> GetAllSeriesAsync()
    {
        const string seriesSql = """
            SELECT SeriesId
                 , NormalizedTitleInternal
                 , Title
                 , ShortTitle
                 , ThumbnailFileName
                 , Author
                 , Description
                 , SeriesCompleted
                 , IsOwnedCompleted
                 , IsSourceMissing
                 , StartVolume
                 , EndVolume
                 , BoundEndVolume
                 , OwnedMaxVolume
                 , NormalizedTitleExternal
                 , UpdatedAt
                 , ThumbnailStatus
                 , Publisher
                 , GoogleBooksImportStatus
                 , GoogleBooksImportedAt
                 , GoogleBooksImportMessage
                 , DescriptionSource
                 , DescriptionSourceTitle
            FROM MangaSeries
            ORDER BY NormalizedTitleInternal
            """;

        const string sourcesSql = """
            SELECT SourceId
                 , SeriesId
                 , Path
                 , Role
            FROM MangaSources
            ORDER BY SeriesId, Role, Path
            """;

        using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
        await connection.OpenAsync();

        var seriesList = (await connection.QueryAsync<MangaSeries>(seriesSql)).AsList();
        var sources = await connection.QueryAsync<MangaSource>(sourcesSql);

        // MangaSeriesTags + MangaTags を一括取得（N+1禁止）
        var seriesTags = await connection.QueryAsync<(long SeriesId, long TagId, string Name, int DisplayOrder, bool ShowOnSeriesCard)>(
            "SELECT st.SeriesId, t.TagId, t.Name, t.DisplayOrder, t.ShowOnSeriesCard " +
            "FROM MangaSeriesTags st " +
            "INNER JOIN MangaTags t ON t.TagId = st.TagId " +
            "ORDER BY st.SeriesId, t.DisplayOrder ASC, t.TagId ASC");

        var seriesDict = seriesList.ToDictionary(s => s.SeriesId);

        foreach (var source in sources)
        {
            if (seriesDict.TryGetValue(source.SeriesId, out var series))
                series.Sources.Add(source);
        }

        // メモリ上でタグを紐付け
        foreach (var row in seriesTags)
        {
            if (!seriesDict.TryGetValue(row.SeriesId, out var series))
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
    /// Home 画面の表示状態を取得します。
    /// </summary>
    /// <returns>Home 画面の状態情報。</returns>
    public async ValueTask<HomeStateInformation> GetHomeStateInformationAsync()
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM MangaSeries)                                                      AS SeriesCount
              , (SELECT COUNT(*) > 0 FROM SourceFolders WHERE Role = 0)                                 AS HasMaterialSourceFolder
              , (SELECT COUNT(*) > 0 FROM JobQueue WHERE Type = @MaterialScan AND Status = @Success)     AS HasCompletedMaterialFolderScanJob
            """;

        using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
        await connection.OpenAsync();

        var row = await connection.QuerySingleAsync(
            sql,
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
    /// 指定した作品一覧のタグを MangaSeriesTags テーブルへ保存します。
    /// 各作品の既存レコードを DELETE してから series.Tags を INSERT します。
    /// </summary>
    /// <param name="seriesList">保存対象の作品一覧。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    public async ValueTask SaveSeriesTagsAsync(
        IEnumerable<MangaSeries> seriesList,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var series in seriesList)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM MangaSeriesTags WHERE SeriesId = @SeriesId",
                    new { series.SeriesId },
                    transaction);

                foreach (var tag in series.Tags)
                {
                    await connection.ExecuteAsync(
                        "INSERT INTO MangaSeriesTags (SeriesId, TagId) VALUES (@SeriesId, @TagId)",
                        new { series.SeriesId, tag.TagId },
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
}
