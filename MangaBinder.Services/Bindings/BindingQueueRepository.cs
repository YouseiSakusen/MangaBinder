using Dapper;
using MangaBinder.Settings;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Bindings;

/// <summary>
/// BindingQueue に登録済みの作品一覧を取得する Repository クラスです。
/// </summary>
public class BindingQueueRepository
{
	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="BindingQueueRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	public BindingQueueRepository(AppSettings appSettings)
	{
		this.appSettings = appSettings;
	}

	/// <summary>
	/// BindingQueue に登録済みの作品一覧を取得します。
	/// </summary>
	/// <returns>AddedAt 昇順で並んだ <see cref="BindingSeries"/> の読み取り専用リスト。</returns>
	public async ValueTask<IReadOnlyList<BindingSeries>> GetQueuedSeriesAsync()
	{
		var joinSql = new StringBuilder();
		joinSql.AppendLine(" SELECT ");
		joinSql.AppendLine(" 	  q.Id ");
		joinSql.AppendLine(" 	, q.SeriesId ");
		joinSql.AppendLine(" 	, q.Status ");
		joinSql.AppendLine(" 	, q.CurrentStep ");
		joinSql.AppendLine(" 	, q.AddedAt ");
		joinSql.AppendLine(" 	, q.UpdatedAt ");
		joinSql.AppendLine(" 	, s.SeriesId AS SplitSeriesId ");
		joinSql.AppendLine(" 	, s.SeriesId ");
		joinSql.AppendLine(" 	, s.NormalizedTitleInternal ");
		joinSql.AppendLine(" 	, s.Title ");
		joinSql.AppendLine(" 	, s.ShortTitle ");
		joinSql.AppendLine(" 	, s.ThumbnailFileName ");
		joinSql.AppendLine(" 	, s.Author ");
		joinSql.AppendLine(" 	, s.Description ");
		joinSql.AppendLine(" 	, s.SeriesCompleted ");
		joinSql.AppendLine(" 	, s.IsOwnedCompleted ");
		joinSql.AppendLine(" 	, s.StartVolume ");
		joinSql.AppendLine(" 	, s.EndVolume ");
		joinSql.AppendLine(" 	, s.BoundEndVolume ");
		joinSql.AppendLine(" 	, s.OwnedMaxVolume ");
		joinSql.AppendLine(" 	, s.NormalizedTitleExternal ");
		joinSql.AppendLine(" 	, s.UpdatedAt ");
		joinSql.AppendLine(" 	, s.ThumbnailStatus ");
		joinSql.AppendLine(" 	, s.Publisher ");
		joinSql.AppendLine(" 	, s.GoogleBooksImportStatus ");
		joinSql.AppendLine(" 	, s.GoogleBooksImportedAt ");
		joinSql.AppendLine(" 	, s.GoogleBooksImportMessage ");
		joinSql.AppendLine(" 	, s.DescriptionSource ");
		joinSql.AppendLine(" 	, s.DescriptionSourceTitle ");
		joinSql.AppendLine(" FROM ");
		joinSql.AppendLine(" 	BindingQueue q ");
		joinSql.AppendLine(" INNER JOIN MangaSeries s ON ");
		joinSql.AppendLine(" 	s.SeriesId = q.SeriesId ");
		joinSql.AppendLine(" ORDER BY ");
		joinSql.AppendLine(" 	q.AddedAt ASC; ");

		var sourcesSql = new StringBuilder();
		sourcesSql.AppendLine(" SELECT ");
		sourcesSql.AppendLine(" 	  SourceId ");
		sourcesSql.AppendLine(" 	, SeriesId ");
		sourcesSql.AppendLine(" 	, Path ");
		sourcesSql.AppendLine(" 	, Role ");
		sourcesSql.AppendLine(" FROM ");
		sourcesSql.AppendLine(" 	MangaSources ");
		sourcesSql.AppendLine(" WHERE ");
		sourcesSql.AppendLine(" 	SeriesId IN @SeriesIds ");
		sourcesSql.AppendLine(" ORDER BY ");
		sourcesSql.AppendLine(" 	  SeriesId ");
		sourcesSql.AppendLine(" 	, Role ");
		sourcesSql.AppendLine(" 	, Path; ");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		var results = (await connection.QueryAsync<BindingQueueRow, MangaSeries, BindingSeries>(
			joinSql.ToString(),
			(queue, series) => new BindingSeries
			{
				Series = series,
				Status = (BindingStartStatus)queue.Status,
				CurrentStep = queue.CurrentStep,
				AddedAt = DateTime.Parse(queue.AddedAt),
				UpdatedAt = DateTime.Parse(queue.UpdatedAt),
			},
			splitOn: "SplitSeriesId"
		)).AsList();

		if (results.Count == 0)
			return [];

		var seriesIds = results.Select(r => r.Series.SeriesId).ToArray();
		var sources = await connection.QueryAsync<MangaSource>(sourcesSql.ToString(), new { SeriesIds = seriesIds });

		var seriesDict = results.ToDictionary(r => r.Series.SeriesId, r => r.Series);
		foreach (var source in sources)
		{
			if (seriesDict.TryGetValue(source.SeriesId, out var series))
				series.Sources.Add(source);
		}

		return results;
	}

	/// <summary>
	/// BindingQueue の内容を全件保存します。
	/// </summary>
	/// <param name="items">保存対象の作品一覧。</param>
	public async ValueTask SaveAsync(IEnumerable<BindingSeries> items)
	{
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();
		using var transaction = connection.BeginTransaction();

		try
		{
			await connection.ExecuteAsync("DELETE FROM BindingQueue;", transaction: transaction);

			const string insertSql = """
				INSERT INTO BindingQueue (
					SeriesId,
					Status,
					CurrentStep,
					AddedAt,
					UpdatedAt
				) VALUES (
					:SeriesId,
					:Status,
					:CurrentStep,
					:AddedAt,
					:UpdatedAt
				);
				""";

			var insertRows = items.Select(item => new
			{
				SeriesId = item.Series.SeriesId,
				Status = (int)item.Status,
				CurrentStep = item.CurrentStep,
				AddedAt = item.AddedAt.ToString("yyyy-MM-dd HH:mm:ss"),
				UpdatedAt = item.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
			});

			await connection.ExecuteAsync(insertSql, insertRows, transaction);
			transaction.Commit();
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}

	/// <summary>
	/// Dapper multi-mapping で BindingQueue 行を受け取るための内部クラスです。
	/// </summary>
	private sealed class BindingQueueRow
	{
		/// <summary>BindingQueue の主キーです。</summary>
		public long Id { get; init; }

		/// <summary>対象作品の SeriesId です。</summary>
		public long SeriesId { get; init; }

		/// <summary>製本ステータスです。</summary>
		public int Status { get; init; }

		/// <summary>現在の製本ステップです。</summary>
		public int CurrentStep { get; init; }

		/// <summary>キューへの追加日時（SQLite TEXT 形式）です。</summary>
		public string AddedAt { get; init; } = string.Empty;

		/// <summary>最終更新日時（SQLite TEXT 形式）です。</summary>
		public string UpdatedAt { get; init; } = string.Empty;
	}
}
