using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using Wpf.Ui;

namespace MangaBinder.Binding;

/// <summary>
/// 製本ホーム画面の ViewModel です。
/// </summary>
public class HomePageViewModel : IDisposable, IDataInitializable
{
    /// <summary>スコープファクトリー。</summary>
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <summary>ナビゲーションサービス。</summary>
    private readonly INavigationService navigationService;

    /// <summary>作品選択状態ストア。</summary>
    private readonly SeriesWorkspaceStore workspaceStore;

    /// <summary>アプリケーション設定。</summary>
    private readonly AppSettings appSettings;

    private DisposableBag disposableBag;

    /// <summary>内部保持する MangaSeries コレクション。</summary>
    private readonly ObservableList<MangaSeries> series;

    /// <summary>
    /// ListView にバインドする MangaSeries の一覧を取得します。
    /// </summary>
    public NotifyCollectionChangedSynchronizedViewList<MangaSeries> Series { get; }

    /// <summary>
    /// 製本開始ボタンの有効状態を取得します。
    /// Series 内で IsSelected == true の作品が1件以上ある時 true を返します。
    /// </summary>
    public ReactiveProperty<bool> CanStartBinding { get; }

    /// <summary>製本開始コマンドです。</summary>
    public ReactiveCommand<Unit> StartBindingCommand { get; }

    /// <summary>
    /// 作品一覧 ListView の VerticalOffset の保存値を取得します。
    /// </summary>
    public BindableReactiveProperty<double> SavedSeriesListVerticalOffset { get; }

    /// <summary>
    /// <see cref="HomePageViewModel"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="serviceScopeFactory">スコープファクトリー。</param>
    /// <param name="navigationService">ナビゲーションサービス。</param>
    /// <param name="workspaceStore">作品選択状態ストア。</param>
    public HomePageViewModel(IServiceScopeFactory serviceScopeFactory, INavigationService navigationService, SeriesWorkspaceStore workspaceStore, AppSettings appSettings)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.navigationService = navigationService;
        this.workspaceStore = workspaceStore;
        this.appSettings = appSettings;

        this.series = new ObservableList<MangaSeries>();

        this.Series = this.series
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        // CanStartBinding: 選択状態の変化を手動で更新する ReactiveProperty
        var canStartBinding = new ReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);

        // コレクション変化時に全要素の PropertyChanged を再購読する
        this.series.CollectionChanged += (in NotifyCollectionChangedEventArgs<MangaSeries> _) =>
        {
            this.resubscribeIsSelected(canStartBinding);
            canStartBinding.Value = this.series.Any(s => s.IsSelected);
        };

        this.CanStartBinding = canStartBinding;

        this.StartBindingCommand = new ReactiveCommand<Unit>(this.CanStartBinding, initialCanExecute: false)
            .AddTo(ref this.disposableBag);
        this.StartBindingCommand.Subscribe(_ =>
        {
            this.workspaceStore.SelectedSeries.Clear();
            this.workspaceStore.SelectedSeries.AddRange(this.series.Where(s => s.IsSelected));
            this.navigationService.NavigateWithHierarchy(typeof(VolumeSelectionPage));
        });

        this.isSelectedSubscriptions = new CompositeDisposable();

        this.SavedSeriesListVerticalOffset = new BindableReactiveProperty<double>(this.appSettings.SeriesListVerticalOffset.Value)
            .AddTo(ref this.disposableBag);

        // DEBUG: スクロール復元調査用
        // this.SavedSeriesListVerticalOffset
        // 	.Subscribe(v => Debug.WriteLine($"[HomePageViewModel] SavedSeriesListVerticalOffset 変化: {v}"))
        // 	.AddTo(ref this.disposableBag);
    }

    // 各 MangaSeries の IsSelected 変化を購読する CompositeDisposable
    private readonly CompositeDisposable isSelectedSubscriptions;

    /// <summary>
    /// series の各要素の IsSelected 変化を再購読します。
    /// </summary>
    private void resubscribeIsSelected(ReactiveProperty<bool> canStartBinding)
    {
        this.isSelectedSubscriptions.Clear();
        foreach (var s in this.series)
        {
            Observable.FromEvent<System.ComponentModel.PropertyChangedEventHandler, System.ComponentModel.PropertyChangedEventArgs>(
                h => (sender, e) => h(e),
                h => s.PropertyChanged += h,
                h => s.PropertyChanged -= h)
                .Where(e => e.PropertyName == nameof(MangaSeries.IsSelected))
                .Subscribe(_ => canStartBinding.Value = this.series.Any(x => x.IsSelected))
                .AddTo(this.isSelectedSubscriptions);
        }
    }

    /// <inheritdoc/>
    public async ValueTask InitializeDataAsync()
    {
        this.series.Clear();

        using var scope = this.serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<MangaRepository>();

        var result = await repository.GetAllSeriesAsync();
        this.series.AddRange(result);

        this.resubscribeIsSelected(this.CanStartBinding);
        this.CanStartBinding.Value = this.series.Any(s => s.IsSelected);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.isSelectedSubscriptions.Dispose();
        this.disposableBag.Dispose();
    }
}
