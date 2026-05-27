using System.Diagnostics;
using System.Reflection;
using System.Windows;
using HalationGhost.Wpf.Ui.Navigation;
using MangaBinder.Binding;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObservableCollections;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;
using ZLogger;

namespace MangaBinder;

/// <summary>
/// メインウィンドウの ViewModel です。
/// </summary>
public class MainWindowViewModel : IDisposable, IWindowClosingAware
{
	/// <summary>ロガー。</summary>
	private readonly ILogger<MainWindowViewModel> logger;

	/// <summary>テーマサービス。</summary>
	private readonly IThemeService themeService;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>スナックバーサービス。</summary>
	private readonly ISnackbarService snackbarService;

	/// <summary>HomePage の ViewModel。終了時のスクロール位置保存に使用します。</summary>
	private readonly HomePageViewModel homePageViewModel;

	/// <summary>アプリケーション設定。終了時のスクロール位置保存先です。</summary>
	private readonly AppSettings appSettings;

	/// <summary>スコープファクトリー。AppSettingsService の取得に使用します。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>現在表示中のページの ViewModel を保持するフィールドです。保存・変更検知に使用します。</summary>
	private object? currentViewModel;

	private DisposableBag disposableBag;

	/// <summary>
	/// アプリケーションタイトルを表す読み取り専用リアクティブプロパティです。
	/// </summary>
	public ReadOnlyReactiveProperty<string> ApplicationTitle { get; }

	/// <summary>
	/// ナビゲーションメニューのアイテムコレクションです。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<NavigationViewItem> MenuItems { get; }

	/// <summary>
	/// <see cref="MainWindowViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="logger">ロガー。</param>
	/// <param name="themeService">テーマサービス。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	public MainWindowViewModel(
		ILogger<MainWindowViewModel> logger,
		IThemeService themeService,
		INavigationService navigationService,
		ISnackbarService snackbarService,
		HomePageViewModel homePageViewModel,
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory)
	{
		this.logger = logger;
		this.themeService = themeService;
		this.navigationService = navigationService;
		this.snackbarService = snackbarService;
		this.homePageViewModel = homePageViewModel;
		this.appSettings = appSettings;
		this.serviceScopeFactory = serviceScopeFactory;

		this.logger.ZLogInformation($"MainWindowViewModel 初期化開始");

		var assemblyName = Assembly.GetEntryAssembly()?.GetName();
		var title = $"{assemblyName?.Name ?? "MangaBinder"} Ver.{assemblyName?.Version?.ToString() ?? "?"}";

		this.ApplicationTitle = Observable.Return(title)
			.ToReadOnlyReactiveProperty(title)
			.AddTo(ref this.disposableBag);

		this.MenuItems = new ObservableList<NavigationViewItem>
		{
			new NavigationViewItem
			{
				Content = "Home",
				Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
				FontSize = 24,
				TargetPageType = typeof(HomePage),
			},
			new NavigationViewItem
			{
				Content = "製本開始",
				Icon = new SymbolIcon { Symbol = SymbolRegular.Library24 },
				FontSize = 24,
				TargetPageType = typeof(StartPage),
			},
			new NavigationViewItem
			{
				Content = "設定",
				Icon = new SymbolIcon { Symbol = SymbolRegular.Wrench24 },
				FontSize = 24,
				TargetPageType = typeof(SettingsPage),
			},
		}
		.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
		.AddTo(ref this.disposableBag);

		Application.Current.Dispatcher.BeginInvoke(() => this.navigationService.Navigate(typeof(HomePage)));
		this.logger.ZLogInformation($"MainWindowViewModel 初期化完了");
	}

	/// <summary>
	/// ナビゲーション遷移時に呼び出されます。直前ページが <see cref="ISavable"/> を実装していれば保存を実行します。
	/// </summary>
	/// <param name="e">ナビゲーションイベント引数。</param>
	internal async ValueTask OnNavigated(NavigatedEventArgs e)
	{
		Debug.WriteLine("===== Navigated =====");
		Debug.WriteLine($"PageType={e.Page?.GetType().FullName}");
		Debug.WriteLine($"DataContext={((FrameworkElement)e.Page!).DataContext?.GetType().FullName}");

		await this.saveCurrentViewModelAsync();
		this.currentViewModel = ((FrameworkElement)e.Page!).DataContext;

		Debug.WriteLine($"CurrentViewModel={this.currentViewModel?.GetType().FullName}");

		if (this.currentViewModel is IDataInitializable initializable)
		{
			Debug.WriteLine("InitializeDataAsync CALL");
			await initializable.InitializeDataAsync();
		}
		else
		{
			Debug.WriteLine("IDataInitializable NOT IMPLEMENTED");
		}
	}

	/// <summary>
	/// ウィンドウが閉じられる直前に呼び出されます。現在ページが <see cref="ISavable"/> を実装していれば保存を実行します。
	/// </summary>
	/// <returns>完了を表す <see cref="ValueTask"/>。</returns>
	public async ValueTask OnClosingAsync()
	{
		await this.saveCurrentViewModelAsync();

		// 作品一覧のスクロール位置を AppSettings へ反映してDB保存する
		this.appSettings.SeriesListVerticalOffset.Value = this.homePageViewModel.SavedSeriesListVerticalOffset.Value;
		using var scope = this.serviceScopeFactory.CreateScope();
		var appSettingsService = scope.ServiceProvider.GetRequiredService<AppSettingsService>();
		await appSettingsService.SaveAppSettingsAsync();
	}

	/// <summary>
	/// <see cref="currentViewModel"/> が <see cref="ISavable"/> を実装している場合、保存を実行してスナックバーで結果を通知します。
	/// </summary>
	private async ValueTask saveCurrentViewModelAsync()
	{
		if (this.currentViewModel is not ISavable savable)
			return;

		var result = await savable.SaveAsync();
		var message = !string.IsNullOrEmpty(result.Message)
			? result.Message
			: (result.IsSuccess ? "保存しました" : "保存に失敗しました");
		var icon = result.Appearance switch
		{
			ControlAppearance.Success => SymbolRegular.CheckmarkCircle24,
			ControlAppearance.Caution => SymbolRegular.Warning24,
			ControlAppearance.Danger => SymbolRegular.ErrorCircle24,
			_ => SymbolRegular.Info24,
		};

		this.snackbarService.Show(
			"設定保存",
			message,
			result.Appearance,
			new SymbolIcon { Symbol = icon },
			result.Appearance == ControlAppearance.Danger ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(3));
	}

	/// <summary>
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
