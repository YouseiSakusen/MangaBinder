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
			System.Diagnostics.Debug.WriteLine(">>> NavigationContext 保留中の処理を実行します");
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
			System.Diagnostics.Debug.WriteLine(">>> NavigationContext NavigationView が登録済み、処理を即座に実行します");
			action.Invoke(currentNavigationView);
		}
		else
		{
			// NavigationView が未登録 → 処理を保留
			System.Diagnostics.Debug.WriteLine(">>> NavigationContext NavigationView が未登録、処理を保留します");
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
}
