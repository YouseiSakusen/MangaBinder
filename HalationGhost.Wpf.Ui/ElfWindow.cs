using System.ComponentModel;
using System.Windows;
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
		System.Diagnostics.Debug.WriteLine(">>> ElfWindow コンストラクタ開始");
		this.Initialized += this.ElfWindow_Initialized;
		this.Loaded += this.ElfWindow_Loaded;
		this.ContentRendered += this.ElfWindow_ContentRendered;
		System.Diagnostics.Debug.WriteLine("<<< ElfWindow コンストラクタ終了");
	}

	/// <summary>
	/// Initialized イベントハンドラー。
	/// </summary>
	private void ElfWindow_Initialized(object? sender, EventArgs e)
	{
		System.Diagnostics.Debug.WriteLine(">>> ElfWindow Initialized");
	}

	/// <summary>
	/// Loaded イベントハンドラー。
	/// </summary>
	private void ElfWindow_Loaded(object? sender, RoutedEventArgs e)
	{
		System.Diagnostics.Debug.WriteLine(">>> ElfWindow Loaded");
		this.FindAndSetNavigationView();
	}

	/// <summary>
	/// VisualTree から NavigationView を検索し、内部フィールドへ保持します。
	/// 取得後、保持されている INavigationViewPageProvider と IServiceProvider を適用します。
	/// NavigationContext へも登録します。
	/// </summary>
	private void FindAndSetNavigationView()
	{
		this.navigationView = this.FindNavigationViewInVisualTree(this);

		if (this.navigationView == null)
		{
			throw new InvalidOperationException("NavigationView が見つかりません。ElfWindow の派生クラスは VisualTree に INavigationView を配置してください。");
		}

		System.Diagnostics.Debug.WriteLine(">>> ElfWindow NavigationView を検出しました");

		// NavigationContext へ登録
		NavigationContext.Register(this.navigationView);
		System.Diagnostics.Debug.WriteLine(">>> ElfWindow NavigationContext へ登録完了");

		// 保持されている NavigationViewPageProvider を適用（SetPageService が呼ばれていた場合）
		if (this.navigationViewPageProvider != null)
		{
			System.Diagnostics.Debug.WriteLine(">>> ElfWindow SetPageProviderService を遅延適用");
			this.navigationView.SetPageProviderService(this.navigationViewPageProvider);
		}

		// 保持されている ServiceProvider を適用（SetServiceProvider が呼ばれていた場合）
		if (this.serviceProvider != null)
		{
			System.Diagnostics.Debug.WriteLine(">>> ElfWindow SetServiceProvider を遅延適用");
			this.navigationView.SetServiceProvider(this.serviceProvider);
		}
	}

	/// <summary>
	/// VisualTree から INavigationView を再帰的に検索します。
	/// </summary>
	/// <param name="parent">検索開始要素。</param>
	/// <returns>見つかった INavigationView、見つからない場合は null。</returns>
	private INavigationView? FindNavigationViewInVisualTree(DependencyObject parent)
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);

			if (child is INavigationView navigationView)
			{
				return navigationView;
			}

			var result = this.FindNavigationViewInVisualTree(child);
			if (result != null)
			{
				return result;
			}
		}

		return null;
	}

	/// <summary>
	/// ContentRendered イベントハンドラー。
	/// </summary>
	private void ElfWindow_ContentRendered(object? sender, EventArgs e)
	{
		System.Diagnostics.Debug.WriteLine(">>> ElfWindow ContentRendered");
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
