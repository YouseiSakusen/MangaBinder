using Dapper;
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
    /// 全 <see cref="MangaSeries"/> を <see cref="MangaSource"/> 込みで取得します。
    /// 並び順は MangaSeriesStore が管理するため、ORDER BY は使用しません。
    /// </summary>
    /// <returns><see cref="MangaSeries"/> の読み取り専用リスト。</returns>
    public async ValueTask<IReadOnlyList<MangaSeries>> GetAllSeriesAsync()
    {
        var seriesSql = new StringBuilder();
        seriesSql.AppendLine(" SELECT ");
        seriesSql.AppendLine(" 	  SeriesId ");
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
        seriesSql.AppendLine(" 	, ManuallyEditedAt ");
        seriesSql.AppendLine(" 	, IsOwnedMaxVolumeManuallyEdited ");
        seriesSql.AppendLine(" FROM ");
        seriesSql.AppendLine(" 	MangaSeries; ");

        var sourcesSql = new StringBuilder();
        sourcesSql.AppendLine(" SELECT ");
        sourcesSql.AppendLine(" 	  SourceId ");
        sourcesSql.AppendLine(" 	, SeriesId ");
        sourcesSql.AppendLine(" 	, Path ");
        sourcesSql.AppendLine(" 	, Role ");
        sourcesSql.AppendLine(" FROM ");
        sourcesSql.AppendLine(" 	MangaSources ");
        sourcesSql.AppendLine(" ORDER BY ");
        sourcesSql.AppendLine(" 	  SeriesId ");
        sourcesSql.AppendLine(" 	, Role ");
        sourcesSql.AppendLine(" 	, Path; ");

        var seriesTagsSql = new StringBuilder();
        seriesTagsSql.AppendLine(" SELECT ");
        seriesTagsSql.AppendLine(" 	  st.SeriesId ");
        seriesTagsSql.AppendLine(" 	, t.TagId ");
        seriesTagsSql.AppendLine(" 	, t.Name ");
        seriesTagsSql.AppendLine(" 	, t.DisplayOrder ");
        seriesTagsSql.AppendLine(" 	, t.ShowOnSeriesCard ");
        seriesTagsSql.AppendLine(" FROM ");
        seriesTagsSql.AppendLine(" 	MangaSeriesTags st ");
        seriesTagsSql.AppendLine(" INNER JOIN MangaTags t ON ");
        seriesTagsSql.AppendLine(" 	t.TagId = st.TagId ");
        seriesTagsSql.AppendLine(" ORDER BY ");
        seriesTagsSql.AppendLine(" 	  st.SeriesId ");
        seriesTagsSql.AppendLine(" 	, t.DisplayOrder ");
        seriesTagsSql.AppendLine(" 	, t.TagId; ");

        using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
        await connection.OpenAsync();

        var seriesList = (await connection.QueryAsync<MangaSeries>(seriesSql.ToString())).AsList();
        var sources = await connection.QueryAsync<MangaSource>(sourcesSql.ToString());

        // MangaSeriesTags + MangaTags を一括取得（N+1禁止）
        var seriesTags = await connection.QueryAsync<(long SeriesId, long TagId, string Name, int DisplayOrder, bool ShowOnSeriesCard)>(
            seriesTagsSql.ToString());

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
    /// 指定した SeriesId の <see cref="MangaSeries"/> を <see cref="MangaSource"/> と <see cref="MangaTag"/> 込みで取得します。
    /// GetAllSeriesAsync() と同じパターンで関連データを一括取得します。
    /// </summary>
    /// <param name="seriesId">取得対象の SeriesId。</param>
    /// <returns>取得した <see cref="MangaSeries"/>。該当する作品が見つからない場合は null を返します。</returns>
    public async ValueTask<MangaSeries?> GetSeriesAsync(long seriesId)
    {
        var seriesSql = new StringBuilder();
        seriesSql.AppendLine(" SELECT ");
        seriesSql.AppendLine(" 	  SeriesId ");
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
        seriesSql.AppendLine(" 	, ManuallyEditedAt ");
        seriesSql.AppendLine(" 	, IsOwnedMaxVolumeManuallyEdited ");
        seriesSql.AppendLine(" FROM ");
        seriesSql.AppendLine(" 	MangaSeries ");
        seriesSql.AppendLine(" WHERE ");
        seriesSql.AppendLine(" 	SeriesId = :SeriesId; ");

        var sourcesSql = new StringBuilder();
        sourcesSql.AppendLine(" SELECT ");
        sourcesSql.AppendLine(" 	  SourceId ");
        sourcesSql.AppendLine(" 	, SeriesId ");
        sourcesSql.AppendLine(" 	, Path ");
        sourcesSql.AppendLine(" 	, Role ");
        sourcesSql.AppendLine(" FROM ");
        sourcesSql.AppendLine(" 	MangaSources ");
        sourcesSql.AppendLine(" WHERE ");
        sourcesSql.AppendLine(" 	SeriesId = :SeriesId ");
        sourcesSql.AppendLine(" ORDER BY ");
        sourcesSql.AppendLine(" 	  Role ");
        sourcesSql.AppendLine(" 	, Path; ");

        var seriesTagsSql = new StringBuilder();
        seriesTagsSql.AppendLine(" SELECT ");
        seriesTagsSql.AppendLine(" 	  st.SeriesId ");
        seriesTagsSql.AppendLine(" 	, t.TagId ");
        seriesTagsSql.AppendLine(" 	, t.Name ");
        seriesTagsSql.AppendLine(" 	, t.DisplayOrder ");
        seriesTagsSql.AppendLine(" 	, t.ShowOnSeriesCard ");
        seriesTagsSql.AppendLine(" FROM ");
        seriesTagsSql.AppendLine(" 	MangaSeriesTags st ");
        seriesTagsSql.AppendLine(" INNER JOIN MangaTags t ON ");
        seriesTagsSql.AppendLine(" 	t.TagId = st.TagId ");
        seriesTagsSql.AppendLine(" WHERE ");
        seriesTagsSql.AppendLine(" 	st.SeriesId = :SeriesId ");
        seriesTagsSql.AppendLine(" ORDER BY ");
        seriesTagsSql.AppendLine(" 	  t.DisplayOrder ");
        seriesTagsSql.AppendLine(" 	, t.TagId; ");

        using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
        await connection.OpenAsync();

        var series = await connection.QuerySingleOrDefaultAsync<MangaSeries>(
            seriesSql.ToString(),
            new { SeriesId = seriesId });

        // 作品が見つからない場合は null を返す
        if (series is null)
            return null;

        var sources = await connection.QueryAsync<MangaSource>(
            sourcesSql.ToString(),
            new { SeriesId = seriesId });

        // MangaSeriesTags + MangaTags を一括取得（N+1禁止）
        var seriesTags = await connection.QueryAsync<(long SeriesId, long TagId, string Name, int DisplayOrder, bool ShowOnSeriesCard)>(
            seriesTagsSql.ToString(),
            new { SeriesId = seriesId });

        foreach (var source in sources)
        {
            series.Sources.Add(source);
        }

        // メモリ上でタグを紐付け
        foreach (var row in seriesTags)
        {
            var tag = new MangaTag
            {
                TagId = row.TagId,
                Name = row.Name,
                DisplayOrder = row.DisplayOrder,
                ShowOnSeriesCard = row.ShowOnSeriesCard,
            };
            series.Tags.Add(tag);
        }

        return series;
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
    public async ValueTask UpdateSeriesAsync(MangaSeries series)
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
        sql.AppendLine(" 	, Memo = :Memo ");
        sql.AppendLine(" 	, StartVolume = :StartVolume ");
        sql.AppendLine(" 	, EndVolume = :EndVolume ");
        sql.AppendLine(" 	, SeriesCompleted = :SeriesCompleted ");
        sql.AppendLine(" 	, IsOwnedCompleted = :IsOwnedCompleted ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 	SeriesId = :SeriesId; ");

        using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            sql.ToString(),
            new
            {
                SeriesId = series.SeriesId,
                Title = series.Title,
                Author = series.Author,
                Publisher = series.Publisher,
                Description = series.Description,
                Memo = series.Memo,
                StartVolume = series.StartVolume,
                EndVolume = series.EndVolume,
                SeriesCompleted = series.SeriesCompleted,
                IsOwnedCompleted = series.IsOwnedCompleted,
            });
    }
}
