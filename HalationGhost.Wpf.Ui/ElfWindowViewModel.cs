using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
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

	/// <summary>
	/// <see cref="ElfWindowViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	public ElfWindowViewModel(INavigationService navigationService)
	{
		this.NavigationService = navigationService;

		// NavigationView が利用可能になった時点で SetNavigationControl() を実行
		NavigationContext.ExecuteWhenAvailable(navigationView =>
		{
			System.Diagnostics.Debug.WriteLine(">>> ElfWindowViewModel NavigationContext.ExecuteWhenAvailable SetNavigationControl 処理実行");
			this.NavigationService.SetNavigationControl(navigationView);

			// NavigationView が利用可能になった時点で Navigated イベントを購読
			System.Diagnostics.Debug.WriteLine(">>> ElfWindowViewModel NavigationContext.ExecuteWhenAvailable Navigated イベント購読");
			navigationView.Navigated += this.OnNavigatedHandler;
		});
	}

	/// <summary>
	/// Navigated イベントのハンドラー。
	/// Singleton Page に対応する Transient ViewModel がある場合、DataContext を差し替えます。
	/// その後、派生クラスの OnNavigated メソッドを呼び出します。
	/// </summary>
	private void OnNavigatedHandler(INavigationView sender, NavigatedEventArgs args)
	{
		// 1. NavigatedEventArgs から Page インスタンスを取得
		if (args.Page == null)
		{
			System.Diagnostics.Debug.WriteLine(">>> ElfWindowViewModel OnNavigatedHandler Page が null です");
			this.OnNavigated(sender, args);
			return;
		}

		var page = (FrameworkElement)args.Page;
		var pageType = page.GetType();

		System.Diagnostics.Debug.WriteLine($">>> ElfWindowViewModel OnNavigatedHandler ナビゲーション開始。PageType: {pageType.FullName}");

		// 2. NavigationContext から登録情報を取得
		if (!NavigationContext.TryGetPageInfo(pageType, out var pageInfo))
		{
			System.Diagnostics.Debug.WriteLine($"<<< ElfWindowViewModel OnNavigatedHandler 登録情報が見つかりません。PageType: {pageType.FullName}");
			this.OnNavigated(sender, args);
			return;
		}

		System.Diagnostics.Debug.WriteLine($">>> ElfWindowViewModel OnNavigatedHandler 登録情報を取得しました。PageType: {pageInfo.PageType.FullName}, ViewModelType: {pageInfo.ViewModelType.FullName}, PageLifetime: {pageInfo.PageLifetime}, ViewModelLifetime: {pageInfo.ViewModelLifetime}");

		// 3. Singleton Page / Transient ViewModel の条件をチェック
		if (pageInfo.PageLifetime == ServiceLifetime.Singleton && pageInfo.ViewModelLifetime == ServiceLifetime.Transient)
		{
			System.Diagnostics.Debug.WriteLine($">>> ElfWindowViewModel OnNavigatedHandler Singleton Page / Transient ViewModel の条件に該当します");

			// 4. 前の ViewModel を取得
			var oldViewModel = page.DataContext;
			var oldViewModelType = oldViewModel?.GetType()?.FullName ?? "null";

			// 5. 新しい ViewModel を解決
			System.Diagnostics.Debug.WriteLine($">>> ElfWindowViewModel OnNavigatedHandler 新しい ViewModel を解決します。ViewModelType: {pageInfo.ViewModelType.FullName}");
			var newViewModel = this.ResolveViewModel(pageInfo.ViewModelType);

			// 6. 解決結果が null でないか、登録された型と互換性があるかを確認
			if (newViewModel == null)
			{
				throw new InvalidOperationException($"ViewModel の解決に失敗しました。ViewModelType: {pageInfo.ViewModelType.FullName}");
			}

			if (!pageInfo.ViewModelType.IsAssignableFrom(newViewModel.GetType()))
			{
				throw new InvalidOperationException($"解決された ViewModel の型が登録情報と互換性がありません。登録型: {pageInfo.ViewModelType.FullName}, 解決型: {newViewModel.GetType().FullName}");
			}

			// 7. DataContext を差し替え
			page.DataContext = newViewModel;
			var newViewModelType = newViewModel.GetType().FullName;
			System.Diagnostics.Debug.WriteLine($"<<< ElfWindowViewModel OnNavigatedHandler DataContext を差し替えました。旧: {oldViewModelType}, 新: {newViewModelType}");
		}
		else
		{
			System.Diagnostics.Debug.WriteLine($"<<< ElfWindowViewModel OnNavigatedHandler ライフタイム条件に該当しません。PageLifetime: {pageInfo.PageLifetime}, ViewModelLifetime: {pageInfo.ViewModelLifetime}");
		}

		// 8. 派生クラスの OnNavigated を呼び出し
		this.OnNavigated(sender, args);
	}

	/// <summary>
	/// ナビゲーション遷移時に呼び出されます。
	/// 派生クラスでオーバーライドして、ナビゲーションライフサイクル処理を実装してください。
	/// </summary>
	/// <param name="sender">NavigationView。</param>
	/// <param name="args">ナビゲーションイベント引数。</param>
	protected virtual void OnNavigated(INavigationView sender, NavigatedEventArgs args)
	{
	}

	/// <summary>
	/// 指定された ViewModel 型に対応するインスタンスをアプリケーション側から解決します。
	/// 派生クラスでオーバーライドして、DI コンテナから ViewModel を取得してください。
	/// </summary>
	/// <param name="viewModelType">解決する ViewModel の型。</param>
	/// <returns>解決した ViewModel のインスタンス。</returns>
	protected abstract object ResolveViewModel(Type viewModelType);
}

