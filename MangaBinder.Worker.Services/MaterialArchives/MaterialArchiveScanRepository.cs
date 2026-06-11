using System.Data.SQLite;
using System.Text;
using Dapper;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Jobs.MaterialArchives;

/// <summary>
/// アーカイブ内部構造スキャン用のリポジトリクラスです。
/// </summary>
public class MaterialArchiveScanRepository
{
	/// <summary>DB接続文字列。</summary>
	private readonly string connectionString;

	/// <summary>ロガー。</summary>
	private readonly ILogger<MaterialArchiveScanRepository> logger;

	/// <summary>
	/// <see cref="MaterialArchiveScanRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="workerContext">Worker 実行コンテキスト。</param>
	/// <param name="logger">ロガー。</param>
	public MaterialArchiveScanRepository(WorkerContext workerContext, ILogger<MaterialArchiveScanRepository> logger)
	{
		this.connectionString = workerContext.ConnectionString;
		this.logger = logger;
	}

	/// <summary>
	/// 全 <see cref="MangaSeries"/> を <see cref="MangaSource"/> 込みで取得します。
	/// </summary>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>タイトル昇順で並んだ <see cref="MangaSeries"/> の読み取り専用リスト。</returns>
	public async ValueTask<IReadOnlyList<MangaSeries>> GetAllSeriesAsync(CancellationToken ct)
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

		using var connection = new SQLiteConnection(this.connectionString);
		await connection.OpenAsync(ct);

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
