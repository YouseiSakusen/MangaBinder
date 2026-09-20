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
	/// 3. 全処理成功後に Commit
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
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <exception cref="InvalidOperationException">
	/// <paramref name="series"/> の <see cref="MangaSeries.SeriesId"/> が 0 の場合にスローされます。
	/// </exception>
	public async ValueTask UpdateAfterBindingAsync(
		MangaSeries series,
		bool removeFromBindingQueue,
		CancellationToken cancellationToken = default)
	{
		if (series.SeriesId == 0)
			throw new InvalidOperationException("UpdateAfterBindingAsync は新規作品（SeriesId=0）では実行できません。");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync(cancellationToken);

		using var transaction = connection.BeginTransaction();
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
				const string deleteBindingQueueSql = """
					DELETE FROM BindingQueue
					WHERE SeriesId = :SeriesId;
					""";

				await connection.ExecuteAsync(
					deleteBindingQueueSql,
					new { SeriesId = series.SeriesId },
					transaction);
			}

			// 3. 全処理成功後に Commit
			transaction.Commit();
		}
		catch
		{
			transaction.Rollback();
			throw;
		}
	}
}
