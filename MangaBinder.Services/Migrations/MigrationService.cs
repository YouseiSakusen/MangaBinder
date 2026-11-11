using Dapper;
using System.Data.SQLite;

namespace MangaBinder.Migrations;

/// <summary>
/// マイグレーションの要否判定と実行を担当するサービスです。
/// Generic Host 生成前に使用するため、DI には登録しません。
/// </summary>
public class MigrationService
{
	private readonly string connectionString;
	private readonly MigrationFactory factory;

	/// <summary>
	/// 直近の ExecuteAsync でバックアップに成功した場合のバックアップファイルのフルパス。未実施・失敗時は null。
	/// </summary>
	public string? BackupFilePath { get; private set; }

	/// <summary>
	/// <see cref="MigrationService"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="connectionString">SQLite 接続文字列。</param>
	public MigrationService(string connectionString)
	{
		this.connectionString = connectionString;
		this.factory = new MigrationFactory(connectionString);
	}

	/// <summary>
	/// マイグレーションが必要かどうかを判定します。
	/// </summary>
	/// <returns>必要な場合は true。更新不要の場合は false。</returns>
	/// <exception cref="InvalidOperationException">対応できないバージョンの場合。</exception>
	public async Task<bool> IsMigrationRequiredAsync()
	{
		var (databaseVersion, titleNormalizationVersion) = await this.loadVersionsAsync();
		return this.factory.Create(databaseVersion, titleNormalizationVersion).Count > 0;
	}

	/// <summary>
	/// 必要なマイグレーションを順番に実行し、要求バージョンへ到達したことを確認します。
	/// </summary>
	public async Task ExecuteAsync()
	{
		this.BackupFilePath = null;

		var (databaseVersion, titleNormalizationVersion) = await this.loadVersionsAsync();
		var migrations = this.factory.Create(databaseVersion, titleNormalizationVersion);

		if (migrations.Count > 0)
		{
			// 実行対象がある場合のみ、Migration 実行前に1回だけバックアップする
			this.BackupFilePath = await this.backupDatabaseAsync();

			foreach (var migration in migrations)
				await migration.ExecuteAsync();
		}

		(databaseVersion, titleNormalizationVersion) = await this.loadVersionsAsync();
		if (databaseVersion != Constants.CurrentDatabaseVersion || titleNormalizationVersion != Constants.CurrentTitleNormalizationVersion)
			throw new InvalidOperationException(
				$"マイグレーション後も要求バージョンに到達していません。DB構造：{databaseVersion}（要求：{Constants.CurrentDatabaseVersion}）　タイトル正規化：{titleNormalizationVersion}（要求：{Constants.CurrentTitleNormalizationVersion}）");
	}

	/// <summary>
	/// BackupDatabase で、
	/// WAL モードのため File.Copy は使用しません。SQLite 接続は Task.Run 内で生成・破棄します。
	/// </summary>
	/// <returns>バックアップファイルのフルパス。</returns>
	private async Task<string> backupDatabaseAsync()
	{
		var directory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			"MangaBinder",
			"Backups");
		Directory.CreateDirectory(directory);

		var path = Path.Combine(directory, $"manga_{DateTime.Now:yyyyMMdd_HHmmss}.db");
		var sourceConnectionString = this.connectionString;

		await Task.Run(() =>
		{
			using var source = new SQLiteConnection(sourceConnectionString);
			using var destination = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = path }.ConnectionString);
			source.Open();
			destination.Open();
			source.BackupDatabase(destination, "main", "main", -1, null, 0);
		});

		return path;
	}

	/// <summary>
	/// DatabaseVersion テーブル（Id = 1）から2種類のバージョンを読み込みます。
	/// </summary>
	private async Task<(string DatabaseVersion, string TitleNormalizationVersion)> loadVersionsAsync()
	{
		using var connection = new SQLiteConnection(this.connectionString);
		await connection.OpenAsync();

		var row = await connection.QuerySingleAsync<VersionRow>(
			"SELECT Version, TitleNormalizationVersion FROM DatabaseVersion WHERE Id = 1;");
		return (row.Version, row.TitleNormalizationVersion);
	}

	/// <summary>DatabaseVersion テーブルの1行を受け取る内部クラスです。</summary>
	private sealed class VersionRow
	{
		public string Version { get; set; } = string.Empty;
		public string TitleNormalizationVersion { get; set; } = string.Empty;
	}
}
