using System.Data.SQLite;
using System.Text;
using Dapper;
using Xunit;
using MangaBinder.Helpers;

namespace MangaBinder.Sandboxes;

/// <summary>
/// MangaSeries.NormalizedTitleInternal を現在の MangaTitleHelper.NormalizeTitleInternal() の仕様で
/// 一括再生成するための Sandbox テストです。
/// 
/// このテストは恒久的な単体テストではなく、仕様変更に伴う既存DB派生値の再生成処理です。
/// ユーザーがVisual Studioのテストエクスプローラーから手動で実行してください。
/// 
/// 処理内容:
/// 1. SQLite DB を開く
/// 2. トランザクション開始
/// 3. MangaSeries から全件取得
/// 4. 各 Title を MangaTitleHelper.NormalizeTitleInternal() で再生成
/// 5. 既存値と異なる行のみ NormalizedTitleInternal を UPDATE
/// 6. 同じ Connection / Transaction で MangaSeries を再取得
/// 7. 全件について期待値と一致することを検証
/// 8. 全件正常なら COMMIT
/// 9. 不一致またはその他の例外が発生した場合は ROLLBACK
/// </summary>
public class NormalizedTitleInternalMigrationSandboxTest
{
	/// <summary>
	/// ローカル開発環境の実DBパスです。
	/// 本番環境では別途設定ファイルまたはユーザー指示に従って更新してください。
	/// </summary>
	private const string DbPath = @"D:\GitBares\MangaBinder\MangaBinder\bin\Debug\net10.0-windows\db\manga.db";

	/// <summary>
	/// MangaSeries.NormalizedTitleInternal を現在の仕様で一括再生成します。
	/// 実行前にDB全体のバックアップを取得することを強く推奨します。
	/// </summary>
	[Fact]
	public void MigrateNormalizedTitleInternal_RegeneratesAllNormalizedValues()
	{
		// 対象DBが存在することを確認
		if (!File.Exists(DbPath))
		{
			throw new FileNotFoundException($"Database file not found: {DbPath}");
		}

		var connectionString = $"Data Source={DbPath};Version=3;";
		var totalCount = 0;
		var changedCount = 0;
		var verificationFailures = new List<(long SeriesId, string Title, string Expected, string Actual)>();

		using var connection = new SQLiteConnection(connectionString);
		connection.Open();

		using var transaction = connection.BeginTransaction();

		try
		{
			// 1. MangaSeries から必要な情報を全件取得
			var selectSql = new StringBuilder();
			selectSql.AppendLine(" SELECT ");
			selectSql.AppendLine(" 	  SeriesId ");
			selectSql.AppendLine(" 	, Title ");
			selectSql.AppendLine(" 	, NormalizedTitleInternal ");
			selectSql.AppendLine(" FROM ");
			selectSql.AppendLine(" 	MangaSeries ");
			selectSql.AppendLine(" ORDER BY ");
			selectSql.AppendLine(" 	SeriesId; ");

			var records = connection.Query<(long SeriesId, string Title, string NormalizedTitleInternal)>(
				selectSql.ToString(),
				transaction: transaction).ToList();

			totalCount = records.Count;

			// 2. 各行について新値を生成し、異なる場合のみ UPDATE
			foreach (var record in records)
			{
				var normalizedTitle = MangaTitleHelper.NormalizeTitleInternal(record.Title);

				if (normalizedTitle != record.NormalizedTitleInternal)
				{
					// UPDATE 実行
					var updateSql = new StringBuilder();
					updateSql.AppendLine(" UPDATE MangaSeries ");
					updateSql.AppendLine(" SET ");
					updateSql.AppendLine(" 	NormalizedTitleInternal = :NormalizedTitleInternal ");
					updateSql.AppendLine(" WHERE ");
					updateSql.AppendLine(" 	SeriesId = :SeriesId; ");

					connection.Execute(
						updateSql.ToString(),
						new { NormalizedTitleInternal = normalizedTitle, SeriesId = record.SeriesId },
						transaction: transaction);

					changedCount++;
				}
			}

			// 3. COMMIT 前に検証：同じ Connection / Transaction で MangaSeries を再取得
			var verifySql = new StringBuilder();
			verifySql.AppendLine(" SELECT ");
			verifySql.AppendLine(" 	  SeriesId ");
			verifySql.AppendLine(" 	, Title ");
			verifySql.AppendLine(" 	, NormalizedTitleInternal ");
			verifySql.AppendLine(" FROM ");
			verifySql.AppendLine(" 	MangaSeries ");
			verifySql.AppendLine(" ORDER BY ");
			verifySql.AppendLine(" 	SeriesId; ");

			var verifyRecords = connection.Query<(long SeriesId, string Title, string NormalizedTitleInternal)>(
				verifySql.ToString(),
				transaction: transaction).ToList();

			// 4. 全件について期待値と一致することを検証
			foreach (var record in verifyRecords)
			{
				var expectedNormalized = MangaTitleHelper.NormalizeTitleInternal(record.Title);

				if (expectedNormalized != record.NormalizedTitleInternal)
				{
					verificationFailures.Add((record.SeriesId, record.Title, expectedNormalized, record.NormalizedTitleInternal));
				}
			}

			// 5. 検証が全件成功した場合のみ COMMIT
			if (verificationFailures.Count > 0)
			{
				var failureDetails = string.Join(
					Environment.NewLine,
					verificationFailures.Select(f =>
						$"  SeriesId={f.SeriesId}, Title='{f.Title}', Expected='{f.Expected}', Actual='{f.Actual}'"));

				throw new InvalidOperationException(
					$"Verification failed: {verificationFailures.Count} record(s) have mismatched NormalizedTitleInternal.{Environment.NewLine}{failureDetails}");
			}

			transaction.Commit();
		}
		catch
		{
			// エラーが発生した場合は ROLLBACK
			transaction.Rollback();
			throw;
		}

		// 6. 処理結果をアサーション（正常完了を記録）
		Assert.True(
			totalCount > 0,
			"No records found in MangaSeries table.");

		Assert.True(
			verificationFailures.Count == 0,
			$"Verification failed: {verificationFailures.Count} record(s) do not match expected normalized values.");

		// テスト実行の出力ログに結果を記録
		System.Diagnostics.Debug.WriteLine($"Migration completed successfully:");
		System.Diagnostics.Debug.WriteLine($"  Total MangaSeries records: {totalCount}");
		System.Diagnostics.Debug.WriteLine($"  Records with changed NormalizedTitleInternal: {changedCount}");
		System.Diagnostics.Debug.WriteLine($"  Verification: All {totalCount} record(s) passed.");
	}
}
