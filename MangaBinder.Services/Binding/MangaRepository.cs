using Dapper;
using MangaBinder.Settings;
using System.Data.SQLite;

namespace MangaBinder.Binding;

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

        var seriesDict = seriesList.ToDictionary(s => s.SeriesId);
        foreach (var source in sources)
        {
            if (seriesDict.TryGetValue(source.SeriesId, out var series))
                series.Sources.Add(source);
        }

        return seriesList;
    }
}
