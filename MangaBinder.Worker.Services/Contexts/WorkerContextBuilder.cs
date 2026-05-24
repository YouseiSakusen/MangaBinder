using Microsoft.Extensions.Configuration;
using System.Data.SQLite;
using System.Text;
using Dapper;
using MangaBinder.Settings;

namespace MangaBinder.Jobs.Contexts;

/// <summary>
/// データベースから設定値を読み込み <see cref="WorkerContext"/> を構築するクラスです。
/// </summary>
public class WorkerContextBuilder
{
    /// <summary>アプリケーション設定。</summary>
    private readonly IConfiguration configuration;

    /// <summary>
    /// <see cref="WorkerContextBuilder"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="configuration">アプリケーション設定。</param>
    public WorkerContextBuilder(IConfiguration configuration)
        => this.configuration = configuration;

    /// <summary>DB待機の最大試行回数。</summary>
    private const int MaxRetryCount = 10;

    /// <summary>DB待機のリトライ間隔（秒）。</summary>
    private const int RetryIntervalSeconds = 5;

    /// <summary>
    /// 設定ファイルおよびデータベースから値を読み込み、<see cref="WorkerContext"/> を非同期で構築します。
    /// </summary>
    public async ValueTask<WorkerContext> BuildAsync()
    {
        var relativePath = this.configuration.GetConnectionString("MangaDb")
            ?? throw new InvalidOperationException("接続文字列 'MangaDb' が appsettings.json に見つかりません。");

        var dbPath = this.resolvePathFromAppRoot(relativePath);
        await this.waitForDatabaseAsync(dbPath);
        var connectionString = new SQLiteConnectionStringBuilder
        {
            DataSource = dbPath,
            JournalMode = SQLiteJournalModeEnum.Wal,
            BusyTimeout = 5000,
        }.ToString();

        var repo = new SharedSettingsRepository(connectionString);

        using var connection = new SQLiteConnection(connectionString);

        var sql = new StringBuilder();
        sql.AppendLine(" SELECT ");
        sql.AppendLine(" \t  ThumbnailExtractLimitFileSizeMB ");
        sql.AppendLine(" \t, TitleSeparatorChars ");
        sql.AppendLine(" \t, IntervalSeconds ");
        sql.AppendLine(" FROM ");
        sql.AppendLine(" \tWorkerSettings ");
        sql.AppendLine(" LIMIT 1; ");
        var row = await connection.QuerySingleOrDefaultAsync<SettingsRow>(sql.ToString());

        var extensions = await repo.GetSupportedFileExtensionsAsync();
        var thumbnailRow = await repo.GetThumbnailSettingsAsync();
        var thumbnailOptions = new ThumbnailOptions
        {
            Width           = (int)thumbnailRow.ThumbnailWidth,
            Height          = (int)thumbnailRow.ThumbnailHeight,
            JpegQuality     = (int)thumbnailRow.ThumbnailJpegQuality,
            BackgroundColor = thumbnailRow.ThumbnailBackgroundColor,
        };

        return new WorkerContext
        {
            ConnectionString                = connectionString,
            DatabasePath                    = dbPath,
            ThumbnailExtractLimitFileSizeMB = row?.ThumbnailExtractLimitFileSizeMB ?? 0,
            TitleSeparatorChars             = row?.TitleSeparatorChars ?? string.Empty,
            IntervalSeconds                 = (int)(row?.IntervalSeconds ?? 5),
            SupportedExtensions             = extensions,
            ThumbnailFolderPath             = this.resolvePathFromAppRoot(thumbnailRow.ThumbnailFolderPath),
            ThumbnailOptions                = thumbnailOptions,
        };
    }

    /// <summary>
    /// Worker配置フォルダの親フォルダをアプリルートとして、指定パスを絶対パスへ解決します。
    /// </summary>
    /// <param name="path">解決対象のパス。</param>
    /// <returns>解決済みの絶対パス。</returns>
    private string resolvePathFromAppRoot(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        var workerDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var appRootPath = workerDirectory.Parent!.FullName;

        return Path.GetFullPath(Path.Combine(appRootPath, path));
    }

    /// <summary>
    /// DBファイルが存在するまで最大 <see cref="MaxRetryCount"/> 回待機します。
    /// </summary>
    /// <param name="dbPath">待機対象のDBファイルパス。</param>
    private async ValueTask waitForDatabaseAsync(string dbPath)
    {
        for (var i = 0; i < MaxRetryCount; i++)
        {
            if (File.Exists(dbPath))
                return;

            await Task.Delay(TimeSpan.FromSeconds(RetryIntervalSeconds));
        }

        if (!File.Exists(dbPath))
            throw new InvalidOperationException(
                $"データベースファイルが見つかりません: {dbPath}{Environment.NewLine}" +
                $"先にUIアプリを起動してDBを初期化してください。");
    }

    /// <summary>WorkerSettings 行マッピング用レコードです。</summary>
    private sealed record SettingsRow(
        long ThumbnailExtractLimitFileSizeMB,
        string TitleSeparatorChars,
        long IntervalSeconds);
}
