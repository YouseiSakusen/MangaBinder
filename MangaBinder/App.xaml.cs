using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MangaBinder.Extensions;
using MangaBinder.Settings;
using Wpf.Ui;
using ZLogger;
using ZLogger.Providers;

namespace MangaBinder;

/// <summary>
/// アプリケーションのエントリーポイントです。
/// .NET 汎用ホスト（Generic Host）で DI・設定・ロギングを統合します。
/// </summary>
public partial class App
{
	// .NET 汎用ホスト。OnStartup で構築され、アプリケーション終了まで保持されます。
	private static IHost? host;

	/// <summary>
	/// DI サービスプロバイダーを取得します。ホストが未初期化の場合は例外をスローします。
	/// </summary>
	public static IServiceProvider Services => host?.Services ?? throw new InvalidOperationException("ホストが未初期化の状態でアクセスされました。");

	/// <summary>
	/// ZLogger の出力フォーマットを設定します。
	/// <c>yyyy-MM-dd HH:mm:ss.fff [LogLevel] </c> 形式のタイムスタンプをログ行先頭に付与します。
	/// </summary>
	private static void configureZLogger(ZLoggerOptions options) =>
		options.UsePlainTextFormatter(formatter =>
			formatter.SetPrefixFormatter($"{0:yyyy/MM/dd HH:mm:ss.fff} [{1}] ",
				(in MessageTemplate template, in LogInfo info) =>
					template.Format(info.Timestamp.Local, info.LogLevel)));

	/// <summary>
	/// .NET 汎用ホストを構築して返します。
	/// </summary>
	private IHost createHost()
	{
		return Host
			.CreateDefaultBuilder()
			.ConfigureAppConfiguration(c =>
			{
				c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty);
			})
			.ConfigureServices((context, services) =>
			{
				services.AddLogging(logging =>
				{
					logging.AddConfiguration(context.Configuration.GetSection("Logging"));
					logging.AddZLoggerConsole(configureZLogger);
					logging.AddZLoggerRollingFile(options =>
					{
						options.FilePathSelector = (timestamp, sequence) => Path.Combine(AppContext.BaseDirectory, "logs", $"app_{timestamp:yyyyMMdd}_{sequence}.log");
						options.RollingInterval = RollingInterval.Day;
						configureZLogger(options);
					});
				});
				services.AddMangaDb(context.Configuration);
				services.AddPages();
				services.AddSingleton<IThemeService, ThemeService>();
				services.AddSingleton<ISnackbarService, SnackbarService>();
				services.AddSingleton<LoadingService>();
				services.AddNavigationWindow<MainWindow, MainWindowViewModel>();
				services.AddTransient<SettingsPageViewModel>();
				services.AddTransient<SettingsPage>();
			})
			.Build();
	}

	/// <summary>
	/// アプリケーション起動時に実行されます。
	/// </summary>
	private async void OnStartup(object sender, StartupEventArgs e)
	{
		var config = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
			.Build();

		var initializer = new MangaBinderInitializer(config);
		await initializer.InitializeAsync();

		host = this.createHost();
		await host.StartAsync();

		var appSettingsService = host.Services.GetRequiredService<AppSettingsService>();
		await appSettingsService.LoadAsync();

		var appSettings = host.Services.GetRequiredService<AppSettings>();
		SupportedExtensionHelper.Initialize(appSettings.SupportedExtensions);

		host.Services.GetRequiredService<MainWindow>().Show();
	}

	/// <summary>
	/// アプリケーション終了時に実行されます。
	/// </summary>
	private async void OnExit(object sender, ExitEventArgs e)
	{
		await host!.StopAsync();
		host!.Dispose();
	}

	/// <summary>
	/// 未処理例外が発生した際に実行されます。
	/// </summary>
	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		host!.Services.GetRequiredService<ILogger<App>>().ZLogError(e.Exception, $"未処理例外が発生しました");
		e.Handled = false;
	}
}
