using Dapper;
using MangaBinder.Helpers;
using System.Data.SQLite;

namespace MangaBinder.Migrations;

/// <summary>
/// タイトル正規化バージョン 0→1 のマイグレーションです。
/// 正式リリース前のマイグレーション基盤の動作確認用で、正式リリース時に削除します。
/// </summary>
public class Migration0_1 : IMigration
{
	private readonly string connectionString;

	/// <summary>
	/// <see cref="Migration0_1"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="connectionString">SQLite 接続文字列。</param>
	public Migration0_1(string connectionString)
		=> this.connectionString = connectionString;

	/// <summary>
	/// 全作品の NormalizedTitleInternal を再生成し、TitleNormalizationVersion を "1" へ更新します。
	/// すべて同一トランザクションで実行し、例外時はロールバックして伝播します。
	/// </summary>
	public async Task ExecuteAsync()
	{
		using var connection = new SQLiteConnection(this.connectionString);
		await connection.OpenAsync();
		using var transaction = connection.BeginTransaction();

		var rows = (await connection.QueryAsync<(long SeriesId, string Title, string NormalizedTitleInternal)>(
			"SELECT SeriesId, Title, NormalizedTitleInternal FROM MangaSeries;",
			transaction: transaction)).ToList();

		foreach (var row in rows)
		{
			var normalized = MangaTitleHelper.NormalizeTitleInternal(row.Title);
			if (normalized == row.NormalizedTitleInternal)
				continue;

			await connection.ExecuteAsync(
				"UPDATE MangaSeries SET NormalizedTitleInternal = :Normalized WHERE SeriesId = :SeriesId;",
				new { Normalized = normalized, row.SeriesId },
				transaction);
		}

		await connection.ExecuteAsync(
			"UPDATE DatabaseVersion SET TitleNormalizationVersion = :Version WHERE Id = 1;",
			new { Version = "1" },
			transaction);

		transaction.Commit();
	}
}
