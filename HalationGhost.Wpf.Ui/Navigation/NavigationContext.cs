using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace HalationGhost.Wpf.Ui.Navigation;

/// <summary>
/// ナビゲーション対象のページとそれに対応する ViewModel の登録情報を保持するレコード型です。
/// </summary>
/// <param name="PageType">ページの型。</param>
/// <param name="ViewModelType">ViewModel の型。</param>
/// <param name="PageLifetime">ページのライフタイム。</param>
/// <param name="ViewModelLifetime">ViewModel のライフタイム。</param>
internal record NavigationPageInfo(
	Type PageType,
	Type ViewModelType,
	ServiceLifetime PageLifetime,
	ServiceLifetime ViewModelLifetime);

/// <summary>
/// HalationGhost.Wpf.Ui 内部でナビゲーション関連情報を管理するための内部コンテキストクラスです。
/// このクラスはアプリケーション共通のナビゲーション情報を保持し、必要に応じて他の処理から参照できるようにします。
/// </summary>
internal static class NavigationContext
{
	/// <summary>現在登録されている NavigationView。</summary>
	private static INavigationView? currentNavigationView;

	/// <summary>NavigationView 利用可能時に実行する保留中の処理。</summary>
	private static Action<INavigationView>? pendingAction;

	/// <summary>Page型をキーとして、ナビゲーション登録情報を保持する辞書。</summary>
	private static readonly Dictionary<Type, NavigationPageInfo> pageInfoDictionary = [];

	/// <summary>現在登録されている SnackbarPresenter。</summary>
	private static SnackbarPresenter? currentSnackbarPresenter;

	/// <summary>現在登録されている ContentDialogHost。</summary>
	private static ContentDialogHost? currentContentDialogHost;

	/// <summary>WindowPlacement ファイルの完全パス。</summary>
	private static string? windowPlacementPath;

	/// <summary>戻る要求が有効かどうかを判定する関数。デフォルトは常に true を返します。</summary>
	private static Func<bool> backRequestEnabledChecker = () => true;

	/// <summary>現在表示中のページ ViewModel。</summary>
	private static object? currentPageViewModel;

	/// <summary>
	/// NavigationView を登録します。
	/// 登録時に保留中の処理があれば、その場で実行します。
	/// </summary>
	/// <param name="navigationView">登録する NavigationView。</param>
	internal static void Register(INavigationView navigationView)
	{
		currentNavigationView = navigationView;

		// 保留中の処理があれば実行
		if (pendingAction != null)
		{
			pendingAction.Invoke(navigationView);
			pendingAction = null;
		}
	}

	/// <summary>
	/// 登録済みの NavigationView を取得します。
	/// </summary>
	/// <returns>登録済みの NavigationView。</returns>
	/// <exception cref="InvalidOperationException">NavigationView が登録されていない場合。</exception>
	internal static INavigationView GetCurrent()
	{
		if (currentNavigationView == null)
		{
			throw new InvalidOperationException("NavigationView が登録されていません。ElfWindow が初期化される前にこのメソッドが呼ばれている可能性があります。");
		}

		return currentNavigationView;
	}

	/// <summary>
	/// NavigationView が利用可能になった時点で処理を実行します。
	/// NavigationView が既に登録済みの場合は即座に実行、未登録の場合は登録時に実行します。
	/// </summary>
	/// <param name="action">NavigationView が利用可能になった時点で実行する処理。</param>
	internal static void ExecuteWhenAvailable(Action<INavigationView> action)
	{
		if (currentNavigationView != null)
		{
			// NavigationView が既に登録済み → 即座に実行
			action.Invoke(currentNavigationView);
		}
		else
		{
			// NavigationView が未登録 → 処理を保留
			pendingAction = action;
		}
	}

	/// <summary>
	/// ナビゲーションページの登録情報を登録します。
	/// 同じ Page型が再度登録された場合は、最新の登録内容で置き換えます。
	/// </summary>
	/// <param name="pageType">ページの型。</param>
	/// <param name="info">登録する ナビゲーションページ情報。</param>
	internal static void RegisterPageInfo(Type pageType, NavigationPageInfo info)
	{
		pageInfoDictionary[pageType] = info;
	}

	/// <summary>
	/// ナビゲーションページの登録情報を取得します。
	/// </summary>
	/// <param name="pageType">ページの型。</param>
	/// <param name="info">取得した登録情報。見つからない場合は null。</param>
	/// <returns>登録情報が見つかった場合は true。見つからない場合は false。</returns>
	internal static bool TryGetPageInfo(Type pageType, out NavigationPageInfo? info)
	{
		return pageInfoDictionary.TryGetValue(pageType, out info);
	}

	/// <summary>
	/// SnackbarPresenter を登録します。
	/// </summary>
	/// <param name="snackbarPresenter">登録する SnackbarPresenter。</param>
	internal static void RegisterSnackbarPresenter(SnackbarPresenter snackbarPresenter)
	{
		currentSnackbarPresenter = snackbarPresenter;
	}

	/// <summary>
	/// 登録済みの SnackbarPresenter を取得します。
	/// </summary>
	/// <returns>登録済みの SnackbarPresenter。</returns>
	/// <exception cref="InvalidOperationException">SnackbarPresenter が登録されていない場合。</exception>
	internal static SnackbarPresenter GetSnackbarPresenter()
	{
		if (currentSnackbarPresenter == null)
		{
			throw new InvalidOperationException("SnackbarPresenter が登録されていません。ElfWindow が初期化される前にこのメソッドが呼ばれている可能性があります。");
		}

		return currentSnackbarPresenter;
	}

	/// <summary>
	/// ContentDialogHost を登録します。
	/// </summary>
	/// <param name="contentDialogHost">登録する ContentDialogHost。</param>
	internal static void RegisterContentDialogHost(ContentDialogHost contentDialogHost)
	{
		currentContentDialogHost = contentDialogHost;
	}

	/// <summary>
	/// 登録済みの ContentDialogHost を取得します。
	/// </summary>
	/// <returns>登録済みの ContentDialogHost。</returns>
	/// <exception cref="InvalidOperationException">ContentDialogHost が登録されていない場合。</exception>
	internal static ContentDialogHost GetContentDialogHost()
	{
		if (currentContentDialogHost == null)
		{
			throw new InvalidOperationException("ContentDialogHost が登録されていません。ElfWindow が初期化される前にこのメソッドが呼ばれている可能性があります。");
		}

		return currentContentDialogHost;
	}

	/// <summary>
	/// WindowPlacement ファイルの完全パスを登録します。
	/// </summary>
	/// <param name="path">登録する完全パス。</param>
	internal static void RegisterWindowPlacementPath(string path)
	{
		windowPlacementPath = path;
	}

	/// <summary>
	/// 登録済みの WindowPlacement ファイルの完全パスを取得します。
	/// </summary>
	/// <returns>登録済みの WindowPlacement ファイルの完全パス。</returns>
	/// <exception cref="InvalidOperationException">WindowPlacement パスが登録されていない場合。</exception>
	internal static string GetWindowPlacementPath()
	{
		if (windowPlacementPath == null)
		{
			throw new InvalidOperationException("WindowPlacement パスが登録されていません。ElfWindowViewModel が初期化される前にこのメソッドが呼ばれている可能性があります。");
		}

		return windowPlacementPath;
	}

	/// <summary>
	/// 戻る要求が有効かどうかを判定する関数を登録します。
	/// 登録されていない場合のデフォルト動作は常に true を返します。
	/// </summary>
	/// <param name="checker">戻る要求が有効かどうかを判定する関数。</param>
	internal static void RegisterBackRequestEnabledChecker(Func<bool> checker)
	{
		backRequestEnabledChecker = checker ?? (() => true);
	}

	/// <summary>
	/// 戻る要求が現在有効かどうかを取得します。
	/// </summary>
	/// <returns>戻る要求が有効な場合は true、無効な場合は false。</returns>
	internal static bool IsBackRequestEnabled()
	{
		return backRequestEnabledChecker.Invoke();
	}

	/// <summary>
	/// 現在表示中のページ ViewModel を登録します。
	/// ナビゲーション完了時に ElfWindowViewModel から呼び出されます。
	/// </summary>
	/// <param name="pageViewModel">登録するページ ViewModel。</param>
	internal static void RegisterCurrentPageViewModel(object? pageViewModel)
	{
		currentPageViewModel = pageViewModel;
	}

	/// <summary>
	/// 現在表示中のページ ViewModel を取得します。
	/// マウスサイドボタンなど、入力デバイスからの戻る要求を処理する際に使用されます。
	/// </summary>
	/// <returns>現在表示中のページ ViewModel。登録されていない場合は null。</returns>
	internal static object? GetCurrentPageViewModel()
	{
		return currentPageViewModel;
	}
}

