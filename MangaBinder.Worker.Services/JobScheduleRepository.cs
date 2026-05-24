using Dapper;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Jobs;

/// <summary>
/// JobSchedules テーブルへの読み書きを担うリポジトリクラスです。
/// </summary>
public class JobScheduleRepository
{
    /// <summary>アプリケーション共通設定。</summary>
    private readonly IMangaBinderConfig config;

    /// <summary>ロガー。</summary>
    private readonly ILogger<JobScheduleRepository> logger;

    /// <summary>
    /// <see cref="JobScheduleRepository"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="config">アプリケーション共通設定。</param>
    /// <param name="logger">ロガー。</param>
    public JobScheduleRepository(IMangaBinderConfig config, ILogger<JobScheduleRepository> logger)
    {
        this.config = config;
        this.logger = logger;
    }

    /// <summary>
    /// 実行期限を過ぎた有効スケジュールを昇順で取得します。
    /// </summary>
    /// <param name="now">現在日時。</param>
    /// <returns>実行期限を過ぎた有効スケジュールの一覧。</returns>
    public async ValueTask<IReadOnlyList<JobSchedule>> GetDueSchedulesAsync(DateTime now)
    {
        this.logger.LogInformation("実行期限済みスケジュールの取得を開始します。Now={Now}", now);

        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	  Id ");
        sql.AppendLine(" 	, JobType ");
        sql.AppendLine(" 	, Enabled ");
        sql.AppendLine(" 	, ScheduleType ");
        sql.AppendLine(" 	, DayOfWeek ");
        sql.AppendLine(" 	, TimeOfDay ");
        sql.AppendLine(" 	, IntervalMinutes ");
        sql.AppendLine(" 	, NextRunAt ");
        sql.AppendLine(" 	, LastQueuedAt ");
        sql.AppendLine(" 	, CreatedAt ");
        sql.AppendLine(" 	, UpdatedAt ");
        sql.AppendLine(" FROM ");
        sql.AppendLine(" 	JobSchedules ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 		Enabled = 1 ");
        sql.AppendLine(" 	AND NextRunAt <> '' ");
        sql.AppendLine(" 	AND NextRunAt <= :Now ");
        sql.AppendLine(" ORDER BY ");
        sql.AppendLine(" 	NextRunAt ASC; ");

        var param = new { Now = formatDateTime(now) };

        using var connection = new SQLiteConnection(this.config.ConnectionString);
        await connection.OpenAsync();

        var result = await connection.QueryAsync<JobSchedule>(sql.ToString(), param);

        this.logger.LogInformation("実行期限済みスケジュールを {Count} 件取得しました。", result.AsList().Count);

        return result.AsList().AsReadOnly();
    }

    /// <summary>
    /// 指定スケジュールの LastQueuedAt / NextRunAt を更新します。
    /// </summary>
    /// <param name="scheduleId">更新対象のスケジュール ID。</param>
    /// <param name="queuedAt">キュー登録日時。</param>
    /// <param name="nextRunAt">次回実行予定日時。</param>
    public async ValueTask UpdateQueuedAsync(long scheduleId, DateTime queuedAt, DateTime nextRunAt)
    {
        this.logger.LogInformation("スケジュールの実行日時を更新します。ScheduleId={Id}", scheduleId);

        var sql = new StringBuilder();
        sql.AppendLine(" UPDATE JobSchedules ");
        sql.AppendLine(" SET ");
        sql.AppendLine(" 	  LastQueuedAt = :LastQueuedAt ");
        sql.AppendLine(" 	, NextRunAt = :NextRunAt ");
        sql.AppendLine(" 	, UpdatedAt = DATETIME('now', 'localtime') ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 	Id = :Id; ");

        var param = new
        {
            LastQueuedAt = formatDateTime(queuedAt),
            NextRunAt = formatDateTime(nextRunAt),
            Id = scheduleId,
        };

        using var connection = new SQLiteConnection(this.config.ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(sql.ToString(), param);

        this.logger.LogInformation("スケジュールの実行日時を更新しました。ScheduleId={Id}", scheduleId);
    }

    /// <summary>
    /// <see cref="DateTime"/> を SQLite 保存用の文字列に変換します。
    /// </summary>
    private static string formatDateTime(DateTime value)
        => value.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Startup タイプの有効スケジュールをすべて取得します。
    /// </summary>
    /// <returns>Startup スケジュールの一覧。</returns>
    public async ValueTask<IReadOnlyList<JobSchedule>> GetStartupSchedulesAsync()
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	  Id ");
        sql.AppendLine(" 	, JobType ");
        sql.AppendLine(" 	, Enabled ");
        sql.AppendLine(" 	, ScheduleType ");
        sql.AppendLine(" 	, DayOfWeek ");
        sql.AppendLine(" 	, TimeOfDay ");
        sql.AppendLine(" 	, IntervalMinutes ");
        sql.AppendLine(" 	, NextRunAt ");
        sql.AppendLine(" 	, LastQueuedAt ");
        sql.AppendLine(" 	, CreatedAt ");
        sql.AppendLine(" 	, UpdatedAt ");
        sql.AppendLine(" FROM ");
        sql.AppendLine(" 	JobSchedules ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 		Enabled = 1 ");
        sql.AppendLine(" 	AND ScheduleType = :ScheduleType; ");

        var param = new { ScheduleType = (int)JobScheduleType.Startup };

        using var connection = new SQLiteConnection(this.config.ConnectionString);
        await connection.OpenAsync();

        var result = await connection.QueryAsync<JobSchedule>(sql.ToString(), param);
        return result.AsList().AsReadOnly();
    }

    /// <summary>
    /// Startup タイプのジョブスケジュールが未登録の場合のみ初期登録します。
    /// 既に同一 <see cref="JobType"/> の Startup スケジュールが存在する場合はスキップします。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async ValueTask EnsureStartupSchedulesAsync(CancellationToken ct)
    {
        using var connection = new SQLiteConnection(this.config.ConnectionString);
        await connection.OpenAsync(ct);

        await this.ensureStartupScheduleAsync(connection, JobType.GoogleBooksImport, ct);
    }

    /// <summary>
    /// 指定した JobType の Startup スケジュールが未登録の場合のみ INSERT します。
    /// </summary>
    private async ValueTask ensureStartupScheduleAsync(SQLiteConnection connection, JobType jobType, CancellationToken ct)
    {
        var existsSql = new StringBuilder();
        existsSql.AppendLine(" SELECT COUNT(*) FROM JobSchedules ");
        existsSql.AppendLine(" WHERE JobType = :JobType AND ScheduleType = :ScheduleType; ");

        var count = await connection.ExecuteScalarAsync<int>(existsSql.ToString(), new
        {
            JobType      = (int)jobType,
            ScheduleType = (int)JobScheduleType.Startup,
        });

        if (count > 0)
            return;

        this.logger.LogInformation("Startup スケジュールを登録します。JobType={JobType}", jobType);

        var insertSql = new StringBuilder();
        insertSql.AppendLine(" INSERT INTO JobSchedules ( ");
        insertSql.AppendLine(" 	  JobType ");
        insertSql.AppendLine(" 	, Enabled ");
        insertSql.AppendLine(" 	, ScheduleType ");
        insertSql.AppendLine(" 	, DayOfWeek ");
        insertSql.AppendLine(" 	, TimeOfDay ");
        insertSql.AppendLine(" 	, IntervalMinutes ");
        insertSql.AppendLine(" 	, NextRunAt ");
        insertSql.AppendLine(" 	, LastQueuedAt ");
        insertSql.AppendLine(" 	, CreatedAt ");
        insertSql.AppendLine(" 	, UpdatedAt ");
        insertSql.AppendLine(" ) VALUES ( ");
        insertSql.AppendLine(" 	  :JobType ");
        insertSql.AppendLine(" 	, 1 ");
        insertSql.AppendLine(" 	, :ScheduleType ");
        insertSql.AppendLine(" 	, 0 ");
        insertSql.AppendLine(" 	, '' ");
        insertSql.AppendLine(" 	, 0 ");
        insertSql.AppendLine(" 	, '' ");
        insertSql.AppendLine(" 	, '' ");
        insertSql.AppendLine(" 	, DATETIME('now', 'localtime') ");
        insertSql.AppendLine(" 	, DATETIME('now', 'localtime') ");
        insertSql.AppendLine(" ); ");

        await connection.ExecuteAsync(insertSql.ToString(), new
        {
            JobType      = (int)jobType,
            ScheduleType = (int)JobScheduleType.Startup,
        });

        this.logger.LogInformation("Startup スケジュールを登録しました。JobType={JobType}", jobType);
    }
}
