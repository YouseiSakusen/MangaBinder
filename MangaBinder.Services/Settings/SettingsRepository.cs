using Dapper;
using MangaBinder.Jobs;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Settings;

/// <summary>
/// 設定関連テーブル（AppSettings / SourceFolders / AppStatus / DatabaseVersion）の DB アクセスと Transaction 管理を担うリポジトリです。
/// UI・Worker 共通の読み込みは <see cref="SharedSettingsRepository"/> を使用します。
/// </summary>
public class SettingsRepository
{
	/// <summary>アプリケーション設定インスタンス。</summary>
	private readonly AppSettings appSettings;

	/// <summary>ジョブリポジトリ。</summary>
	private readonly JobRepository jobRepository;

	/// <summary>
	/// <see cref="SettingsRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定インスタンス。</param>
	/// <param name="jobRepository">ジョブリポジトリ。</param>
	public SettingsRepository(AppSettings appSettings, JobRepository jobRepository)
	{
		this.appSettings = appSettings;
		this.jobRepository = jobRepository;
	}

	/// <summary>
	/// AppSettings・AppStatus・DatabaseVersion を読み込み、<see cref="AppSettings"/> へ反映します。
	/// </summary>
	public async ValueTask LoadAsync()
	{
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		await this.loadAppSettingsAsync(connection);
		await this.loadApplicationStatusAsync(connection);
		await this.loadDatabaseVersionAsync(connection);
	}

	/// <summary>
	/// AppSettings テーブルの設定値のみを同一 Transaction で保存します。
	/// </summary>
	public async ValueTask SaveAppSettingsAsync()
	{
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		using var transaction = connection.BeginTransaction();
		await this.updateAppSettingsAsync(connection, transaction);
		transaction.Commit();
	}

	/// <summary>
	/// AppSettings の設定値、SourceFolders の全件洗い替え、Job 登録、初回設定完了の AppStatus 更新を同一 Transaction で保存します。
	/// </summary>
	/// <param name="jobTypes">登録する Job 種別。</param>
	/// <param name="completeInitialSetup">AppStatus の初回設定完了フラグを更新する場合は true。</param>
	public async ValueTask SaveSettingsAsync(IReadOnlyList<JobType> jobTypes, bool completeInitialSetup)
	{
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		using var transaction = connection.BeginTransaction();
		await this.updateAppSettingsAsync(connection, transaction);
		await this.replaceSourceFoldersAsync(connection, transaction);

		foreach (var jobType in jobTypes)
			await this.jobRepository.EnqueueAsync(jobType, connection, transaction, false);

		if (completeInitialSetup)
			await this.updateInitialFolderSetupCompletedAsync(connection, transaction);

		transaction.Commit();
	}

	/// <summary>
	/// AppSettings テーブルの設定値を更新します。
	/// </summary>
	private async ValueTask updateAppSettingsAsync(SQLiteConnection connection, SQLiteTransaction transaction)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" UPDATE AppSettings ");
		sql.AppendLine(" SET ");
		sql.AppendLine(" 	  WorkFolderPath      = :WorkFolderPath ");
		sql.AppendLine(" 	, ThumbnailFolderPath = :ThumbnailFolderPath ");
		sql.AppendLine(" 	, ThumbnailWidth      = :ThumbnailWidth ");
		sql.AppendLine(" 	, ThumbnailHeight     = :ThumbnailHeight ");
		sql.AppendLine(" 	, ThumbnailJpegQuality = :ThumbnailJpegQuality ");
		sql.AppendLine(" 	, ThumbnailBackgroundColor = :ThumbnailBackgroundColor ");
		sql.AppendLine(" 	, BindingDefaultImageExtension   = :BindingDefaultImageExtension ");
		sql.AppendLine(" 	, BindingDefaultArchiveExtension = :BindingDefaultArchiveExtension ");
		sql.AppendLine(" 	, BindingConvertImagesToDefaultFormat = :BindingConvertImagesToDefaultFormat ");
		sql.AppendLine(" 	, WorkVolumeFolderNamePrefix    = :WorkVolumeFolderNamePrefix ");
		sql.AppendLine(" 	, WorkVolumeFolderNameSuffix    = :WorkVolumeFolderNameSuffix ");
		sql.AppendLine(" 	, SeriesListVerticalOffset      = :SeriesListVerticalOffset ");
		sql.AppendLine(" 	, BindingZipAuthorLeftBracket   = :BindingZipAuthorLeftBracket ");
		sql.AppendLine(" 	, BindingZipAuthorRightBracket  = :BindingZipAuthorRightBracket ");
		sql.AppendLine(" 	, BindingZipNameSeparator       = :BindingZipNameSeparator ");
		sql.AppendLine(" 	, BindingZipNormalVolumePrefix  = :BindingZipNormalVolumePrefix ");
		sql.AppendLine(" 	, BindingZipNormalVolumeSeparator = :BindingZipNormalVolumeSeparator ");
		sql.AppendLine(" 	, BindingZipNormalVolumeSuffix  = :BindingZipNormalVolumeSuffix ");
		sql.AppendLine(" 	, BindingZipCompleteVolumePrefix = :BindingZipCompleteVolumePrefix ");
		sql.AppendLine(" 	, BindingZipCompleteVolumeSuffix = :BindingZipCompleteVolumeSuffix ");
		sql.AppendLine(" 	, BindingZipPartialCompleteVolumePrefix    = :BindingZipPartialCompleteVolumePrefix ");
		sql.AppendLine(" 	, BindingZipPartialCompleteVolumeSeparator = :BindingZipPartialCompleteVolumeSeparator ");
		sql.AppendLine(" 	, BindingZipPartialCompleteVolumeSuffix    = :BindingZipPartialCompleteVolumeSuffix ");
		sql.AppendLine(" 	, BindingZipCompleteVolumeZeroPadding      = :BindingZipCompleteVolumeZeroPadding; ");

		var param = new
		{
			WorkFolderPath = this.appSettings.WorkFolderPath.Value,
			ThumbnailFolderPath = this.appSettings.ThumbnailFolderPath.Value,
			ThumbnailWidth = this.appSettings.ThumbnailWidth.Value,
			ThumbnailHeight = this.appSettings.ThumbnailHeight.Value,
			ThumbnailJpegQuality = this.appSettings.ThumbnailJpegQuality.Value,
			ThumbnailBackgroundColor = this.appSettings.ThumbnailBackgroundColor.Value,
			BindingDefaultImageExtension = this.appSettings.BindingDefaultImageExtension.Value,
			BindingDefaultArchiveExtension = this.appSettings.BindingDefaultArchiveExtension.Value,
			BindingConvertImagesToDefaultFormat = this.appSettings.BindingConvertImagesToDefaultFormat.Value ? 1 : 0,
			WorkVolumeFolderNamePrefix = this.appSettings.WorkVolumeFolderNamePrefix.Value,
			WorkVolumeFolderNameSuffix = this.appSettings.WorkVolumeFolderNameSuffix.Value,
			SeriesListVerticalOffset = this.appSettings.SeriesListVerticalOffset.Value,
			BindingZipAuthorLeftBracket = this.appSettings.BindingZipAuthorLeftBracket.Value,
			BindingZipAuthorRightBracket = this.appSettings.BindingZipAuthorRightBracket.Value,
			BindingZipNameSeparator = this.appSettings.BindingZipNameSeparator.Value,
			BindingZipNormalVolumePrefix = this.appSettings.BindingZipNormalVolumePrefix.Value,
			BindingZipNormalVolumeSeparator = this.appSettings.BindingZipNormalVolumeSeparator.Value,
			BindingZipNormalVolumeSuffix = this.appSettings.BindingZipNormalVolumeSuffix.Value,
			BindingZipCompleteVolumePrefix = this.appSettings.BindingZipCompleteVolumePrefix.Value,
			BindingZipCompleteVolumeSuffix = this.appSettings.BindingZipCompleteVolumeSuffix.Value,
			BindingZipPartialCompleteVolumePrefix = this.appSettings.BindingZipPartialCompleteVolumePrefix.Value,
			BindingZipPartialCompleteVolumeSeparator = this.appSettings.BindingZipPartialCompleteVolumeSeparator.Value,
			BindingZipPartialCompleteVolumeSuffix = this.appSettings.BindingZipPartialCompleteVolumeSuffix.Value,
			BindingZipCompleteVolumeZeroPadding = this.appSettings.BindingZipCompleteVolumeZeroPadding.Value ? 1 : 0,
		};
		await connection.ExecuteAsync(sql.ToString(), param, transaction);
	}

	/// <summary>
	/// SourceFolders テーブルを DELETE → INSERT で全件洗い替えします。
	/// </summary>
	private async ValueTask replaceSourceFoldersAsync(SQLiteConnection connection, SQLiteTransaction transaction)
	{
		var deleteSql = new StringBuilder();
		deleteSql.AppendLine(" DELETE FROM SourceFolders; ");
		await connection.ExecuteAsync(deleteSql.ToString(), transaction: transaction);

		var insertSql = new StringBuilder();
		insertSql.AppendLine(" INSERT INTO SourceFolders ( ");
		insertSql.AppendLine(" 	  FolderPath ");
		insertSql.AppendLine(" 	, DisplayName ");
		insertSql.AppendLine(" 	, Role ");
		insertSql.AppendLine(" ) VALUES ( ");
		insertSql.AppendLine(" 	  :FolderPath ");
		insertSql.AppendLine(" 	, :DisplayName ");
		insertSql.AppendLine(" 	, :Role ");
		insertSql.AppendLine(" ); ");

		var rows = this.appSettings.SourceFolders
			.Select(f => new
			{
				FolderPath = f.FolderPath.Value,
				DisplayName = f.DisplayName.Value,
				Role = (int)f.Role.Value,
			})
			.ToList();
		await connection.ExecuteAsync(insertSql.ToString(), rows, transaction);
	}

	/// <summary>
	/// AppStatus テーブル（Id = 1）の初回設定完了フラグを完了へ更新します。
	/// </summary>
	private async ValueTask updateInitialFolderSetupCompletedAsync(SQLiteConnection connection, SQLiteTransaction transaction)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" UPDATE AppStatus ");
		sql.AppendLine(" SET ");
		sql.AppendLine(" 	  IsInitialFolderSetupCompleted = :IsInitialFolderSetupCompleted ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	Id = :Id; ");

		await connection.ExecuteAsync(sql.ToString(), new { Id = 1, IsInitialFolderSetupCompleted = 1 }, transaction);
	}

	/// <summary>
	/// AppStatus テーブル（Id = 1）から状態を読み込みます。レコードが無い場合は例外とします。
	/// </summary>
	private async ValueTask loadApplicationStatusAsync(SQLiteConnection connection)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" SELECT ");
		sql.AppendLine(" 	  IsInitialFolderSetupCompleted ");
		sql.AppendLine(" FROM ");
		sql.AppendLine(" 	AppStatus ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	Id = :Id; ");

		var value = await connection.QuerySingleAsync<long>(sql.ToString(), new { Id = 1 });
		this.appSettings.AppStatus.IsInitialFolderSetupCompleted.Value = value != 0;
	}

	/// <summary>
	/// DatabaseVersion テーブル（Id = 1）からバージョンを読み込みます。
	/// </summary>
	private async ValueTask loadDatabaseVersionAsync(SQLiteConnection connection)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" SELECT ");
		sql.AppendLine(" 	  Version ");
		sql.AppendLine(" FROM ");
		sql.AppendLine(" 	DatabaseVersion ");
		sql.AppendLine(" WHERE ");
		sql.AppendLine(" 	Id = :Id; ");

		this.appSettings.DatabaseVersion = await connection.QuerySingleAsync<string>(sql.ToString(), new { Id = 1 });
	}

	/// <summary>
	/// AppSettings テーブルから設定値を読み込みます。
	/// </summary>
	private async ValueTask loadAppSettingsAsync(SQLiteConnection connection)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" SELECT ");
		sql.AppendLine(" 	  WorkFolderPath ");
		sql.AppendLine(" 	, ThumbnailFolderPath ");
		sql.AppendLine(" 	, ThumbnailWidth ");
		sql.AppendLine(" 	, ThumbnailHeight ");
		sql.AppendLine(" 	, ThumbnailJpegQuality ");
		sql.AppendLine(" 	, ThumbnailBackgroundColor ");
		sql.AppendLine(" 	, BindingDefaultImageExtension ");
		sql.AppendLine(" 	, BindingDefaultArchiveExtension ");
		sql.AppendLine(" 	, BindingConvertImagesToDefaultFormat ");
		sql.AppendLine(" 	, WorkVolumeFolderNamePrefix ");
		sql.AppendLine(" 	, WorkVolumeFolderNameSuffix ");
		sql.AppendLine(" 	, SeriesListVerticalOffset ");
		sql.AppendLine(" 	, BindingZipAuthorLeftBracket ");
		sql.AppendLine(" 	, BindingZipAuthorRightBracket ");
		sql.AppendLine(" 	, BindingZipNameSeparator ");
		sql.AppendLine(" 	, BindingZipNormalVolumePrefix ");
		sql.AppendLine(" 	, BindingZipNormalVolumeSeparator ");
		sql.AppendLine(" 	, BindingZipNormalVolumeSuffix ");
		sql.AppendLine(" 	, BindingZipCompleteVolumePrefix ");
		sql.AppendLine(" 	, BindingZipCompleteVolumeSuffix ");
		sql.AppendLine(" 	, BindingZipPartialCompleteVolumePrefix ");
		sql.AppendLine(" 	, BindingZipPartialCompleteVolumeSeparator ");
		sql.AppendLine(" 	, BindingZipPartialCompleteVolumeSuffix ");
		sql.AppendLine(" 	, BindingZipCompleteVolumeZeroPadding ");
		sql.AppendLine(" FROM ");
		sql.AppendLine(" 	AppSettings; ");

		using var command = new SQLiteCommand(sql.ToString(), connection);
		using var reader = await command.ExecuteReaderAsync();

		if (await reader.ReadAsync())
		{
			this.appSettings.WorkFolderPath.Value = reader.GetString(0);
			this.appSettings.ThumbnailFolderPath.Value = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
			this.appSettings.ThumbnailWidth.Value = reader.IsDBNull(2) ? 160 : reader.GetInt32(2);
			this.appSettings.ThumbnailHeight.Value = reader.IsDBNull(3) ? 224 : reader.GetInt32(3);
			this.appSettings.ThumbnailJpegQuality.Value = reader.IsDBNull(4) ? 80 : reader.GetInt32(4);
			this.appSettings.ThumbnailBackgroundColor.Value = reader.IsDBNull(5) ? "#FEFEFE" : reader.GetString(5);
			this.appSettings.BindingDefaultImageExtension.Value = reader.IsDBNull(6) ? ".jpg" : reader.GetString(6);
			this.appSettings.BindingDefaultArchiveExtension.Value = reader.IsDBNull(7) ? ".zip" : reader.GetString(7);
			this.appSettings.BindingConvertImagesToDefaultFormat.Value = !reader.IsDBNull(8) && reader.GetInt32(8) != 0;
			this.appSettings.WorkVolumeFolderNamePrefix.Value = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
			this.appSettings.WorkVolumeFolderNameSuffix.Value = reader.IsDBNull(10) ? "巻" : reader.GetString(10);
			this.appSettings.SeriesListVerticalOffset.Value = reader.IsDBNull(11) ? 0.0 : reader.GetDouble(11);
			this.appSettings.BindingZipAuthorLeftBracket.Value = reader.IsDBNull(12) ? "[" : reader.GetString(12);
			this.appSettings.BindingZipAuthorRightBracket.Value = reader.IsDBNull(13) ? "]" : reader.GetString(13);
			this.appSettings.BindingZipNameSeparator.Value = reader.IsDBNull(14) ? " " : reader.GetString(14);
			this.appSettings.BindingZipNormalVolumePrefix.Value = reader.IsDBNull(15) ? "第" : reader.GetString(15);
			this.appSettings.BindingZipNormalVolumeSeparator.Value = reader.IsDBNull(16) ? "-" : reader.GetString(16);
			this.appSettings.BindingZipNormalVolumeSuffix.Value = reader.IsDBNull(17) ? "巻" : reader.GetString(17);
			this.appSettings.BindingZipCompleteVolumePrefix.Value = reader.IsDBNull(18) ? "全" : reader.GetString(18);
			this.appSettings.BindingZipCompleteVolumeSuffix.Value = reader.IsDBNull(19) ? "巻" : reader.GetString(19);
			this.appSettings.BindingZipPartialCompleteVolumePrefix.Value = reader.IsDBNull(20) ? "第" : reader.GetString(20);
			this.appSettings.BindingZipPartialCompleteVolumeSeparator.Value = reader.IsDBNull(21) ? "-全" : reader.GetString(21);
			this.appSettings.BindingZipPartialCompleteVolumeSuffix.Value = reader.IsDBNull(22) ? "巻" : reader.GetString(22);
			this.appSettings.BindingZipCompleteVolumeZeroPadding.Value = !reader.IsDBNull(23) && reader.GetInt32(23) != 0;
		}
	}
}
