using MangaBinder.Settings;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books 書誌情報インポートジョブです。
/// </summary>
public class GoogleBooksImportJob : IJob
{
	/// <summary>インポーター。</summary>
	private readonly GoogleBooksImporter importer;

	/// <summary>リポジトリ。</summary>
	private readonly IGoogleBooksImportRepository repository;

	/// <summary>DB 共有設定リポジトリ（ExcludeWords 取得用）。</summary>
	private readonly SharedSettingsRepository sharedSettings;

	/// <summary>クォータ管理（Job が所有）。</summary>
	private GoogleBooksQuotaManager? quotaManager;

	/// <summary>ロガー。</summary>
	private readonly ILogger<GoogleBooksImportJob> logger;

	/// <inheritdoc />
	public bool SkipThumbnailSizeLimit { get; set; }

	/// <summary>
	/// <see cref="GoogleBooksImportJob"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="importer">インポーター。</param>
	/// <param name="repository">インポートリポジトリ。</param>
	/// <param name="sharedSettings">共有設定リポジトリ。</param>
	/// <param name="logger">ロガー。</param>
	public GoogleBooksImportJob(
		GoogleBooksImporter importer,
		IGoogleBooksImportRepository repository,
		SharedSettingsRepository sharedSettings,
		ILogger<GoogleBooksImportJob> logger)
	{
		this.importer           = importer;
		this.repository         = repository;
		this.sharedSettings     = sharedSettings;
		this.logger             = logger;
	}

	/// <inheritdoc />
	public async ValueTask ExecuteAsync(CancellationToken ct)
	{
		this.logger.ZLogInformation($"Google Books インポートジョブを開始します。");

		var settingsFilePath = Path.GetFullPath(
			Path.Combine(AppContext.BaseDirectory, "..", "google-books-settings.json"));
		var settingsRepository = new GoogleBooksSettingsRepository(settingsFilePath);

		var excludeWords = await this.sharedSettings.GetGoogleBooksExcludeWordsAsync(ct);
		var settings     = await settingsRepository.LoadAsync(excludeWords, ct);
		var qm           = new GoogleBooksQuotaManager(settings);
		this.quotaManager = qm;
		qm.ResetIfNeeded();

		if (!qm.CanCall)
		{
			this.logger.ZLogInformation($"Quota上限に達しているため Google Books インポートジョブを終了します。CallCount={qm.CallCount}");
			return;
		}

		if (string.IsNullOrWhiteSpace(settings.ApiKey))
		{
			this.logger.ZLogInformation($"APIキーが設定されていないため Google Books API 呼び出しをスキップします。");
			return;
		}

		var targets = await this.repository.GetImportTargetsAsync(ct);
		this.logger.ZLogInformation($"対象作品数: {targets.Count}");

		if (targets.Count == 0)
		{
			this.logger.ZLogInformation($"対象作品が0件のため終了します。");
			await settingsRepository.SaveAsync(settings, ct);
			return;
		}

		this.importer.ApplySettings(settings, qm);

		foreach (var series in targets)
		{
			ct.ThrowIfCancellationRequested();
			this.logger.ZLogInformation($"インポート開始: {series.Title}");
			try
			{
				await this.importer.ImportAsync(series, ct);
				this.logger.ZLogInformation($"インポート完了: {series.Title}");
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				// ImportAsync 内で UpdateImportFailedAsync 済み。ここではログのみ記録して次へ進む。
				this.logger.ZLogError(ex, $"インポートに失敗しました。次の作品へ進みます: {series.Title}");
			}

			if (!qm.CanCall)
			{
				this.logger.ZLogInformation($"Quota上限に達したためジョブを終了します。CallCount={qm.CallCount}");
				break;
			}
		}

		await settingsRepository.SaveAsync(settings, ct);
		this.logger.ZLogInformation($"Google Books インポートジョブが完了しました。");
	}
}
