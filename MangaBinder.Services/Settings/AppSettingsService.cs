using Dapper;
using MangaBinder.Jobs;
using System.Data.SQLite;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Settings;

/// <summary>
/// アプリケーション設定の読み込み・保存・素材フォルダ初期化を担うサービスクラスです。
/// </summary>
public class AppSettingsService
{
	/// <summary>アプリケーション設定インスタンス。</summary>
	private readonly AppSettings appSettings;

	/// <summary>ジョブリポジトリ。</summary>
	private readonly JobRepository jobRepository;

	/// <summary>共用設定リポジトリ。</summary>
	private readonly SharedSettingsRepository sharedSettingsRepository;

	/// <summary>ロガー。</summary>
	private readonly ILogger<AppSettingsService> logger;

	/// <summary>
	/// <see cref="AppSettingsService"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定インスタンス。</param>
	/// <param name="jobRepository">ジョブリポジトリ。</param>
	/// <param name="sharedSettingsRepository">共用設定リポジトリ。</param>
	/// <param name="logger">ロガー。</param>
	public AppSettingsService(
		AppSettings appSettings,
		JobRepository jobRepository,
		SharedSettingsRepository sharedSettingsRepository,
		ILogger<AppSettingsService> logger)
	{
		this.appSettings = appSettings;
		this.jobRepository = jobRepository;
		this.sharedSettingsRepository = sharedSettingsRepository;
		this.logger = logger;
	}

	/// <summary>
	/// データベースからアプリケーション設定および対象フォルダ一覧を非同期で読み込みます。
	/// 読み込み完了後に <see cref="AppSettings.UpdateSnapshot"/> を呼び出して初期状態を確定します。
	/// </summary>
	public async ValueTask LoadAsync()
	{
		this.logger.LogInformation("設定データの読み込みを開始します。");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		await this.loadAppSettingsAsync(connection);
		await this.loadSourceFoldersAsync();
		await this.loadSupportedExtensionsAsync();

		this.appSettings.UpdateSnapshot();

		this.logger.LogInformation("設定データの読み込みが完了しました。");
	}

	/// <summary>
	/// データベースへ AppSettings テーブルの設定値のみを非同期で保存します。
	/// SourceFolders の更新と Job 投入は行いません。
	/// UI 設定・軽量状態保存や終了時保存に使用します。
	/// 保存完了後に <see cref="AppSettings.UpdateSnapshot"/> を呼び出してスナップショットを更新します。
	/// </summary>
	public async ValueTask SaveAppSettingsAsync()
	{
		this.logger.LogInformation("AppSettings の保存を開始します。");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		using var transaction = connection.BeginTransaction();
		await this.persistAppSettingsToDbAsync(connection, transaction);
		transaction.Commit();

		this.appSettings.UpdateSnapshot();

		this.logger.LogInformation("AppSettings の保存が完了しました。");
	}

	/// <summary>
	/// SourceFolders を保存し、MaterialScan・BindingScan の Job を投入します。
	/// 初回素材フォルダ登録時など、重い初期化処理が必要な場合に使用します。
	/// 保存完了後に <see cref="AppSettings.UpdateSnapshot"/> を呼び出してスナップショットを更新します。
	/// </summary>
	public async ValueTask InitializeSourceFoldersAsync()
	{
		this.logger.LogInformation("SourceFolders の初期化を開始します。");

		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		using var transaction = connection.BeginTransaction();
		await this.saveSourceFoldersAsync(connection, transaction);
		await this.jobRepository.EnqueueAsync(JobType.MaterialScan, connection, transaction, false);
		await this.jobRepository.EnqueueAsync(JobType.BindingScan, connection, transaction, false);
		transaction.Commit();

		this.appSettings.UpdateSnapshot();

		this.logger.LogInformation("SourceFolders の初期化が完了しました。");
	}

	/// <summary>
	/// AppSettings テーブルの設定値を非同期で更新します。
	/// </summary>
	private async ValueTask persistAppSettingsToDbAsync(SQLiteConnection connection, SQLiteTransaction transaction)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" UPDATE AppSettings ");
		sql.AppendLine(" SET ");
		sql.AppendLine(" 	  WorkFolderPath      = :WorkFolderPath ");
		sql.AppendLine(" 	, ThumbnailFolderPath = :ThumbnailFolderPath ");
		sql.AppendLine(" 	, ThumbnailWidth      = :ThumbnailWidth ");
		sql.AppendLine(" 	, ThumbnailHeight     = :ThumbnailHeight ");
		sql.AppendLine(" 	, BindingDefaultImageExtension   = :BindingDefaultImageExtension ");
		sql.AppendLine(" 	, BindingDefaultArchiveExtension = :BindingDefaultArchiveExtension ");
		sql.AppendLine(" 	, BindingConvertImagesToDefaultFormat = :BindingConvertImagesToDefaultFormat ");
		sql.AppendLine(" 	, WorkVolumeFolderNamePrefix    = :WorkVolumeFolderNamePrefix ");
		sql.AppendLine(" 	, WorkVolumeFolderNameSuffix    = :WorkVolumeFolderNameSuffix ");
		sql.AppendLine(" 	, WorkVolumeFolderNumberDigits  = :WorkVolumeFolderNumberDigits ");
		sql.AppendLine(" 	, SeriesListVerticalOffset      = :SeriesListVerticalOffset; ");

		var param = new
		{
			WorkFolderPath = this.appSettings.WorkFolderPath.Value,
			ThumbnailFolderPath = this.appSettings.ThumbnailFolderPath.Value,
			ThumbnailWidth = this.appSettings.ThumbnailWidth.Value,
			ThumbnailHeight = this.appSettings.ThumbnailHeight.Value,
			BindingDefaultImageExtension = this.appSettings.BindingDefaultImageExtension.Value,
			BindingDefaultArchiveExtension = this.appSettings.BindingDefaultArchiveExtension.Value,
			BindingConvertImagesToDefaultFormat = this.appSettings.BindingConvertImagesToDefaultFormat.Value ? 1 : 0,
			WorkVolumeFolderNamePrefix = this.appSettings.WorkVolumeFolderNamePrefix.Value,
			WorkVolumeFolderNameSuffix = this.appSettings.WorkVolumeFolderNameSuffix.Value,
			WorkVolumeFolderNumberDigits = this.appSettings.WorkVolumeFolderNumberDigits.Value,
			SeriesListVerticalOffset = this.appSettings.SeriesListVerticalOffset.Value,
		};
		await connection.ExecuteAsync(sql.ToString(), param, transaction);
	}

	/// <summary>
	/// SourceFolders テーブルを DELETE → INSERT で洗い替えします。
	/// </summary>
	private async ValueTask saveSourceFoldersAsync(SQLiteConnection connection, SQLiteTransaction transaction)
	{
		await connection.ExecuteAsync("DELETE FROM SourceFolders", transaction: transaction);

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
		var insertSqlStr = insertSql.ToString();

		var rows = this.appSettings.SourceFolders
			.Select(f => new
			{
				FolderPath = f.FolderPath.Value,
				DisplayName = f.DisplayName.Value,
				Role = (int)f.Role.Value,
			})
			.ToList();
		await connection.ExecuteAsync(insertSqlStr, rows, transaction);
	}

	/// <summary>
	/// AppSettings テーブルから設定値を非同期で読み込みます。
	/// </summary>
	private async ValueTask loadAppSettingsAsync(SQLiteConnection connection)
	{
		var sql = new StringBuilder();
		sql.AppendLine(" SELECT ");
		sql.AppendLine(" 	  WorkFolderPath ");
		sql.AppendLine(" 	, ThumbnailFolderPath ");
		sql.AppendLine(" 	, ThumbnailWidth ");
		sql.AppendLine(" 	, ThumbnailHeight ");
		sql.AppendLine(" 	, BindingDefaultImageExtension ");
		sql.AppendLine(" 	, BindingDefaultArchiveExtension ");
		sql.AppendLine(" 	, BindingConvertImagesToDefaultFormat ");
		sql.AppendLine(" 	, WorkVolumeFolderNamePrefix ");
		sql.AppendLine(" 	, WorkVolumeFolderNameSuffix ");
		sql.AppendLine(" 	, WorkVolumeFolderNumberDigits ");
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
			this.appSettings.BindingDefaultImageExtension.Value = reader.IsDBNull(4) ? ".jpg" : reader.GetString(4);
			this.appSettings.BindingDefaultArchiveExtension.Value = reader.IsDBNull(5) ? ".zip" : reader.GetString(5);
			this.appSettings.BindingConvertImagesToDefaultFormat.Value = !reader.IsDBNull(6) && reader.GetInt32(6) != 0;
			this.appSettings.WorkVolumeFolderNamePrefix.Value = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
			this.appSettings.WorkVolumeFolderNameSuffix.Value = reader.IsDBNull(8) ? "巻" : reader.GetString(8);
			this.appSettings.WorkVolumeFolderNumberDigits.Value = reader.IsDBNull(9) ? 2 : reader.GetInt32(9);
			this.appSettings.SeriesListVerticalOffset.Value = reader.IsDBNull(10) ? 0.0 : reader.GetDouble(10);
			this.appSettings.BindingZipAuthorLeftBracket.Value = reader.IsDBNull(11) ? "[" : reader.GetString(11);
			this.appSettings.BindingZipAuthorRightBracket.Value = reader.IsDBNull(12) ? "]" : reader.GetString(12);
			this.appSettings.BindingZipNameSeparator.Value = reader.IsDBNull(13) ? " " : reader.GetString(13);
			this.appSettings.BindingZipNormalVolumePrefix.Value = reader.IsDBNull(14) ? "第" : reader.GetString(14);
			this.appSettings.BindingZipNormalVolumeSeparator.Value = reader.IsDBNull(15) ? "-" : reader.GetString(15);
			this.appSettings.BindingZipNormalVolumeSuffix.Value = reader.IsDBNull(16) ? "巻" : reader.GetString(16);
			this.appSettings.BindingZipCompleteVolumePrefix.Value = reader.IsDBNull(17) ? "全" : reader.GetString(17);
			this.appSettings.BindingZipCompleteVolumeSuffix.Value = reader.IsDBNull(18) ? "巻" : reader.GetString(18);
			this.appSettings.BindingZipPartialCompleteVolumePrefix.Value = reader.IsDBNull(19) ? "第" : reader.GetString(19);
			this.appSettings.BindingZipPartialCompleteVolumeSeparator.Value = reader.IsDBNull(20) ? "-全" : reader.GetString(20);
			this.appSettings.BindingZipPartialCompleteVolumeSuffix.Value = reader.IsDBNull(21) ? "巻" : reader.GetString(21);
		}
	}

	/// <summary>
	/// SharedSettingsRepository 経由で SourceFolders を読み込み、AppSettings に追加します。
	/// </summary>
	private async ValueTask loadSourceFoldersAsync()
	{
		var records = await this.sharedSettingsRepository.GetSourceFoldersAsync();

		foreach (var record in records)
		{
			var folder = new SourceFolder();
			folder.FolderPath.Value = record.FolderPath;
			folder.DisplayName.Value = record.DisplayName;
			folder.Role.Value = record.Role;
			this.appSettings.SourceFolders.Add(folder);
		}
	}

	/// <summary>
	/// SharedSettingsRepository 経由でサポート拡張子一覧を読み込み、AppSettings に反映します。
	/// </summary>
	private async ValueTask loadSupportedExtensionsAsync()
	{
		var extensions = await this.sharedSettingsRepository.GetSupportedFileExtensionsAsync();
		this.appSettings.ReloadSupportedExtensions(extensions);
	}
}

