using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using HalationGhost.Wpf.Ui.Navigation;

namespace HalationGhost.Wpf.Ui;

/// <summary>
/// ウィンドウ用 ViewModel の基底クラスです。
/// ナビゲーション制御の関連付けを行い、ウィンドウの初期化をサポートします。
/// </summary>
public abstract class ElfWindowViewModel
{
	/// <summary>ナビゲーションサービス。</summary>
	protected INavigationService NavigationService { get; }

	/// <summary>スナックバーサービス。</summary>
	protected ISnackbarService SnackbarService { get; }

	/// <summary>コンテントダイアログサービス。</summary>
	protected IContentDialogService ContentDialogService { get; }

	/// <summary>現在表示中のページの ViewModel を保持するフィールドです。ナビゲーションライフサイクル管理に使用します。</summary>
	private object? currentViewModel;

	/// <summary>現在表示中のページの ViewModel を取得します。</summary>
	protected object? CurrentViewModel => this.currentViewModel;

	/// <summary>
	/// <see cref="ElfWindowViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="navigationViewPageProvider">ページプロバイダーサービス。</param>
	/// <param name="applicationName">アプリケーション名。</param>
	/// <param name="windowPlacementFileName">WindowPlacement ファイル名。</param>
	public ElfWindowViewModel(
		INavigationService navigationService,
		ISnackbarService snackbarService,
		IContentDialogService contentDialogService,
		INavigationViewPageProvider navigationViewPageProvider,
		string applicationName,
		string windowPlacementFileName)
	{
		this.NavigationService = navigationService;
		this.SnackbarService = snackbarService;
		this.ContentDialogService = contentDialogService;

		// WindowPlacement ファイルの保存先パスを生成し、NavigationContext に登録
		var directory = System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"HalationGhost",
			applicationName);
		System.IO.Directory.CreateDirectory(directory);
		var windowPlacementPath = System.IO.Path.Combine(directory, windowPlacementFileName);
		NavigationContext.RegisterWindowPlacementPath(windowPlacementPath);

		// NavigationView が利用可能になった時点で初期化処理を実行
		NavigationContext.ExecuteWhenAvailable(navigationView =>
		{
			this.NavigationService.SetNavigationControl(navigationView);

			// NavigationView が利用可能になった時点で Navigated イベントを購読
			navigationView.Navigated += this.OnNavigatedHandler;

			// SnackbarPresenter を SnackbarService に設定
			var snackbarPresenter = NavigationContext.GetSnackbarPresenter();
			this.SnackbarService.SetSnackbarPresenter(snackbarPresenter);

			// ContentDialogHost を ContentDialogService に設定
			var contentDialogHost = NavigationContext.GetContentDialogHost();
			this.ContentDialogService.SetDialogHost(contentDialogHost);

			// INavigationViewPageProvider を NavigationView に設定
			navigationView.SetPageProviderService(navigationViewPageProvider);
		});
	}

	/// <summary>
	/// Navigated イベントのハンドラー。
	/// テンプレートメソッドパターンに従い、ナビゲーションライフサイクルを一元管理します。
	/// Singleton Page に対応する Transient ViewModel がある場合、DataContext を差し替えます。
	/// </summary>
	private async void OnNavigatedHandler(INavigationView sender, NavigatedEventArgs args)
	{
		// 1. NavigatedEventArgs から Page インスタンスを取得
		if (args.Page == null)
		{
			return;
		}

		var page = (FrameworkElement)args.Page;
		var pageType = page.GetType();

		// 2. NavigationContext から登録情報を取得
		if (!NavigationContext.TryGetPageInfo(pageType, out var pageInfo))
		{
			return;
		}

		// 3. 遷移先 Page の DataContext を確定
		// Singleton Page / Transient ViewModel の条件をチェック
		object? nextViewModel = page.DataContext;
		if (pageInfo.PageLifetime == ServiceLifetime.Singleton && pageInfo.ViewModelLifetime == ServiceLifetime.Transient)
		{

			// 前の ViewModel を取得
			var oldViewModel = page.DataContext;
			var oldViewModelType = oldViewModel?.GetType()?.FullName ?? "null";

			// 新しい ViewModel を解決
			var newViewModel = this.ResolveViewModel(pageInfo.ViewModelType);

			// 解決結果が null でないか、登録された型と互換性があるかを確認
			if (newViewModel == null)
			{
				throw new InvalidOperationException($"ViewModel の解決に失敗しました。ViewModelType: {pageInfo.ViewModelType.FullName}");
			}

			if (!pageInfo.ViewModelType.IsAssignableFrom(newViewModel.GetType()))
			{
				throw new InvalidOperationException($"解決された ViewModel の型が登録情報と互換性がありません。登録型: {pageInfo.ViewModelType.FullName}, 解決型: {newViewModel.GetType().FullName}");
			}

			// DataContext を差し替え
			page.DataContext = newViewModel;
			nextViewModel = newViewModel;
		}
		else
		{
		}

		// 4. 遷移元 ViewModel を previousViewModel として退避
		var previousViewModel = this.currentViewModel;

		// 5. 派生クラスの遷移前処理を呼び出し
		await this.OnNavigatingFromAsync(previousViewModel, nextViewModel);

		// 6. currentViewModel を nextViewModel に更新
		this.currentViewModel = nextViewModel;

		// 7. 遷移元 ViewModel が INavigationDisposable を実装している場合、Dispose を遅延実行
		// 同一インスタンスの場合は Dispose しない
		if (previousViewModel is INavigationDisposable disposable && previousViewModel != nextViewModel)
		{
			// WPF のナビゲーション処理および VisualTree の取り外しが完了した後に Dispose を実行するため、
			// Dispatcher の優先度を DispatcherPriority.ApplicationIdle に設定
			await Application.Current.Dispatcher.BeginInvoke(
				DispatcherPriority.ApplicationIdle,
				() =>
				{
					disposable.Dispose();
				});
		}

		// 8. 派生クラスの遷移後処理を呼び出し
		await this.OnNavigatedToAsync(nextViewModel);

		// 9. NavigationContext に現在のページ ViewModel を登録
		NavigationContext.RegisterCurrentPageViewModel(nextViewModel);
	}

	/// <summary>
	/// ナビゲーション遷移元 ViewModel から離れるときに派生クラスで実装する処理を呼び出します。
	/// 前ページの保存、退場処理などを行う際にオーバーライドしてください。
	/// </summary>
	/// <param name="previousViewModel">遷移元ページの ViewModel。null の場合があります。</param>
	/// <param name="nextViewModel">遷移先ページの ViewModel。</param>
	/// <returns>完了を表す <see cref="ValueTask"/>。</returns>
	protected virtual ValueTask OnNavigatingFromAsync(object? previousViewModel, object? nextViewModel)
	{
		return default;
	}

	/// <summary>
	/// ナビゲーション遷移後に派生クラスで実装する処理を呼び出します。
	/// 新ページの初期化処理などを行う際にオーバーライドしてください。
	/// </summary>
	/// <param name="nextViewModel">遷移先ページの ViewModel。</param>
	/// <returns>完了を表す <see cref="ValueTask"/>。</returns>
	protected virtual ValueTask OnNavigatedToAsync(object? nextViewModel)
	{
		return default;
	}

	/// <summary>
	/// 指定された ViewModel 型に対応するインスタンスをアプリケーション側から解決します。
	/// 派生クラスでオーバーライドして、DI コンテナから ViewModel を取得してください。
	/// </summary>
	/// <param name="viewModelType">解決する ViewModel の型。</param>
	/// <returns>解決した ViewModel のインスタンス。</returns>
	protected abstract object ResolveViewModel(Type viewModelType);
}

