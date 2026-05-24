using Dapper;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using System.Text;

namespace MangaBinder.Jobs;

/// <summary>
/// JobQueue テーブルへの読み書きを一手に担うリポジトリクラスです。
/// UI・Worker 双方から利用できます。
/// </summary>
public class JobRepository
{
    /// <summary>アプリケーション共通設定。</summary>
    private readonly IMangaBinderConfig config;

    /// <summary>ロガー。</summary>
    private readonly ILogger<JobRepository> logger;

    /// <summary>
    /// <see cref="JobRepository"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="config">アプリケーション共通設定。</param>
    /// <param name="logger">ロガー。</param>
    public JobRepository(IMangaBinderConfig config, ILogger<JobRepository> logger)
    {
        this.config = config;
        this.logger = logger;
    }

    /// <summary>
    /// 指定されたジョブタイプをキューへ非同期で登録します。
    /// 同一タイプで <see cref="JobStatus.Pending"/> または <see cref="JobStatus.Running"/> のレコードが
    /// 既に存在する場合は登録をスキップします。
    /// </summary>
    /// <param name="type">登録するジョブタイプ。</param>
    public async ValueTask EnqueueAsync(JobType type, bool skipThumbnailSizeLimit)
    {
        this.logger.LogInformation("ジョブのキュー登録を開始します。JobType={Type}", type);

        using var connection = new SQLiteConnection(this.config.ConnectionString);
        await connection.OpenAsync();

        if (await this.existsPendingOrRunningAsync(connection, null, type))
        {
            this.logger.LogInformation("同一ジョブが既にキューに存在するためスキップします。JobType={Type}", type);
            return;
        }

        await this.insertJobAsync(connection, null, type, skipThumbnailSizeLimit);

        this.logger.LogInformation("ジョブのキュー登録が完了しました。JobType={Type}", type);
    }

    /// <summary>
    /// 指定されたジョブタイプをキューへ非同期で登録します（外部トランザクション使用）。
    /// 同一タイプで <see cref="JobStatus.Pending"/> または <see cref="JobStatus.Running"/> のレコードが
    /// 既に存在する場合は登録をスキップします。
    /// </summary>
    /// <param name="type">登録するジョブタイプ。</param>
    /// <param name="connection">使用する SQLite 接続。</param>
    /// <param name="transaction">使用するトランザクション。</param>
    public async ValueTask EnqueueAsync(JobType type, SQLiteConnection connection, SQLiteTransaction transaction, bool skipThumbnailSizeLimit)
    {
        this.logger.LogInformation("ジョブのキュー登録を開始します。JobType={Type}", type);

        if (await this.existsPendingOrRunningAsync(connection, transaction, type))
        {
            this.logger.LogInformation("同一ジョブが既にキューに存在するためスキップします。JobType={Type}", type);
            return;
        }

        await this.insertJobAsync(connection, transaction, type, skipThumbnailSizeLimit);

        this.logger.LogInformation("ジョブのキュー登録が完了しました。JobType={Type}", type);
    }

    /// <summary>
    /// Status = Pending のレコードのうち、最も古いジョブを 1 件取得します。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>次に実行すべきジョブ。存在しない場合は <see langword="null"/>。</returns>
    public async ValueTask<Jobs.JobQueue?> GetNextPendingJobAsync(CancellationToken ct)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" 	  Id ");
        sql.AppendLine(" 	, Type ");
        sql.AppendLine(" 	, Status ");
        sql.AppendLine(" 	, CreatedAt ");
        sql.AppendLine(" 	, SkipThumbnailSizeLimit ");
        sql.AppendLine(" FROM ");
        sql.AppendLine(" 	JobQueue ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 	Status = :Pending ");
        sql.AppendLine(" ORDER BY ");
        sql.AppendLine(" 	CreatedAt ASC ");
        sql.AppendLine(" LIMIT 1; ");

        var param = new { Pending = (int)JobStatus.Pending };

        using var connection = new SQLiteConnection(this.config.ConnectionString);
        await connection.OpenAsync(ct);

        return await connection.QueryFirstOrDefaultAsync<Jobs.JobQueue>(sql.ToString(), param);
    }

    /// <summary>
    /// 指定されたジョブの Status を更新します。
    /// </summary>
    /// <param name="jobId">更新対象のジョブ ID。</param>
    /// <param name="status">更新後のステータス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async ValueTask UpdateStatusAsync(long jobId, JobStatus status, CancellationToken ct)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" UPDATE JobQueue ");
        sql.AppendLine(" SET ");
        sql.AppendLine(" 	Status = :Status ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 	Id = :Id; ");

        var param = new
        {
            Status = (int)status,
            Id = jobId,
        };

        using var connection = new SQLiteConnection(this.config.ConnectionString);
        await connection.OpenAsync(ct);

        await connection.ExecuteAsync(sql.ToString(), param);
    }

    /// <summary>
    /// 同一タイプで待機中または実行中のジョブが存在するか確認します。
    /// </summary>
    private async ValueTask<bool> existsPendingOrRunningAsync(SQLiteConnection connection, SQLiteTransaction? transaction, JobType type)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" SELECT COUNT(*) ");
        sql.AppendLine(" FROM ");
        sql.AppendLine(" 	JobQueue ");
        sql.AppendLine(" WHERE ");
        sql.AppendLine(" 		Type = :Type ");
        sql.AppendLine(" 	AND Status IN (:Pending, :Running); ");

        var param = new
        {
            Type = (int)type,
            Pending = (int)JobStatus.Pending,
            Running = (int)JobStatus.Running,
        };
        var count = await connection.ExecuteScalarAsync<int>(sql.ToString(), param, transaction);
        return count > 0;
    }

    /// <summary>
    /// JobQueue テーブルへジョブを挿入します。
    /// </summary>
    private async ValueTask insertJobAsync(SQLiteConnection connection, SQLiteTransaction? transaction, JobType type, bool skipThumbnailSizeLimit)
    {
        var sql = new StringBuilder();
        sql.AppendLine(" INSERT INTO JobQueue ( ");
        sql.AppendLine(" 	  Type ");
        sql.AppendLine(" 	, Status ");
        sql.AppendLine(" 	, SkipThumbnailSizeLimit ");
        sql.AppendLine(" ) VALUES ( ");
        sql.AppendLine(" 	  :Type ");
        sql.AppendLine(" 	, 0 ");
        sql.AppendLine(" 	, :SkipThumbnailSizeLimit ");
        sql.AppendLine(" ); ");

        var param = new { Type = (int)type, SkipThumbnailSizeLimit = skipThumbnailSizeLimit ? 1 : 0 };
        await connection.ExecuteAsync(sql.ToString(), param, transaction);
    }
}
