using System.Data.SQLite;
using System.IO;
using MangaBinder.Bindings;
using MangaBinder.Bindings.Inspection;
using MangaBinder.Bindings.Prepress;
using MangaBinder.Converters;
using MangaBinder.Jobs;
using MangaBinder.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;

namespace MangaBinder.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> の拡張メソッドを提供します。
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// SQLite の接続文字列を生成し、関連サービスを DI 登録します。
	/// DB ファイルの物理的な初期化は <see cref="MangaBinderInitializer"/> が担います。
	/// </summary>
	/// <param name="services">DI サービスコレクション。</param>
	/// <param name="config">アプリケーション設定。</param>
	/// <returns>メソッドチェーン用に <paramref name="services"/> を返します。</returns>
	public static IServiceCollection AddMangaDb(this IServiceCollection services, IConfiguration config)
	{
		var relativePath = config["Database:RelativePath"]!;
		var dbPath = Path.Combine(AppContext.BaseDirectory, relativePath);

		var connectionString = new SQLiteConnectionStringBuilder
		{
			DataSource = dbPath,
			JournalMode = SQLiteJournalModeEnum.Wal,
			BusyTimeout = 5000,
		}.ToString();

		var appSettings = new AppSettings { ConnectionString = connectionString };
		services.AddSingleton(appSettings);
		services.AddSingleton<IMangaBinderConfig>(appSettings);
		services.AddScoped(sp => new SharedSettingsRepository(sp.GetRequiredService<AppSettings>().ConnectionString));
		services.AddScoped<JobRepository>();
		services.AddScoped<AppSettingsService>();
		services.AddScoped<MangaRepository>();
		services.AddScoped<WorkMangaSeriesRepository>();
		services.AddScoped<BindingQueueRepository>();

		return services;
	}

	/// <summary>
	/// ページ（View）を DI コンテナに登録します。
	/// </summary>
	/// <param name="services">DI サービスコレクション。</param>
	/// <returns>メソッドチェーン用に <paramref name="services"/> を返します。</returns>
	public static IServiceCollection AddPages(this IServiceCollection services)
	{
		services.AddNavigationPageWithSingletonViewModel<HomePage, HomePageViewModel>();
		services.AddNavigationPage<TagPage, TagPageViewModel>();
		services.AddScoped<TagRepository>();
		services.AddScoped<TagEditor>();
		services.AddScoped<SeriesTagDispatcher>();
		services.AddScoped<ThumbnailManager>();
		services.AddScoped<MangaSeriesManager>();
		services.AddNavigationPageWithSingletonViewModel<StartPage, StartPageViewModel>();
		services.AddNavigationPageWithSingletonViewModel<VolumeSelectionPage, VolumeSelectionPageViewModel>();
		services.AddNavigationPageWithSingletonViewModel<SeriesInspectionPage, SeriesInspectionPageViewModel>();
		services.AddNavigationPageWithSingletonViewModel<MaintenancePage, MaintenancePageViewModel>();
		services.AddNavigationPage<VolumeThumbnailsPage, VolumeThumbnailsPageViewModel>();
		services.AddNavigationPage<SpreadSplitterPage, SpreadSplitterPageViewModel>();
		services.AddNavigationPage<EditorPage, EditorPageViewModel>();
		services.AddScoped<ThumbnailPicker>();
		services.AddScoped<IThumbnailImageProcessor, ThumbnailImageProcessor>();
		services.AddScoped<OwnedVolumeEstimator>();
		services.AddSingleton<IContentDialogService, ContentDialogService>();
		services.AddSingleton<VolumeFileNameNormalizer>();
		services.AddSingleton<VolumeThumbnailImageProcessor>();
		services.AddSingleton<VolumeThumbnailLoader>();
		services.AddSingleton<ThumbnailImageLoader>();
		services.AddSingleton<ThumbnailImageConverter>();
		services.AddSingleton<ThumbnailPreviewOrDefaultConverter>();
		services.AddSingleton<SpreadSplitterImageLoader>();

		services.AddSingleton<SeriesWorkspaceStore>();
		services.AddSingleton<SeriesTagStore>();
		services.AddSingleton<MangaSeriesStore>();
		services.AddSingleton<BindingQueueStore>();
		services.AddSingleton<BindingStoreRepository>();
		services.AddSingleton<BindingQueueDispatcher>();
		services.AddSingleton<MaterialSourceDisplayNameConverter>();

		services.AddScoped<IVolumeImageProcessor, VolumeImageProcessor>();
		services.AddScoped<FolderVolumeExtractor>();
		services.AddScoped<ArchiveVolumeExtractor>();
		services.AddScoped<EpubVolumeExtractor>();
		services.AddScoped<ISeriesExtractor, MaterialFolderSeriesExtractor>();
		services.AddScoped<VolumeNumberExtractor>();
		services.AddScoped<WorkFolderBuilder>();
		services.AddScoped<BindingVolumeTextFormatter>();
		services.AddScoped<BindingZipFileNameFormatter>();
		services.AddScoped<MaterialArchiveExtractor>();
		services.AddScoped(sp => new MaterialArchiveRepository(sp.GetRequiredService<AppSettings>().ConnectionString));
		services.AddScoped<SeriesMaterialFolderLoader>();
		services.AddScoped<MaterialFolderOpener>();
		services.AddScoped<MaterialManager>();

		return services;
	}
}
