using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HalationGhost.Wpf.Ui.Navigation;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace HalationGhost.Wpf.Ui;

/// <summary>
/// ウィンドウ位置・サイズ保存機能を備えた共通基底ウィンドウクラス。
/// FluentWindow を拡張し、ウィンドウの位置・サイズ・最大化状態を自動的に保存・復元します。
/// INavigationWindow インターフェースを実装し、ナビゲーション機能を提供します。
/// </summary>
public class ElfWindow : FluentWindow, INavigationWindow
{
	private readonly WindowPlacementRepository repository = new();
	private string windowPlacementFilePath = string.Empty;
	private INavigationView? navigationView;
	private INavigationViewPageProvider? navigationViewPageProvider;
	private IServiceProvider? serviceProvider;

	/// <summary>
	/// <see cref="ElfWindow"/> の新しいインスタンスを初期化します。
	/// </summary>
	public ElfWindow()
	{
		this.Initialized += this.ElfWindow_Initialized;
		this.Loaded += this.ElfWindow_Loaded;
		this.ContentRendered += this.ElfWindow_ContentRendered;
		this.PreviewMouseDown += this.ElfWindow_PreviewMouseDown;
	}

	/// <summary>
	/// Initialized イベントハンドラー。
	/// </summary>
	private void ElfWindow_Initialized(object? sender, EventArgs e)
	{
	}

	/// <summary>
	/// Loaded イベントハンドラー。
	/// </summary>
	private void ElfWindow_Loaded(object? sender, RoutedEventArgs e)
	{
		this.FindAndSetNavigationView();
	}

	/// <summary>
	/// VisualTree から NavigationView、SnackbarPresenter、ContentDialogHost を検索し、
	/// NavigationContext へ登録します。
	/// 登録順序を制御して、すべてのコントロールが登録済みになった状態で
	/// NavigationContext.Register() を実行し、ElfWindowViewModel の初期化処理を開始します。
	/// 取得後、保持されている INavigationViewPageProvider と IServiceProvider を適用します。
	/// </summary>
	private void FindAndSetNavigationView()
	{
		// 1. VisualTree から各コントロールを検索
		var navigationView = this.FindInVisualTree<INavigationView>(this);
		if (navigationView == null)
		{
			throw new InvalidOperationException("NavigationView が見つかりません。ElfWindow の派生クラスは VisualTree に INavigationView を配置してください。");
		}

		var snackbarPresenter = this.FindInVisualTree<SnackbarPresenter>(this);
		if (snackbarPresenter == null)
		{
			throw new InvalidOperationException("SnackbarPresenter が見つかりません。ElfWindow の派生クラスは VisualTree に SnackbarPresenter を配置してください。");
		}

		var contentDialogHost = this.FindInVisualTree<ContentDialogHost>(this);
		if (contentDialogHost == null)
		{
			throw new InvalidOperationException("ContentDialogHost が見つかりません。ElfWindow の派生クラスは VisualTree に ContentDialogHost を配置してください。");
		}

		// 2. SnackbarPresenter を NavigationContext へ登録
		NavigationContext.RegisterSnackbarPresenter(snackbarPresenter);

		// 3. ContentDialogHost を NavigationContext へ登録
		NavigationContext.RegisterContentDialogHost(contentDialogHost);

		// 4. INavigationView を NavigationContext へ登録（最後に実行）
		// この時点で保留中の ElfWindowViewModel.ExecuteWhenAvailable() 処理が実行される
		this.navigationView = navigationView;
		NavigationContext.Register(this.navigationView);

		// 5. 保持されている NavigationViewPageProvider を適用（SetPageService が呼ばれていた場合）
		if (this.navigationViewPageProvider != null)
		{
			this.navigationView.SetPageProviderService(this.navigationViewPageProvider);
		}

		// 6. 保持されている ServiceProvider を適用（SetServiceProvider が呼ばれていた場合）
		if (this.serviceProvider != null)
		{
			this.navigationView.SetServiceProvider(this.serviceProvider);
		}
	}

	/// <summary>
	/// VisualTree から指定された型の要素を再帰的に検索します。
	/// </summary>
	/// <typeparam name="T">検索する型。</typeparam>
	/// <param name="parent">検索開始要素。</param>
	/// <returns>見つかった要素、見つからない場合は null。</returns>
	private T? FindInVisualTree<T>(DependencyObject parent) where T : class
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);

			if (child is T result)
			{
				return result;
			}

			var found = this.FindInVisualTree<T>(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	/// <summary>
	/// ContentRendered イベントハンドラー。
	/// </summary>
	private void ElfWindow_ContentRendered(object? sender, EventArgs e)
	{
	}

	/// <summary>
	/// マウスサイドボタン（戻るボタン）を検知して戻る要求を処理します。
	/// </summary>
	private async void ElfWindow_PreviewMouseDown(object? sender, MouseButtonEventArgs e)
	{
		// マウスサイドボタン(XButton1)を検知
		if (e.XButton1 == MouseButtonState.Pressed)
		{
			// 戻る要求が無効な場合は何もしない
			if (!NavigationContext.IsBackRequestEnabled())
			{
				return;
			}

			// NavigationContext から現在のページ ViewModel を取得
			var pageViewModel = NavigationContext.GetCurrentPageViewModel();
			if (pageViewModel is not IBackRequestHandler backRequestHandler)
			{
				// IBackRequestHandler を実装していない場合は何もしない
				return;
			}

			// OnBackRequestedAsync を呼び出す
			await backRequestHandler.OnBackRequestedAsync();
			e.Handled = true;
		}
	}

	/// <summary>
	/// ナビゲーションコントロールを返します。
	/// </summary>
	/// <returns>ウィンドウに配置された <see cref="INavigationView"/>。</returns>
	INavigationView INavigationWindow.GetNavigation() => this.navigationView ?? throw new InvalidOperationException("NavigationView が初期化されていません。");

	/// <summary>
	/// 指定されたページ型へナビゲートします。
	/// </summary>
	/// <param name="pageType">ナビゲート先のページ型。</param>
	/// <returns>ナビゲーションが成功した場合は <c>true</c>。</returns>
	bool INavigationWindow.Navigate(Type pageType)
	{
		if (this.navigationView == null)
		{
			throw new InvalidOperationException("NavigationView が初期化されていません。");
		}

		return this.navigationView.Navigate(pageType);
	}

	/// <summary>
	/// DI サービスプロバイダーを設定します。
	/// NavigationView が取得済みならその場で適用し、未取得なら Loaded 後に適用します。
	/// </summary>
	/// <param name="serviceProvider">設定するサービスプロバイダー。</param>
	void INavigationWindow.SetServiceProvider(IServiceProvider serviceProvider)
	{
		this.serviceProvider = serviceProvider;

		if (this.navigationView != null)
		{
			this.navigationView.SetServiceProvider(serviceProvider);
		}
	}

	/// <summary>
	/// ウィンドウを表示します。
	/// </summary>
	void INavigationWindow.ShowWindow() => this.Show();

	/// <summary>
	/// ウィンドウを閉じます。
	/// </summary>
	void INavigationWindow.CloseWindow() => this.Close();

	/// <summary>
	/// ページプロバイダーサービスを設定します。
	/// NavigationView が取得済みならその場で適用し、未取得なら Loaded 後に適用します。
	/// </summary>
	/// <param name="navigationViewPageProvider">設定するページプロバイダー。</param>
	public void SetPageService(INavigationViewPageProvider navigationViewPageProvider)
	{
		this.navigationViewPageProvider = navigationViewPageProvider;

		if (this.navigationView != null)
		{
			this.navigationView.SetPageProviderService(navigationViewPageProvider);
		}
	}

	/// <summary>
	/// ウィンドウ位置・サイズの保存と復元を有効にするかどうかを取得または設定します。
	/// </summary>
	[Category("Elf Window")]
	[Description("ウィンドウ位置・サイズの保存と復元を有効にします。")]
	[DefaultValue(true)]
	public bool IsWindowPlacementEnabled { get; init; } = true;

	/// <summary>
	/// ウィンドウ位置・サイズ保存ファイルの保存先を設定します。
	/// </summary>
	/// <remarks>
	/// <para>
	/// このメソッドをコンストラクタで呼び出してください。
	/// </para>
	/// <para>
	/// filePath が空文字の場合は保存・復元機能は無効になります。
	/// </para>
	/// </remarks>
	/// <param name="filePath">保存先ファイルパス。空文字の場合は機能を無効にします。</param>
	protected void ConfigureWindowPlacement(string filePath)
	{
		this.windowPlacementFilePath = filePath;
	}

	/// <summary>
	/// ウィンドウの初期化が完了したときに呼ばれます。
	/// ここでウィンドウ位置・サイズの復元を行います。
	/// </summary>
	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);

		// WindowPlacement ファイルパスを NavigationContext から取得し、設定を反映
		var windowPlacementPath = NavigationContext.GetWindowPlacementPath();
		this.ConfigureWindowPlacement(windowPlacementPath);

		if (!this.IsWindowPlacementEnabled || string.IsNullOrEmpty(this.windowPlacementFilePath))
		{
			return;
		}

		this.RestoreWindowPlacement();
	}

	/// <summary>
	/// ウィンドウが閉じられるときに呼ばれます。
	/// ここでウィンドウ位置・サイズの保存を行います。
	/// </summary>
	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);

		if (!this.IsWindowPlacementEnabled || string.IsNullOrEmpty(this.windowPlacementFilePath))
		{
			return;
		}

		this.SaveWindowPlacement();
	}

	/// <summary>
	/// ウィンドウ位置・サイズ情報を保存ファイルから復元します。
	/// 復元ファイルが存在しない場合や破損している場合は何もしません。
	/// </summary>
	private void RestoreWindowPlacement()
	{
		try
		{
			var placement = this.repository.Load(this.windowPlacementFilePath);
			if (placement == null)
			{
				// ファイルが存在しない = 初回起動 = 正常系
				return;
			}

			// ウィンドウの位置・サイズを復元
			this.Left = placement.Left;
			this.Top = placement.Top;
			this.Width = placement.Width;
			this.Height = placement.Height;

			// ウィンドウの状態を復元
			// Maximized の場合は最後に状態を設定する
			if (placement.WindowState == WindowState.Maximized)
			{
				this.WindowState = WindowState.Maximized;
			}
			else if (placement.WindowState == WindowState.Normal)
			{
				this.WindowState = WindowState.Normal;
			}
			// Minimized は起動時の状態として復元しない
		}
		catch
		{
			// 復元失敗（ファイル破損など）= 初回起動扱い = 何もしない
		}
	}

	/// <summary>
	/// ウィンドウの現在の位置・サイズ情報を保存ファイルに保存します。
	/// 保存に失敗した場合は何もしません。
	/// </summary>
	private void SaveWindowPlacement()
	{
		try
		{
			var placement = new WindowPlacement
			{
				Left = this.Left,
				Top = this.Top,
				Width = this.Width,
				Height = this.Height,
				WindowState = this.WindowState
			};

			// Maximized 状態の場合は RestoreBounds を保存する
			if (this.WindowState == WindowState.Maximized && this.RestoreBounds != default)
			{
				placement.Left = this.RestoreBounds.Left;
				placement.Top = this.RestoreBounds.Top;
				placement.Width = this.RestoreBounds.Width;
				placement.Height = this.RestoreBounds.Height;
				placement.WindowState = WindowState.Maximized;
			}
			// Minimized 状態の場合は RestoreBounds を Normal として保存する
			else if (this.WindowState == WindowState.Minimized && this.RestoreBounds != default)
			{
				placement.Left = this.RestoreBounds.Left;
				placement.Top = this.RestoreBounds.Top;
				placement.Width = this.RestoreBounds.Width;
				placement.Height = this.RestoreBounds.Height;
				placement.WindowState = WindowState.Normal;
			}

			this.repository.Save(placement, this.windowPlacementFilePath);
		}
		catch
		{
			// 保存失敗 = 次回起動時は復元されない = 許容範囲
		}
	}
}
