using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Jobs;

/// <summary>
/// 実行期限に到達したスケジュールを JobQueue へ登録するサービスです。
/// </summary>
public class JobScheduler
{
    /// <summary>スコープファクトリ。</summary>
    private readonly IServiceScopeFactory scopeFactory;

    /// <summary>ロガー。</summary>
    private readonly ILogger<JobScheduler> logger;

    /// <summary>Startup Job を今回の起動中に既に enqueue したかどうか。</summary>
    private bool startupEnqueued;

    /// <summary>
    /// <see cref="JobScheduler"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="scopeFactory">スコープファクトリ。</param>
    /// <param name="logger">ロガー。</param>
    public JobScheduler(IServiceScopeFactory scopeFactory, ILogger<JobScheduler> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    /// <summary>
    /// 実行期限に到達したスケジュールを JobQueue へ登録します。
    /// Startup タイプのスケジュールは、Worker 起動後に Pending がないタイミングで 1 回だけ登録します。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async ValueTask EnqueueDueJobsAsync(CancellationToken ct)
    {
        await using var scope = this.scopeFactory.CreateAsyncScope();
        var scheduleRepository = scope.ServiceProvider.GetRequiredService<JobScheduleRepository>();
        var jobRepository      = scope.ServiceProvider.GetRequiredService<JobRepository>();

        // Startup スケジュールの初期登録（未登録なら INSERT）
        await scheduleRepository.EnsureStartupSchedulesAsync(ct);

        // Startup Job を起動中に 1 回だけ enqueue
        if (!this.startupEnqueued)
        {
            await this.enqueueStartupJobsAsync(scheduleRepository, jobRepository, ct);
            this.startupEnqueued = true;
        }

        // 通常スケジュール（期限到来分）
        var now = DateTime.Now;
        var schedules = await scheduleRepository.GetDueSchedulesAsync(now);

        this.logger.LogInformation("期限到来スケジュール数: {Count}", schedules.Count);

        foreach (var schedule in schedules)
        {
            this.logger.LogInformation("JobQueue 登録開始。JobType={JobType} ScheduleId={Id}", schedule.JobType, schedule.Id);

            await jobRepository.EnqueueAsync(schedule.JobType, false);

            var nextRunAt = calculateNextRunAt(schedule, now);

            this.logger.LogInformation("NextRunAt 更新。ScheduleId={Id} NextRunAt={NextRunAt}", schedule.Id, nextRunAt);

            await scheduleRepository.UpdateQueuedAsync(schedule.Id, now, nextRunAt);
        }
    }

    /// <summary>
    /// Startup タイプのスケジュールを取得し、JobQueue へ enqueue します。
    /// </summary>
    private async ValueTask enqueueStartupJobsAsync(JobScheduleRepository scheduleRepository, JobRepository jobRepository, CancellationToken ct)
    {
        var startupSchedules = await scheduleRepository.GetStartupSchedulesAsync();

        foreach (var schedule in startupSchedules)
        {
            this.logger.LogInformation("Startup Job を enqueue します。JobType={JobType} ScheduleId={Id}", schedule.JobType, schedule.Id);
            await jobRepository.EnqueueAsync(schedule.JobType, false);
        }
    }

    /// <summary>
    /// スケジュール設定に基づいて次回実行日時を計算します。
    /// </summary>
    /// <param name="schedule">ジョブスケジュール。</param>
    /// <param name="now">現在日時。</param>
    /// <returns>次回実行日時。</returns>
    private static DateTime calculateNextRunAt(JobSchedule schedule, DateTime now)
    {
        switch (schedule.ScheduleType)
        {
            case JobScheduleType.Daily:
            {
                var time = TimeSpan.Parse(schedule.TimeOfDay);
                var candidate = now.Date.Add(time);
                return candidate > now ? candidate : candidate.AddDays(1);
            }
            case JobScheduleType.Weekly:
            {
                var time = TimeSpan.Parse(schedule.TimeOfDay);
                var targetDow = (DayOfWeek)schedule.DayOfWeek;
                var candidate = now.Date.Add(time);
                var daysUntil = ((int)targetDow - (int)now.DayOfWeek + 7) % 7;
                candidate = candidate.AddDays(daysUntil);
                return candidate > now ? candidate : candidate.AddDays(7);
            }
            case JobScheduleType.Interval:
                return now.AddMinutes(schedule.IntervalMinutes);
            case JobScheduleType.Startup:
                // Startup タイプは NextRunAt を使わないため、現在時刻をそのまま返す
                return now;
            default:
                throw new InvalidOperationException($"未知の ScheduleType です。ScheduleType={schedule.ScheduleType}");
        }
    }
}
