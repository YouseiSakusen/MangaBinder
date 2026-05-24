using System.Text;
using MangaBinder;
using MangaBinder.Bindings;
using MangaBinder.Jobs;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Jobs.Extensions;
using MangaBinder.Jobs.FolderScanners;
using MangaBinder.Jobs.GoogleBooks;
using MangaBinder.Jobs.LargeThumbnails;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZLogger;
using ZLogger.Providers;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMangaWorkerContext();
builder.Services.AddScoped<IFolderScannerRepository, FolderScannerRepository>();
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<JobScheduleRepository>();
builder.Services.AddSingleton<JobScheduler>();
builder.Services.AddScoped<IThumbnailImageProcessor, ThumbnailImageProcessor>();

builder.Logging.AddZLoggerConsole(configureZLogger);
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
builder.Services.AddHostedService<JobWatcher>();

var host = builder.Build();
host.Run();

static void configureZLogger(ZLoggerOptions options) =>
    options.UsePlainTextFormatter(formatter =>
        formatter.SetPrefixFormatter($"{0:yyyy/MM/dd HH:mm:ss.fff} [{1}] ",
            (in MessageTemplate template, in LogInfo info) =>
                template.Format(info.Timestamp.Local, info.LogLevel)));
