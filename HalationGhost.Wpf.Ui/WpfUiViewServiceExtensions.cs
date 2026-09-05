using System.Windows;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using HalationGhost.Wpf.Ui.Navigation;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.DependencyInjection;
using System.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> に対する WPF UI ビュー登録拡張メソッドを提供します。
/// </summary>
public static class WpfUiViewServiceExtensions
{
	/// <summary>
	/// ナビゲーション対象のメインウィンドウを Singleton として登録します。
	/// <typeparamref name="TView"/> のコンストラクタ引数は DI コンテナが自動解決します。
	/// <typeparamref name="TViewModel"/> が <see cref="IWindowClosingAware"/> を実装している場合、
	/// ウィンドウの Closing イベントに <see cref="IWindowClosingAware.OnClosingAsync"/> を自動配管します。
	/// </summary>
	/// <typeparam name="TView">
	/// 登録するウィンドウの型。<see cref="FluentWindow"/> を継承すること。
	/// </typeparam>
	/// <typeparam name="TViewModel">対応する ViewModel の型。参照型であること。</typeparam>
	/// <param name="services">サービスコレクション。</param>
	/// <returns>サービスコレクション（メソッドチェーン用）。</returns>
	public static IServiceCollection AddNavigationWindow<TView, TViewModel>(
		this IServiceCollection services)
		where TView : FluentWindow
		where TViewModel : class
	{
		services.AddNavigationViewPageProvider();
		services.AddSingleton<INavigationService, NavigationService>();
		services.AddSingleton<TViewModel>();
		services.AddSingleton<TView>(sp =>
		{
			var view = ActivatorUtilities.CreateInstance<TView>(sp);
			var viewModel = sp.GetRequiredService<TViewModel>();

			// DataContext を設定
			view.DataContext = viewModel;

			if (viewModel is IWindowClosingAware closingAware)
				view.Closing += async (_, e) => await closingAware.OnClosingAsync(e);

			return view;
		});

		return services;
	}

	/// <summary>
	/// ナビゲーション対象のページを Transient として登録します。
	/// DataContext には <typeparamref name="TViewModel"/> が DI コンテナから注入されます。
	/// </summary>
	/// <typeparam name="TView">
	/// 登録するページの型。<see cref="FrameworkElement"/> を継承し、引数なしコンストラクタを持つこと。
	/// </typeparam>
	/// <typeparam name="TViewModel">対応する ViewModel の型。参照型であること。</typeparam>
	/// <param name="services">サービスコレクション。</param>
	/// <returns>サービスコレクション（メソッドチェーン用）。</returns>
	public static IServiceCollection AddNavigationPage<TView, TViewModel>(
		this IServiceCollection services)
		where TView : FrameworkElement, new()
		where TViewModel : class
	{
		services.AddTransient<TViewModel>();
		services.AddTransient<TView>(sp =>
		{
			var view = new TView();
			view.DataContext = sp.GetRequiredService<TViewModel>();
			return view;
		});

		// NavigationContext へ登録情報を登録
		NavigationContext.RegisterPageInfo(
			typeof(TView),
			new NavigationPageInfo(
				PageType: typeof(TView),
				ViewModelType: typeof(TViewModel),
				PageLifetime: ServiceLifetime.Transient,
				ViewModelLifetime: ServiceLifetime.Transient));

		Debug.WriteLine($">>> WpfUiViewServiceExtensions AddNavigationPage ページを登録しました。PageType: {typeof(TView).FullName}, ViewModelType: {typeof(TViewModel).FullName}, PageLifetime: Transient, ViewModelLifetime: Transient");

		return services;
	}

	/// <summary>
	/// ナビゲーション対象のページを登録します。
	/// <typeparamref name="TView"/> は Transient、<typeparamref name="TViewModel"/> は Singleton として登録されます。
	/// DataContext には Singleton の <typeparamref name="TViewModel"/> が DI コンテナから注入されます。
	/// スクロール位置・検索条件など、ページをまたいで状態を保持したい場合に使用してください。
	/// </summary>
	/// <typeparam name="TView">
	/// 登録するページの型。<see cref="FrameworkElement"/> を継承し、引数なしコンストラクタを持つこと。
	/// </typeparam>
	/// <typeparam name="TViewModel">対応する ViewModel の型。参照型であること。</typeparam>
	/// <param name="services">サービスコレクション。</param>
	/// <returns>サービスコレクション（メソッドチェーン用）。</returns>
	public static IServiceCollection AddNavigationPageWithSingletonViewModel<TView, TViewModel>(
		this IServiceCollection services)
		where TView : FrameworkElement, new()
		where TViewModel : class
	{
		services.AddSingleton<TViewModel>();
		services.AddTransient<TView>(sp =>
		{
			var view = new TView();
			view.DataContext = sp.GetRequiredService<TViewModel>();
			return view;
		});

		// NavigationContext へ登録情報を登録
		NavigationContext.RegisterPageInfo(
			typeof(TView),
			new NavigationPageInfo(
				PageType: typeof(TView),
				ViewModelType: typeof(TViewModel),
				PageLifetime: ServiceLifetime.Transient,
				ViewModelLifetime: ServiceLifetime.Singleton));

		Debug.WriteLine($">>> WpfUiViewServiceExtensions AddNavigationPageWithSingletonViewModel ページを登録しました。PageType: {typeof(TView).FullName}, ViewModelType: {typeof(TViewModel).FullName}, PageLifetime: Transient, ViewModelLifetime: Singleton");

		return services;
	}

	/// <summary>
	/// ナビゲーション対象のページを Singleton として登録します。
	/// <typeparamref name="TView"/> は常に Singleton として登録され、<typeparamref name="TViewModel"/> は指定された
	/// <paramref name="viewModelLifetime"/> に従って登録されます。
	/// DataContext は設定されず、ナビゲーション時に <see cref="ElfWindowViewModel.OnNavigatedHandler"/> で
	/// 現在の <typeparamref name="TViewModel"/> インスタンスが設定されます。
	/// </summary>
	/// <typeparam name="TView">
	/// 登録するページの型。<see cref="FrameworkElement"/> を継承し、引数なしコンストラクタを持つこと。
	/// </typeparam>
	/// <typeparam name="TViewModel">対応する ViewModel の型。参照型であること。</typeparam>
	/// <param name="services">サービスコレクション。</param>
	/// <param name="viewModelLifetime">
	/// ViewModel のライフサイクル。<see cref="ServiceLifetime.Singleton"/> または
	/// <see cref="ServiceLifetime.Transient"/> のみ許可。<see cref="ServiceLifetime.Scoped"/> が指定された場合は
	/// <see cref="ArgumentException"/> をスローします。
	/// </param>
	/// <returns>サービスコレクション（メソッドチェーン用）。</returns>
	/// <exception cref="ArgumentException"><paramref name="viewModelLifetime"/> が Scoped の場合。</exception>
	public static IServiceCollection AddNavigationPage<TView, TViewModel>(
		this IServiceCollection services,
		ServiceLifetime viewModelLifetime)
		where TView : FrameworkElement, new()
		where TViewModel : class
	{
		// Scoped は許可しない
		if (viewModelLifetime == ServiceLifetime.Scoped)
		{
			throw new ArgumentException(
				$"ナビゲーション画面の ViewModel ライフサイクルは Singleton または Transient のみ許可されます。Scoped は指定できません。 PageType: {typeof(TView).FullName}, ViewModelType: {typeof(TViewModel).FullName}",
				nameof(viewModelLifetime));
		}

		// ViewModel をライフサイクル指定で登録
		if (viewModelLifetime == ServiceLifetime.Singleton)
		{
			services.AddSingleton<TViewModel>();
			// Singleton ViewModel の場合は Page 生成時に DataContext を設定
			services.AddSingleton<TView>(serviceProvider =>
			{
				var page = new TView();
				page.DataContext = serviceProvider.GetRequiredService<TViewModel>();
				return page;
			});
		}
		else
		{
			services.AddTransient<TViewModel>();
			// Transient ViewModel の場合は Page だけを Singleton 登録（DataContext は OnNavigatedHandler で設定）
			services.AddSingleton<TView>();
		}

		// NavigationContext へ登録情報を登録
		NavigationContext.RegisterPageInfo(
			typeof(TView),
			new NavigationPageInfo(
				PageType: typeof(TView),
				ViewModelType: typeof(TViewModel),
				PageLifetime: ServiceLifetime.Singleton,
				ViewModelLifetime: viewModelLifetime));

		Debug.WriteLine($">>> WpfUiViewServiceExtensions AddNavigationPage ページを登録しました。PageType: {typeof(TView).FullName}, ViewModelType: {typeof(TViewModel).FullName}, PageLifetime: Singleton, ViewModelLifetime: {viewModelLifetime}");

		return services;
	}

	/// <summary>
	/// ナビゲーション対象のページを Singleton として登録します。
	/// <typeparamref name="TView"/> は Singleton、<typeparamref name="TViewModel"/> は Transient として登録されます。
	/// DataContext は設定されず、ナビゲーション時に <see cref="ElfWindowViewModel.OnNavigatedHandler"/> で新しい <typeparamref name="TViewModel"/> が設定されます。
	/// ナビゲーションのたびに新しい ViewModel インスタンスが生成され、DataContext へ設定されます。
	/// </summary>
	/// <typeparam name="TView">
	/// 登録するページの型。<see cref="FrameworkElement"/> を継承し、引数なしコンストラクタを持つこと。
	/// </typeparam>
	/// <typeparam name="TViewModel">対応する ViewModel の型。参照型であること。</typeparam>
	/// <param name="services">サービスコレクション。</param>
	/// <returns>サービスコレクション（メソッドチェーン用）。</returns>
	[Obsolete("AddNavigationPage(ServiceLifetime) を使用してください。")]
	public static IServiceCollection AddNavigationPageWithSingletonView<TView, TViewModel>(
		this IServiceCollection services)
		where TView : FrameworkElement, new()
		where TViewModel : class
	{
		services.AddTransient<TViewModel>();
		services.AddSingleton<TView>();

		// NavigationContext へ登録情報を登録
		NavigationContext.RegisterPageInfo(
			typeof(TView),
			new NavigationPageInfo(
				PageType: typeof(TView),
				ViewModelType: typeof(TViewModel),
				PageLifetime: ServiceLifetime.Singleton,
				ViewModelLifetime: ServiceLifetime.Transient));

		Debug.WriteLine($">>> WpfUiViewServiceExtensions AddNavigationPageWithSingletonView ページを登録しました。PageType: {typeof(TView).FullName}, ViewModelType: {typeof(TViewModel).FullName}, PageLifetime: Singleton, ViewModelLifetime: Transient");

		return services;
	}

	/// <summary>
	/// ナビゲーション対象のページを登録します。
	/// <typeparamref name="TView"/> と <typeparamref name="TViewModel"/> の両方を Singleton として登録されます。
	/// DataContext には Singleton の <typeparamref name="TViewModel"/> が設定され、アプリケーション全体で共有されます。
	/// ページの状態と ViewModel の状態の両方をアプリケーション起動時から終了時まで保持する場合に使用してください。
	/// </summary>
	/// <typeparam name="TView">
	/// 登録するページの型。<see cref="FrameworkElement"/> を継承し、引数なしコンストラクタを持つこと。
	/// </typeparam>
	/// <typeparam name="TViewModel">対応する ViewModel の型。参照型であること。</typeparam>
	/// <param name="services">サービスコレクション。</param>
	/// <returns>サービスコレクション（メソッドチェーン用）。</returns>
	public static IServiceCollection AddNavigationPageWithSingletonViewAndViewModel<TView, TViewModel>(
		this IServiceCollection services)
		where TView : FrameworkElement, new()
		where TViewModel : class
	{
		services.AddSingleton<TViewModel>();
		services.AddSingleton<TView>(sp =>
		{
			var view = new TView();
			view.DataContext = sp.GetRequiredService<TViewModel>();
			return view;
		});

		// NavigationContext へ登録情報を登録
		NavigationContext.RegisterPageInfo(
			typeof(TView),
			new NavigationPageInfo(
				PageType: typeof(TView),
				ViewModelType: typeof(TViewModel),
				PageLifetime: ServiceLifetime.Singleton,
				ViewModelLifetime: ServiceLifetime.Singleton));

		Debug.WriteLine($">>> WpfUiViewServiceExtensions AddNavigationPageWithSingletonViewAndViewModel ページを登録しました。PageType: {typeof(TView).FullName}, ViewModelType: {typeof(TViewModel).FullName}, PageLifetime: Singleton, ViewModelLifetime: Singleton");

		return services;
	}
}
