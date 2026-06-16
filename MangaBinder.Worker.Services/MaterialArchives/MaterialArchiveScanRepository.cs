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
	/// 全 <see cref="MangaSeries"/> を Material Role の <see cref="MangaSource"/> 込みで取得します。
	/// Dapper Multi Mapping を使用して JOIN 結果から集約します。
	/// </summary>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>タイトル昇順で並んだ <see cref="MangaSeries"/> の読み取り専用リスト。</returns>
	public async ValueTask<IReadOnlyList<MangaSeries>> GetAllSeriesAsync(CancellationToken ct)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" SELECT ");
		sql.AppendLine(" 	  s.SeriesId ");
		sql.AppendLine(" 	, s.NormalizedTitleInternal ");
		sql.AppendLine(" 	, s.Title ");
		sql.AppendLine(" 	, s.HasNestedArchive ");
		sql.AppendLine(" 	, ms.SourceId ");
		sql.AppendLine(" 	, ms.SeriesId ");
		sql.AppendLine(" 	, ms.Path ");
		sql.AppendLine(" 	, ms.Role ");
		sql.AppendLine(" FROM ");
		sql.AppendLine(" 	MangaSeries s ");
		sql.AppendLine(" INNER JOIN MangaSources ms ");
		sql.AppendLine(" 	ON ms.SeriesId = s.SeriesId ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	ms.Role = :MaterialRole ");
		sql.AppendLine(" ORDER BY ");
		sql.AppendLine(" 	s.NormalizedTitleInternal ");

		using var connection = new SQLiteConnection(this.connectionString);
		await connection.OpenAsync(ct);

		var results = await connection.QueryAsync<MangaSeries, MangaSource, MangaSeries>(
			sql.ToString(),
			(series, source) =>
			{
				series.Sources.Add(source);
				return series;
			},
			new { MaterialRole = (int)FolderRole.Material },
			splitOn: "SourceId");

		// JOIN結果は MangaSeries が重複して返るため、Dictionary で集約
		var seriesDict = new Dictionary<long, MangaSeries>();
		foreach (var series in results)
		{
			if (seriesDict.TryGetValue(series.SeriesId, out var existing))
			{
				// 既に同じ SeriesId が存在する場合、Sources を追加
				foreach (var source in series.Sources)
				{
					existing.Sources.Add(source);
				}
			}
			else
			{
				seriesDict[series.SeriesId] = series;
			}
		}

		return seriesDict.Values.ToList();
	}
}
