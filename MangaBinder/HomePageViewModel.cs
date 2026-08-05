using MangaBinder.Bindings;
using MangaBinder.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObservableCollections;
using R3;
using System.Collections.Specialized;
using Wpf.Ui;

namespace MangaBinder;

/// <summary>
/// 製本ホーム画面の ViewModel です。
/// </summary>
public class HomePageViewModel : IDisposable, IDataInitializable, ISavable, INavigationLeavingRequestProvider
{
    /// <summary>ログを出力するロガー。</summary>
    private readonly ILogger<HomePageViewModel> logger;

    /// <summary>スコープファクトリー。</summary>
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <summary>ナビゲーションサービス。</summary>
    private readonly INavigationService navigationService;

    /// <summary>作品選択状態ストア。</summary>
    private readonly SeriesWorkspaceStore workspaceStore;

    /// <summary>アプリケーション設定。</summary>
    private readonly AppSettings appSettings;

    /// <summary>タグ変更追跡ストア。</summary>
    private readonly SeriesTagStore seriesTagStore;

    /// <summary>MangaSeries の正本リストを管理するストア。</summary>
    private readonly MangaSeriesStore mangaSeriesStore;

    /// <summary>製本開始キュー ストア。</summary>
    private readonly BindingQueueStore bindingQueueStore;

    private DisposableBag disposableBag;

    /// <summary>SeriesCardViewModel ごとの IsSelected 購読の管理用 Dictionary。</summary>
    private readonly Dictionary<SeriesCardViewModel, IDisposable> seriesIsSelectedSubscriptions = new();

    /// <summary>
    /// ListView にバインドする SeriesCardViewModel の一覧を取得します。
    /// </summary>
    public NotifyCollectionChangedSynchronizedViewList<SeriesCardViewModel> Series { get; }

    /// <summary>
    /// 製本開始ボタンの有効状態を取得します。
    /// Series 内で IsSelected == true の作品が1件以上ある時 true を返します。
    /// </summary>
    public BindableReactiveProperty<bool> CanStartBinding { get; }

    /// <summary>製本開始コマンドです。</summary>
    public ReactiveCommand<Unit> StartBindingCommand { get; }

    /// <summary>選択作品数を取得します。</summary>
    public BindableReactiveProperty<int> SelectedCount { get; }

    /// <summary>Home 画面の表示状態を取得します。</summary>
    public HomeStateInformation HomeStateInformation { get; } = new();

    /// <summary>設定画面へ遷移するコマンドです。</summary>
    public ReactiveCommand<Unit> NavigateToSettingsCommand { get; }

    /// <summary>
    /// 作品一覧 ListView の VerticalOffset の保存値を取得します。
    /// </summary>
    public BindableReactiveProperty<double> SavedSeriesListVerticalOffset { get; }

    /// <summary>
    /// 素材フォルダを開くコマンドです。<see cref="MangaSource"/> をパラメータとして受け取ります。
    /// </summary>
    public ReactiveCommand<MangaSource> OpenMaterialFolderCommand { get; }

    /// <summary>
    /// 既存作品を編集画面で編集するコマンドです。<see cref="MangaSeries"/> をパラメータとして受け取ります。
    /// </summary>
    public ReactiveCommand<MangaSeries> EditSeriesCommand { get; }

    /// <summary>
    /// <see cref="HomePageViewModel"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="serviceScopeFactory">スコープファクトリー。</param>
    /// <param name="navigationService">ナビゲーションサービス。</param>
    /// <param name="workspaceStore">作品選択状態ストア。</param>
    /// <param name="appSettings">アプリケーション設定。</param>
    /// <param name="seriesTagStore">タグ変更追跡ストア。</param>
    /// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
    public HomePageViewModel(ILogger<HomePageViewModel> logger, IServiceScopeFactory serviceScopeFactory, INavigationService navigationService, SeriesWorkspaceStore workspaceStore, AppSettings appSettings, SeriesTagStore seriesTagStore, MangaSeriesStore mangaSeriesStore, BindingQueueStore bindingQueueStore)
    {
        this.logger = logger;
        this.serviceScopeFactory = serviceScopeFactory;
        this.navigationService = navigationService;
        this.workspaceStore = workspaceStore;
        this.seriesTagStore = seriesTagStore;
        this.appSettings = appSettings;
        this.mangaSeriesStore = mangaSeriesStore;
        this.bindingQueueStore = bindingQueueStore;

        // MangaSeriesStore.All から CreateView で SeriesCardViewModel へ変換し、
        // WPF バインド用に ToNotifyCollectionChanged で公開
        this.Series = this.mangaSeriesStore.All
            .CreateView(series => new SeriesCardViewModel(series, this.bindingQueueStore, this.mangaSeriesStore, this.seriesTagStore))
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        // 削除・追加・置換を処理するため、Series のコレクション変化を監視
        ((INotifyCollectionChanged)this.Series).CollectionChanged += this.onSeriesCollectionChanged;

        // CanStartBinding: 選択状態の変化を手動で更新する BindableReactiveProperty
        var canStartBinding = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);

        var selectedCount = new BindableReactiveProperty<int>(0)
            .AddTo(ref this.disposableBag);
        this.SelectedCount = selectedCount;

        this.CanStartBinding = canStartBinding;

        this.StartBindingCommand = new ReactiveCommand<Unit>(this.CanStartBinding, initialCanExecute: false)
            .AddTo(ref this.disposableBag);
        this.StartBindingCommand.Subscribe(_ =>
        {
            this.workspaceStore.SelectedSeries.Clear();
            this.workspaceStore.SelectedSeries.AddRange(this.Series.Where(c => c.IsSelected.Value).Select(c => c.Series.Value));
            this.navigationService.NavigateWithHierarchy(typeof(VolumeSelectionPage));
        });

        this.SavedSeriesListVerticalOffset = new BindableReactiveProperty<double>(this.appSettings.SeriesListVerticalOffset.Value)
            .AddTo(ref this.disposableBag);

        this.NavigateToSettingsCommand = new ReactiveCommand<Unit>()
            .AddTo(ref this.disposableBag);
        this.NavigateToSettingsCommand.Subscribe(_ => this.navigationService.Navigate(typeof(SettingsPage)));

        this.OpenMaterialFolderCommand = new ReactiveCommand<MangaSource>()
            .AddTo(ref this.disposableBag);
        this.OpenMaterialFolderCommand.Subscribe(source =>
        {
            _ = this.openMaterialFolderAsync(source);
        });

        this.EditSeriesCommand = new ReactiveCommand<MangaSeries>()
            .AddTo(ref this.disposableBag);
        this.EditSeriesCommand.Subscribe(series => this.editSeries(series));

        // DEBUG: スクロール復元調査用
        // this.SavedSeriesListVerticalOffset
        // 	.Subscribe(v => Debug.WriteLine($"[HomePageViewModel] SavedSeriesListVerticalOffset 変化: {v}"))
            // 	.AddTo(ref this.disposableBag);
        }

        /// <summary>
        /// 指定した SeriesCardViewModel の IsSelected プロパティ変化を監視し、BindingQueue と表示状態を更新します。
        /// </summary>
        /// <param name="cardViewModel">監視対象の SeriesCardViewModel。</param>
    private void subscribeIsSelectedForSeries(SeriesCardViewModel cardViewModel)
    {
        var subscription = cardViewModel.IsSelected
            .Subscribe(isSelected =>
            {
                using var scope = this.serviceScopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<BindingQueueDispatcher>();

                if (isSelected)
                    dispatcher.Add(new BindingSeries { Series = cardViewModel.Series.Value, Status = BindingStartStatus.Configuring, AddedAt = DateTime.Now, UpdatedAt = DateTime.Now });
                else
                    dispatcher.Remove(cardViewModel.Series.Value.SeriesId);

                var count = this.Series.Count(x => x.IsSelected.Value);
                this.CanStartBinding.Value = count > 0;
                this.SelectedCount.Value = count;
            });

        // 購読をカード単位で管理
        this.seriesIsSelectedSubscriptions[cardViewModel] = subscription;
    }

    /// <summary>
    /// Series コレクションの変化を処理します。
    /// 削除・置換されたカードのDispose、追加されたカードのIsSelected監視登録を行います。
    /// </summary>
    private void onSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // 追加されたカードの IsSelected を監視
                if (e.NewItems != null)
                {
                    foreach (var newCard in e.NewItems.Cast<SeriesCardViewModel>())
                    {
                        this.subscribeIsSelectedForSeries(newCard);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // 削除されたカードを処理
                if (e.OldItems != null)
                {
                    foreach (var oldCard in e.OldItems.Cast<SeriesCardViewModel>())
                    {
                        // IsSelected 購読をDispose
                        if (this.seriesIsSelectedSubscriptions.TryGetValue(oldCard, out var subscription))
                        {
                            subscription.Dispose();
                            this.seriesIsSelectedSubscriptions.Remove(oldCard);
                        }

                        // カード自体をDispose
                        oldCard.Dispose();
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // 置換された古いカードを処理
                if (e.OldItems != null)
                {
                    foreach (var oldCard in e.OldItems.Cast<SeriesCardViewModel>())
                    {
                        // IsSelected 購読をDispose
                        if (this.seriesIsSelectedSubscriptions.TryGetValue(oldCard, out var subscription))
                        {
                            subscription.Dispose();
                            this.seriesIsSelectedSubscriptions.Remove(oldCard);
                        }

                        // カード自体をDispose
                        oldCard.Dispose();
                    }
                }

                // 新しいカードのIsSelected監視を登録
                if (e.NewItems != null)
                {
                    foreach (var newCard in e.NewItems.Cast<SeriesCardViewModel>())
                    {
                        this.subscribeIsSelectedForSeries(newCard);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // リセット時は、既存のカードをすべてクリーンアップ
                foreach (var oldCard in this.seriesIsSelectedSubscriptions.Keys.ToList())
                {
                    var subscription = this.seriesIsSelectedSubscriptions[oldCard];
                    subscription.Dispose();
                    this.seriesIsSelectedSubscriptions.Remove(oldCard);
                }
                break;
        }
    }

    /// <inheritdoc/>
    public async ValueTask InitializeDataAsync()
    {
        // 初回のみ DB から取得して Store へ反映する
        if (this.mangaSeriesStore.All.Count == 0)
        {
            using var managerScope = this.serviceScopeFactory.CreateScope();
            var manager = managerScope.ServiceProvider.GetRequiredService<MangaSeriesManager>();
            await manager.GetAllSeriesAsync();
            // MangaSeriesManager内部でMangaSeriesStore.ReplaceAll()が実行済み
            // CreateViewが自動追従するため、Home側での二重ReplaceAllは不要
        }

        // 毎回: Store の状態を元に SeriesCardViewModel.IsSelected を復元する
        using var dispatcherScope = this.serviceScopeFactory.CreateScope();
        var dispatcher = dispatcherScope.ServiceProvider.GetRequiredService<BindingQueueDispatcher>();
        foreach (var cardViewModel in this.Series)
        {
            cardViewModel.IsSelected.Value = dispatcher.Contains(cardViewModel.Series.Value.SeriesId);
        }

        // 毎回: ボタン状態更新
        var count = this.Series.Count(c => c.IsSelected.Value);
        this.CanStartBinding.Value = count > 0;
        this.SelectedCount.Value = count;

        // 毎回: HomeState 更新
        using var stateScope = this.serviceScopeFactory.CreateScope();
        var stateRepository = stateScope.ServiceProvider.GetRequiredService<MangaRepository>();
        var homeState = await stateRepository.GetHomeStateInformationAsync();
        this.HomeStateInformation.SeriesCount.Value                       = homeState.SeriesCount.Value;
        this.HomeStateInformation.HasMaterialSourceFolder.Value           = homeState.HasMaterialSourceFolder.Value;
        this.HomeStateInformation.HasCompletedMaterialFolderScanJob.Value = homeState.HasCompletedMaterialFolderScanJob.Value;
        this.HomeStateInformation.EmptyStateKind.Value                    = homeState.EmptyStateKind.Value;

        // EditTarget を消費：編集済みカードだけ表示更新
        var editTarget = this.workspaceStore.EditTarget;
        if (editTarget is not null)
        {
            var card = this.Series.FirstOrDefault(
                x => x.Series.Value.SeriesId == editTarget.SeriesId);

            card?.RefreshDisplay();

            this.workspaceStore.EditTarget = null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ISaveResult> SaveAsync()
    {
        if (!this.seriesTagStore.HasChanges)
            return SaveResult.Success();

        try
        {
            using var scope = this.serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<MangaRepository>();
            await repository.SaveSeriesTagsAsync(this.seriesTagStore.GetDirtyItems());
            this.seriesTagStore.Clear();
            return SaveResult.Success("タグを保存しました");
        }
        catch (Exception ex)
        {
            return SaveResult.Failure($"タグの保存に失敗しました: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // CollectionChanged 購読を明示的に解除
        ((INotifyCollectionChanged)this.Series).CollectionChanged -= this.onSeriesCollectionChanged;

        // 残存する IsSelected 購読をすべて破棄
        foreach (var subscription in this.seriesIsSelectedSubscriptions.Values)
        {
            subscription.Dispose();
        }
        this.seriesIsSelectedSubscriptions.Clear();

        // 残っているすべての SeriesCardViewModel をクリーンアップ
        foreach (var card in this.Series)
        {
            card.Dispose();
        }

        this.disposableBag.Dispose();
    }

    /// <summary>
    /// 自身へ遷移してくる際に、遷移元の一時状態を保持するよう要求します。
    /// </summary>
    /// <returns>遷移元に対して状態保持を要求する <see cref="NavigationLeavingRequest"/>。</returns>
    public NavigationLeavingRequest GetNavigationLeavingRequest()
    {
        return new NavigationLeavingRequest
        {
            PreserveState = true,
        };
    }

    /// <summary>
    /// 素材フォルダを開きます。
    /// </summary>
    /// <param name="source">開くフォルダの情報。</param>
    private async Task openMaterialFolderAsync(MangaSource source)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var opener = scope.ServiceProvider.GetRequiredService<MaterialFolderOpener>();
        await opener.OpenAsync(source);
    }

    /// <summary>
    /// 指定した作品を編集対象として設定し、EditorPage へ遷移します。
    /// </summary>
    /// <param name="series">編集対象の作品。</param>
    private void editSeries(MangaSeries series)
    {
        // 編集対象を指定作品に設定
        this.workspaceStore.EditTarget = series;

        // NavigationHierarchy を使用して遷移
        this.navigationService.NavigateWithHierarchy(typeof(EditorPage));
    }
}

