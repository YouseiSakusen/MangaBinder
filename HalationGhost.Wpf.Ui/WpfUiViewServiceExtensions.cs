using System.Windows;
using HalationGhost.Wpf.Ui.Navigation;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.DependencyInjection;

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

            if (viewModel is IWindowClosingAware closingAware)
                view.Closing += async (_, _) => await closingAware.OnClosingAsync();

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

        return services;
    }
}
