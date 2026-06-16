using Microsoft.Extensions.Logging;

namespace MangaBinder.Jobs;

/// <summary>
/// Worker 起動時の JobQueue 初期化を担当するサービスです。
/// 前回起動の異常終了や不要なジョブをクリーンアップします。
/// </summary>
public class JobQueueService
{
	/// <summary>ジョブリポジトリ。</summary>
	private readonly JobRepository repository;

	/// <summary>ロガー。</summary>
	private readonly ILogger<JobQueueService> logger;

	/// <summary>
	/// <see cref="JobQueueService"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="repository">ジョブリポジトリ。</param>
	/// <param name="logger">ロガー。</param>
	public JobQueueService(JobRepository repository, ILogger<JobQueueService> logger)
	{
		this.repository = repository;
		this.logger = logger;
	}

	/// <summary>
	/// JobQueue を初期化します。
	/// 以下の順序でクリーンアップを実行します：
	/// 1. Running ジョブを Pending に戻す（異常終了時の復旧）
	/// 2. Success ジョブを削除（実行済みジョブは不要）
	/// 3. 3ヶ月以上前の Error ジョブを削除（古いエラー履歴の整理）
	/// </summary>
	/// <param name="ct">キャンセルトークン。</param>
	public async ValueTask InitializeAsync(CancellationToken ct = default)
	{
		this.logger.LogInformation("JobQueue 初期化を開始します。");

		try
		{
			await this.repository.ResetRunningJobsAsync(ct);
			await this.repository.DeleteSuccessJobsAsync(ct);
			await this.repository.DeleteExpiredErrorJobsAsync(ct);

			this.logger.LogInformation("JobQueue 初期化が完了しました。");
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "JobQueue 初期化中にエラーが発生しました。");
			throw;
		}
	}
}
