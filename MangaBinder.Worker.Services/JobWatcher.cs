using System.Diagnostics;
using MangaBinder.Jobs.Contexts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MangaBinder.Jobs;

/// <summary>
/// JobQueue テーブルからジョブを取得し、実行状態を管理するバックグラウンドサービスです。
/// </summary>
public class JobWatcher : BackgroundService
{
    private readonly WorkerContext workerContext;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<JobWatcher> logger;

    /// <summary>
    /// <see cref="JobWatcher"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="workerContext">Worker 実行コンテキスト。</param>
    /// <param name="scopeFactory">スコープファクトリ。</param>
    /// <param name="logger">ロガー。</param>
    public JobWatcher(WorkerContext workerContext, IServiceScopeFactory scopeFactory, ILogger<JobWatcher> logger)
    {
        this.workerContext = workerContext;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    /// <summary>
    /// バックグラウンドサービスのメインループを実行します。
    /// <see cref="WorkerContext.IntervalSeconds"/> で指定された秒数ごとにジョブを監視します。
    /// </summary>
    /// <param name="stoppingToken">サービス停止を通知するキャンセルトークン。</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            this.logger.ZLogInformation($"JobWatcher is running...");

            try
            {
                // UI による DB 初期化（template.db コピー）完了前に接続すると
                // SQLite が空ファイルを自動生成してしまうため、ファイル存在を確認する
                if (!File.Exists(this.workerContext.DatabasePath))
                {
                    this.logger.ZLogInformation($"DB が未初期化のためスキップします: {this.workerContext.DatabasePath}");
                }
                else
                {
                    await using var scope = this.scopeFactory.CreateAsyncScope();
                    var repository = scope.ServiceProvider.GetRequiredService<global::MangaBinder.Jobs.JobRepository>();

                    var job = await repository.GetNextPendingJobAsync(stoppingToken);
                    if (job is not null)
                    {
                        this.logger.ZLogInformation($"ジョブ取得: Id={job.Id}, Type={job.Type}");

                        await repository.UpdateStatusAsync(job.Id, JobStatus.Running, stoppingToken);

                        var resultStatus = JobStatus.Success;
                        var sw = new Stopwatch();
                        try
                        {
                            var jobService = scope.ServiceProvider.GetKeyedService<IJob>(job.Type);
                            if (jobService is null)
                            {
                                this.logger.ZLogWarning($"未登録のジョブタイプ: Type={job.Type}");
                            }
                            else
                            {
                                this.logger.ZLogInformation($"ジョブ実行中: Id={job.Id}, Type={job.Type}");
                                jobService.SkipThumbnailSizeLimit = job.SkipThumbnailSizeLimit;
                                sw = Stopwatch.StartNew();
                                await jobService.ExecuteAsync(stoppingToken);
                                sw.Stop();
                            }
                        }
                        catch (Exception ex)
                        {
                            this.logger.ZLogError(ex, $"ジョブ失敗: Id={job.Id}, Type={job.Type}");
                            resultStatus = JobStatus.Error;
                        }

                        await repository.UpdateStatusAsync(job.Id, resultStatus, stoppingToken);
                        this.logger.ZLogInformation(
                            $"ジョブ完了: Id={job.Id}, Status={resultStatus}, Elapsed={sw.Elapsed}");
                    }
                    else
                    {
                        var scheduler = scope.ServiceProvider.GetRequiredService<JobScheduler>();
                        await scheduler.EnqueueDueJobsAsync(stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // stoppingToken による正常なサービス停止キャンセルのため、ログのみ出力して終了する
                this.logger.ZLogInformation($"JobWatcher がキャンセルされました。サービスを停止します。");
            }
            catch (Exception ex)
            {
                this.logger.ZLogError(ex, $"JobWatcher 実行中に予期せぬ例外が発生しました。次回実行まで待機します。");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(this.workerContext.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                this.logger.ZLogInformation($"JobWatcher がキャンセルされました。サービスを停止します。");
                break;
            }
        }
    }
}
