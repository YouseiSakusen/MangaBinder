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
	/// BindingQueue に登録済みの SeriesId を取得します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>AddedAt 昇順で並んだ SeriesId のリスト。</returns>
	public async ValueTask<List<long>> GetQueuedSeriesIdsAsync(CancellationToken cancellationToken = default)
	{
		const string sql = """
			SELECT SeriesId
			FROM BindingQueue
			ORDER BY AddedAt ASC
			""";

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync(cancellationToken);
		var seriesIds = await connection.QueryAsync<long>(sql);

		return seriesIds.ToList();
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
