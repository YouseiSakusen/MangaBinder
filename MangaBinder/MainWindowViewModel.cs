using System.Diagnostics;
using System.Reflection;
using System.Windows;
using HalationGhost.Wpf.Ui.Navigation;
using MangaBinder.Bindings;
using MangaBinder.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;
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

	/// <summary>ローディングサービス。</summary>
	private readonly LoadingService loadingService;

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
	/// ナビゲーションフッターのアイテムコレクションです。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<NavigationViewItem> FooterMenuItems { get; }

	/// <summary>ローディング状態のリアクティブプロパティ（読み取り専用）。</summary>
	private readonly BindableReactiveProperty<bool> isLoadingProperty = new(false);

	/// <summary>ローディングメッセージのリアクティブプロパティ（読み取り専用）。</summary>
	private readonly BindableReactiveProperty<string> loadingMessageProperty = new(string.Empty);

	/// <summary>
	/// ローディング状態を表す読み取り専用リアクティブプロパティです。
	/// </summary>
	public ReadOnlyReactiveProperty<bool> IsLoading { get; }

	/// <summary>
	/// ローディングメッセージを表す読み取り専用リアクティブプロパティです。
	/// </summary>
	public ReadOnlyReactiveProperty<string> LoadingMessage { get; }

	/// <summary>
	/// <see cref="MainWindowViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="logger">ロガー。</param>
	/// <param name="themeService">テーマサービス。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	/// <param name="homePageViewModel">ホームページの ViewModel。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	/// <param name="loadingService">ローディングサービス。</param>
	public MainWindowViewModel(
		ILogger<MainWindowViewModel> logger,
		IThemeService themeService,
		INavigationService navigationService,
		ISnackbarService snackbarService,
		HomePageViewModel homePageViewModel,
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory,
		LoadingService loadingService)
	{
		this.logger = logger;
		this.themeService = themeService;
		this.navigationService = navigationService;
		this.snackbarService = snackbarService;
		this.homePageViewModel = homePageViewModel;
		this.appSettings = appSettings;
		this.serviceScopeFactory = serviceScopeFactory;
		this.loadingService = loadingService;

		this.logger.ZLogInformation($"MainWindowViewModel 初期化開始");

		var assemblyName = Assembly.GetEntryAssembly()?.GetName();
		var title = $"{assemblyName?.Name ?? "MangaBinder"} Ver.{assemblyName?.Version?.ToString() ?? "?"}";

		this.ApplicationTitle = Observable.Return(title)
			.ToReadOnlyReactiveProperty(title)
			.AddTo(ref this.disposableBag);

		// ローディング状態を初期化
		this.isLoadingProperty.AddTo(ref this.disposableBag);
		this.loadingMessageProperty.AddTo(ref this.disposableBag);

		this.IsLoading = this.isLoadingProperty
			.ToReadOnlyReactiveProperty()
			.AddTo(ref this.disposableBag);

		this.LoadingMessage = this.loadingMessageProperty
			.ToReadOnlyReactiveProperty()
			.AddTo(ref this.disposableBag);

		// LoadingService のイベント購読
		this.loadingService.StateChanged += this.onLoadingServiceStateChanged;

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
				Content = "製本",
				Icon = new SymbolIcon { Symbol = SymbolRegular.Library24 },
				FontSize = 24,
				TargetPageType = typeof(StartPage),
			},
			new NavigationViewItem
			{
				Content = "作品管理",
				Icon = new SymbolIcon { Symbol = SymbolRegular.BookToolbox24 },
				FontSize = 24,
				TargetPageType = typeof(MaintenancePage),
			},
			new NavigationViewItem
			{
				Content = "タグ",
				Icon = new SymbolIcon { Symbol = SymbolRegular.Tag24 },
				FontSize = 24,
				TargetPageType = typeof(TagPage),
			},
		}
		.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
		.AddTo(ref this.disposableBag);

		this.FooterMenuItems = new ObservableList<NavigationViewItem>
		{
			new NavigationViewItem
			{
				Content = "設定",
				Icon = new SymbolIcon { Symbol = SymbolRegular.Wrench24 },
				FontSize = 24,
				TargetPageType = typeof(SettingsPage),
				Margin = new Thickness(0, 0, 0, 16),
			},
		}
		.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
		.AddTo(ref this.disposableBag);

		Application.Current.Dispatcher.BeginInvoke(() => this.navigationService.Navigate(typeof(HomePage)));
		this.logger.ZLogInformation($"MainWindowViewModel 初期化完了");
	}

	/// <summary>
	/// ナビゲーション遷移時に呼び出されます。
	/// 前ページに対して ISavable 保存と INavigationLeavingAware 退場処理を行い、
	/// その後、新ページに対して IDataInitializable 初期化処理を実行します。
	/// </summary>
	/// <param name="e">ナビゲーションイベント引数。</param>
	internal async ValueTask OnNavigated(NavigatedEventArgs e)
	{
		Debug.WriteLine("===== Navigated =====");
		Debug.WriteLine($"PageType={e.Page?.GetType().FullName}");
		Debug.WriteLine($"DataContext={((FrameworkElement)e.Page!).DataContext?.GetType().FullName}");

		// 1. 遷移先ページの ViewModel を取得
		var nextViewModel = ((FrameworkElement)e.Page!).DataContext;

		// 2. 前ページの保存処理
		await this.saveCurrentViewModelAsync();

		// 3. 遷移先が要求提供インターフェースを実装していれば要求を取得、否則既定値を使用
		var request =
			nextViewModel is INavigationLeavingRequestProvider provider
				? provider.GetNavigationLeavingRequest()
				: NavigationLeavingRequest.None;

		// 4. 前ページの退場処理
		if (this.currentViewModel is INavigationLeavingAware leavingAware)
		{
			Debug.WriteLine("OnNavigatingFromAsync CALL");
			await leavingAware.OnNavigatingFromAsync(request);
		}

		// 5. 新ページの ViewModel を currentViewModel に設定
		this.currentViewModel = nextViewModel;

		Debug.WriteLine($"CurrentViewModel={this.currentViewModel?.GetType().FullName}");

		// 6. 新ページの初期化処理
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

		using var scope = this.serviceScopeFactory.CreateScope();
		var bindingStoreRepository = scope.ServiceProvider.GetRequiredService<BindingStoreRepository>();
		var bindingQueueRepository = scope.ServiceProvider.GetRequiredService<BindingQueueRepository>();
		await bindingQueueRepository.SaveAsync(bindingStoreRepository.GetAll());

		// 作品一覧のスクロール位置を AppSettings へ反映してDB保存する
		this.appSettings.SeriesListVerticalOffset.Value = this.homePageViewModel.SavedSeriesListVerticalOffset.Value;
		var appSettingsService = scope.ServiceProvider.GetRequiredService<AppSettingsService>();
		await appSettingsService.SaveAppSettingsAsync();
	}

	/// <summary>
	/// <see cref="currentViewModel"/> が <see cref="ISavable"/> を実装している場合、保存を実行します。
	/// 失敗時のみスナックバーで通知します。
	/// </summary>
	private async ValueTask saveCurrentViewModelAsync()
	{
		if (this.currentViewModel is not ISavable savable)
			return;
					var result = await savable.SaveAsync();
		if (result.IsSuccess)
			return;

		var message = !string.IsNullOrEmpty(result.Message)
			? result.Message
			: "保存に失敗しました";

		this.snackbarService.Show(
			"設定保存",
			message,
			ControlAppearance.Danger,
			new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
			Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// LoadingService の状態変更イベントハンドラーです。
	/// R3 リアクティブプロパティへ状態を反映します。
	/// </summary>
	private void onLoadingServiceStateChanged(object? sender, LoadingStateChangedEventArgs e)
	{
		this.isLoadingProperty.Value = e.IsLoading;
		this.loadingMessageProperty.Value = e.Message;
	}

	/// <summary>
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		// LoadingService のイベント購読を解除
		this.loadingService.StateChanged -= this.onLoadingServiceStateChanged;

		this.disposableBag.Dispose();
	}
}
