using Dapper;
using MangaBinder.Settings;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本完了後の DB 更新を担当する Repository クラスです。
/// </summary>
public class BindingRepository
{
	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="BindingRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	public BindingRepository(AppSettings appSettings)
	{
		this.appSettings = appSettings;
	}

	/// <summary>
	/// 製本完了後の情報として BoundEndVolume のみを更新します。
	/// </summary>
	/// <remarks>
	/// 製本完了後の情報として <see cref="MangaSeries.BoundEndVolume"/> のみを更新するメソッドです。
	/// 
	/// 渡された <paramref name="series"/> の全項目を更新するのではなく、
	/// <see cref="MangaSeries.BoundEndVolume"/> のみが対象となります。
	/// 
	/// 1つの SQLite Transaction 内で以下を処理します：
	/// 1. MangaSeries.BoundEndVolume の更新（DB保存値より大きい場合のみ）
	/// 2. removeFromBindingQueue が true の場合、BindingQueue から該当行を削除
	/// 3. BindingHistory の登録と古い履歴の削除
	/// 4. BindingSettings の保存（作品単位で1件のみ。既存の場合は SettingsJson と UpdatedAt を上書き）
	/// 5. 全処理成功後に Commit
	/// 
	/// 途中で例外が発生した場合は Rollback し、例外を呼び出し元へ伝播します。
	/// </remarks>
	/// <param name="series">
	/// 更新対象の <see cref="MangaSeries"/>。
	/// <see cref="MangaSeries.SeriesId"/> と <see cref="MangaSeries.BoundEndVolume"/> が使用されます。
	/// </param>
	/// <param name="removeFromBindingQueue">
	/// true の場合、BindingQueue から該当行を削除します。
	/// false の場合、BindingQueue は変更されません。
	/// </param>
	/// <param name="history">登録する製本履歴。</param>
	/// <param name="bindingSettingsJson">BindingSettings へ保存する製本設定の JSON 文字列。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <exception cref="InvalidOperationException">
	/// <paramref name="series"/> の
	/// </exception>
	public async ValueTask<BindingHistory> UpdateAfterBindingAsync(
		MangaSeries series,
		bool removeFromBindingQueue,
		BindingHistory history,
		string bindingSettingsJson,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(history);

		if (series.SeriesId == 0)
			throw new InvalidOperationException("UpdateAfterBindingAsync は新規作品（SeriesId=0）では実行できません。");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync(cancellationToken);

		using var transaction = connection.BeginTransaction();
		BindingHistory savedHistory;
		try
		{
			// 1. MangaSeries.BoundEndVolume の更新
			// DB に保存済みの BoundEndVolume より大きい場合のみ更新する
			var updateBoundEndVolumeSql = new StringBuilder();
			updateBoundEndVolumeSql.AppendLine(" UPDATE MangaSeries ");
			updateBoundEndVolumeSql.AppendLine(" SET ");
			updateBoundEndVolumeSql.AppendLine(" 	  BoundEndVolume = :BoundEndVolume ");
			updateBoundEndVolumeSql.AppendLine(" WHERE ");
			updateBoundEndVolumeSql.AppendLine(" 	SeriesId = :SeriesId ");
			updateBoundEndVolumeSql.AppendLine(" 	AND BoundEndVolume < :BoundEndVolume; ");

			await connection.ExecuteAsync(
				updateBoundEndVolumeSql.ToString(),
				new
				{
					SeriesId = series.SeriesId,
					BoundEndVolume = series.BoundEndVolume,
				},
				transaction);

			// 2. BindingQueue の削除
			if (removeFromBindingQueue)
			{
				var deleteBindingQueueSql = new StringBuilder();
				deleteBindingQueueSql.AppendLine(" DELETE FROM BindingQueue ");
				deleteBindingQueueSql.AppendLine(" WHERE ");
				deleteBindingQueueSql.AppendLine(" 	SeriesId = :SeriesId; ");

				await connection.ExecuteAsync(
					deleteBindingQueueSql.ToString(),
					new { SeriesId = series.SeriesId },
					transaction);
			}

			// 3. BindingHistory の INSERT（BoundAt は DB の DEFAULT 値を使用）
			var insertHistorySql = new StringBuilder();
			insertHistorySql.AppendLine(" INSERT INTO BindingHistory ( ");
			insertHistorySql.AppendLine(" 	  SeriesId ");
			insertHistorySql.AppendLine(" 	, VolumeRange ");
			insertHistorySql.AppendLine(" 	, OutputFileName ");
			insertHistorySql.AppendLine(" 	, OutputFolderPath ");
			insertHistorySql.AppendLine(" ) VALUES ( ");
			insertHistorySql.AppendLine(" 	  :SeriesId ");
			insertHistorySql.AppendLine(" 	, :VolumeRange ");
			insertHistorySql.AppendLine(" 	, :OutputFileName ");
			insertHistorySql.AppendLine(" 	, :OutputFolderPath ");
			insertHistorySql.AppendLine(" ); ");

			await connection.ExecuteAsync(
				insertHistorySql.ToString(),
				new
				{
					SeriesId = series.SeriesId,
					history.VolumeRange,
					history.OutputFileName,
					history.OutputFolderPath,
				},
				transaction);

			var selectHistorySql = new StringBuilder();
			selectHistorySql.AppendLine(" SELECT ");
			selectHistorySql.AppendLine(" 	  HistoryId ");
			selectHistorySql.AppendLine(" 	, SeriesId ");
			selectHistorySql.AppendLine(" 	, BoundAt ");
			selectHistorySql.AppendLine(" 	, VolumeRange ");
			selectHistorySql.AppendLine(" 	, OutputFileName ");
			selectHistorySql.AppendLine(" 	, OutputFolderPath ");
			selectHistorySql.AppendLine(" FROM ");
			selectHistorySql.AppendLine(" 	BindingHistory ");
			selectHistorySql.AppendLine(" WHERE ");
			selectHistorySql.AppendLine(" 	HistoryId = last_insert_rowid(); ");

			savedHistory = await connection.QuerySingleAsync<BindingHistory>(
				selectHistorySql.ToString(),
				transaction: transaction);

			// 4. 最新3件以外の履歴を削除
			var deleteOldHistorySql = new StringBuilder();
			deleteOldHistorySql.AppendLine(" DELETE FROM BindingHistory ");
			deleteOldHistorySql.AppendLine(" WHERE ");
			deleteOldHistorySql.AppendLine(" 		SeriesId = :SeriesId ");
			deleteOldHistorySql.AppendLine(" 	AND HistoryId NOT IN ( ");
			deleteOldHistorySql.AppendLine(" 		SELECT HistoryId FROM BindingHistory ");
			deleteOldHistorySql.AppendLine(" 		WHERE SeriesId = :SeriesId ");
			deleteOldHistorySql.AppendLine(" 		ORDER BY HistoryId DESC ");
			deleteOldHistorySql.AppendLine(" 		LIMIT 3 ");
			deleteOldHistorySql.AppendLine(" 	); ");

			await connection.ExecuteAsync(
				deleteOldHistorySql.ToString(),
				new { SeriesId = series.SeriesId },
				transaction);

			// 5. BindingSettings の UPSERT（作品単位で最新の1件のみ保持し、既存の場合は JSON 全体を上書きする）
			var upsertBindingSettingsSql = new StringBuilder();
			upsertBindingSettingsSql.AppendLine(" INSERT INTO BindingSettings ( ");
			upsertBindingSettingsSql.AppendLine(" 	  SeriesId ");
			upsertBindingSettingsSql.AppendLine(" 	, SettingsJson ");
			upsertBindingSettingsSql.AppendLine(" 	, UpdatedAt ");
			upsertBindingSettingsSql.AppendLine(" ) VALUES ( ");
			upsertBindingSettingsSql.AppendLine(" 	  :SeriesId ");
			upsertBindingSettingsSql.AppendLine(" 	, :SettingsJson ");
			upsertBindingSettingsSql.AppendLine(" 	, DATETIME('now', 'localtime') ");
			upsertBindingSettingsSql.AppendLine(" ) ");
			upsertBindingSettingsSql.AppendLine(" ON CONFLICT(SeriesId) DO UPDATE SET ");
			upsertBindingSettingsSql.AppendLine(" 	  SettingsJson = excluded.SettingsJson ");
			upsertBindingSettingsSql.AppendLine(" 	, UpdatedAt = DATETIME('now', 'localtime'); ");

			await connection.ExecuteAsync(
				upsertBindingSettingsSql.ToString(),
				new
				{
					SeriesId = series.SeriesId,
					SettingsJson = bindingSettingsJson,
				},
				transaction);
		}
		catch
		{
			// Commit 前の例外のみ Rollback する
			transaction.Rollback();
			throw;
		}

		// 6. 全処理成功後に Commit（
		transaction.Commit();

		return savedHistory;
	}
}
