using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MangaBinder;
using MangaBinder.Bindings;
using MangaBinder.Jobs;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Jobs.Extensions;
using MangaBinder.Jobs.FolderScanners;
using MangaBinder.Jobs.GoogleBooks;
using MangaBinder.Jobs.LargeThumbnails;
using MangaBinder.Jobs.MaterialArchives;
using MangaBinder.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

// 多重起動防止：Generic Host 構築・JobQueue 初期化より前に判定する
using var instanceMutex = WorkerStartup.TryAcquireInstance();
if (instanceMutex is null)
    return;

var hostArgs = WorkerStartup.ApplyConsoleMode(args);

#if DEBUG
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
#endif

var builder = Host.CreateApplicationBuilder(hostArgs);

// ログ出力の開始前に、保持期間を超えた古い Worker ログを削除する（起動時に 1 回のみ）
deleteOldLogFiles(builder.Configuration.GetValue<int>("Logging:RetentionDays", Constants.DefaultLogRetentionDays));

builder.Services.AddMangaWorkerContext();
builder.Services.AddScoped<IFolderScannerRepository, FolderScannerRepository>();
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<JobQueueService>();
builder.Services.AddScoped<JobScheduleRepository>();
builder.Services.AddSingleton<JobScheduler>();
builder.Services.AddScoped<IThumbnailImageProcessor, ThumbnailImageProcessor>();
builder.Services.AddScoped<MaterialArchiveExtractor>();
builder.Services.AddScoped(sp => new MaterialArchiveRepository(sp.GetRequiredService<WorkerContext>().ConnectionString));
builder.Services.AddScoped<SeriesMaterialFolderLoader>();

builder.Logging.ClearProviders();
#if DEBUG
builder.Logging.AddZLoggerConsole(configureZLogger);
#endif
builder.Logging.AddZLoggerRollingFile(options =>
{
    options.FilePathSelector = (timestamp, sequence) =>
        Path.Combine(AppContext.BaseDirectory, "logs", $"worker_{timestamp:yyyyMMdd}_{sequence}.log");
    options.RollingInterval = RollingInterval.Day;
    configureZLogger(options);
});

builder.Services.AddKeyedScoped<IThumbnailExtractor, ArchiveThumbnailExtractor>(FileType.Archive);
builder.Services.AddKeyedScoped<IThumbnailExtractor, EpubThumbnailExtractor>(FileType.Epub);
builder.Services.AddScoped<SeriesExtractorFactory>();
builder.Services.AddScoped<ThumbnailCreator>();
builder.Services.AddScoped<LargeThumbnailRepository>();
builder.Services.AddScoped<MaterialArchiveScanRepository>();
builder.Services.AddKeyedScoped<IJob, MaterialFolderScanner>(JobType.MaterialScan);
builder.Services.AddKeyedScoped<IJob, BindingFolderScanner>(JobType.BindingScan);
builder.Services.AddKeyedScoped<IJob, LargeThumbnailCreateJob>(JobType.LargeThumbnailCreate);
builder.Services.AddScoped<IGoogleBooksImportRepository, GoogleBooksImportRepository>();
builder.Services.AddHttpClient<GoogleBooksAgent>();
builder.Services.AddScoped<GoogleBooksVolumeFilter>();
builder.Services.AddScoped<GoogleBooksImporter>();
builder.Services.AddScoped(sp =>
{
    var context = sp.GetRequiredService<WorkerContext>();
    return new SharedSettingsRepository(context.ConnectionString);
});
builder.Services.AddKeyedScoped<IJob, GoogleBooksImportJob>(JobType.GoogleBooksImport);
builder.Services.AddKeyedScoped<IJob, MaterialArchiveScanJob>(JobType.MaterialArchiveScan);
builder.Services.AddHostedService<JobWatcher>();

var host = builder.Build();

// JobQueue 初期化（Worker 起動時に 1 回実行）
using (var scope = host.Services.CreateScope())
{
    var jobQueueService = scope.ServiceProvider.GetRequiredService<JobQueueService>();
    await jobQueueService.InitializeAsync();
}

host.Run();

/// <summary>
/// 保持期間を超えた古い Worker ログを削除します。
/// AppContext.BaseDirectory/logs 配下の worker_yyyyMMdd_sequence.log 形式のファイルのみを対象とします。
/// </summary>
/// <param name="retentionDays">ログの保持日数。</param>
static void deleteOldLogFiles(int retentionDays)
{
	var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
	if (!Directory.Exists(logsPath))
		return;

	var today = DateTime.Now.Date;
	foreach (var filePath in Directory.GetFiles(logsPath, "worker_*.log"))
	{
		// ファイル名の日付部分（yyyyMMdd）を生成日として扱う
		var match = Regex.Match(Path.GetFileName(filePath), @"^worker_(\d{8})_\d+\.log$");
		if (!match.Success)
			continue;

		if (!DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var logDate))
			continue;

		if ((today - logDate).Days > retentionDays)
			File.Delete(filePath);
	}
}

static void configureZLogger(ZLoggerOptions options) =>
    options.UsePlainTextFormatter(formatter =>
        formatter.SetPrefixFormatter($"{0:yyyy/MM/dd HH:mm:ss.fff} [{1}] ",
            (in MessageTemplate template, in LogInfo info) =>
                template.Format(info.Timestamp.Local, info.LogLevel)));
