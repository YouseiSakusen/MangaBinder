using HalationGhost.Wpf.Ui.Navigation;
using MangaBinder.Bindings;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using System.Collections.ObjectModel;
using Wpf.Ui;

namespace MangaBinder;

/// <summary>
/// 製本ホーム画面の ViewModel です。
/// </summary>
public class HomePageViewModel : IDisposable, IDataInitializable, ISavable
{
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

    /// <summary>製本開始状態 Dispatcher。</summary>
    private readonly BindingQueueDispatcher bindingQueueDispatcher;

    /// <summary>MangaSeries 読み込みマネージャー。</summary>
    private readonly MangaSeriesManager mangaSeriesManager;

    /// <summary>MangaSeries の正本リストを管理するストア。</summary>
    private readonly MangaSeriesStore mangaSeriesStore;

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
    /// Home で選択可能なタグ一覧（ポップアップ用チェックボックスリスト）を取得します。
    /// </summary>
    public ObservableCollection<SeriesTagSelectionItem> SelectableTagsForPopup { get; } = new();

    /// <summary>
    /// タグポップアップを開く前に、対象作品のチェック状態を準備するコマンドです。
    /// </summary>
    public ReactiveCommand<MangaSeries> PrepareTagPopupCommand { get; }

    /// <summary>
    /// 素材フォルダを開くコマンドです。<see cref="MangaSource"/> をパラメータとして受け取ります。
    /// </summary>
    public ReactiveCommand<MangaSource> OpenMaterialFolderCommand { get; }

    /// <summary>
    /// <see cref="HomePageViewModel"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="serviceScopeFactory">スコープファクトリー。</param>
    /// <param name="navigationService">ナビゲーションサービス。</param>
    /// <param name="workspaceStore">作品選択状態ストア。</param>
    /// <param name="appSettings">アプリケーション設定。</param>
    /// <param name="seriesTagStore">タグ変更追跡ストア。</param>
    /// <param name="bindingQueueDispatcher">製本開始状態 Dispatcher。</param>
    /// <param name="mangaSeriesManager">MangaSeries 読み込みマネージャー。</param>
    /// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
    public HomePageViewModel(IServiceScopeFactory serviceScopeFactory, INavigationService navigationService, SeriesWorkspaceStore workspaceStore, AppSettings appSettings, SeriesTagStore seriesTagStore, BindingQueueDispatcher bindingQueueDispatcher, MangaSeriesManager mangaSeriesManager, MangaSeriesStore mangaSeriesStore)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.navigationService = navigationService;
        this.workspaceStore = workspaceStore;
        this.seriesTagStore = seriesTagStore;
        this.appSettings = appSettings;
        this.bindingQueueDispatcher = bindingQueueDispatcher;
        this.mangaSeriesManager = mangaSeriesManager;
        this.mangaSeriesStore = mangaSeriesStore;

        this.series = new ObservableList<MangaSeries>();

        this.Series = this.series
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        // CanStartBinding: 選択状態の変化を手動で更新する BindableReactiveProperty
        var canStartBinding = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);

        var selectedCount = new BindableReactiveProperty<int>(0)
            .AddTo(ref this.disposableBag);
        this.SelectedCount = selectedCount;

        // コレクション変化時に全要素の PropertyChanged を再購読する
        this.series.CollectionChanged += (in NotifyCollectionChangedEventArgs<MangaSeries> _) =>
        {
            this.resubscribeIsSelected();
            var count = this.series.Count(s => s.IsSelected);
            this.CanStartBinding.Value = count > 0;
            this.SelectedCount.Value = count;
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

        this.NavigateToSettingsCommand = new ReactiveCommand<Unit>()
            .AddTo(ref this.disposableBag);
        this.NavigateToSettingsCommand.Subscribe(_ => this.navigationService.Navigate(typeof(SettingsPage)));

        this.PrepareTagPopupCommand = new ReactiveCommand<MangaSeries>()
            .AddTo(ref this.disposableBag);
        this.PrepareTagPopupCommand.Subscribe(series =>
        {
            this.SelectableTagsForPopup.Clear();
            foreach (var tag in this.mangaSeriesStore.GetTags()
                         .OrderByDescending(t => t.DisplayOrder)
                         .ThenByDescending(t => t.TagId))
            {
                var isChecked = series.Tags.Any(t => t.TagId == tag.TagId);
                var item = new SeriesTagSelectionItem(tag, isChecked);
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(SeriesTagSelectionItem.IsChecked))
                        return;
                    using var scope = this.serviceScopeFactory.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<SeriesTagDispatcher>();
                    dispatcher.ApplyTag(series, item.Tag, item.IsChecked);
                };
                this.SelectableTagsForPopup.Add(item);
            }
        });

        this.OpenMaterialFolderCommand = new ReactiveCommand<MangaSource>()
            .AddTo(ref this.disposableBag);
        this.OpenMaterialFolderCommand.Subscribe(source =>
        {
            _ = this.openMaterialFolderAsync(source);
        });

        // DEBUG: スクロール復元調査用
        // this.SavedSeriesListVerticalOffset
        // 	.Subscribe(v => Debug.WriteLine($"[HomePageViewModel] SavedSeriesListVerticalOffset 変化: {v}"))
        // 	.AddTo(ref this.disposableBag);
    }

    // 各 MangaSeries の IsSelected 変化を購読する CompositeDisposable
    private readonly CompositeDisposable isSelectedSubscriptions;

    /// <summary>
    /// 各 <see cref="MangaSeries"/> のタグを <see cref="MangaSeriesStore"/> のタグへ再同期します。
    /// </summary>
    private void refreshSeriesTagsFromStore()
    {
        var tagMap = this.mangaSeriesStore.GetTags().ToDictionary(t => t.TagId);

        foreach (var s in this.series)
        {
            var currentTagIds = s.Tags.Select(t => t.TagId).ToList();
            s.Tags.Clear();
            foreach (var tagId in currentTagIds)
            {
                if (tagMap.TryGetValue(tagId, out var currentTag))
                    s.Tags.Add(currentTag);
            }
        }
    }

    /// <summary>
    /// series の各要素の IsSelected 変化を再購読します。
    /// </summary>
    private void resubscribeIsSelected()
    {
        this.isSelectedSubscriptions.Clear();
        foreach (var s in this.series)
        {
            var captured = s;
            Observable.FromEvent<System.ComponentModel.PropertyChangedEventHandler, System.ComponentModel.PropertyChangedEventArgs>(
                h => (sender, e) => h(e),
                h => captured.PropertyChanged += h,
                h => captured.PropertyChanged -= h)
                .Where(e => e.PropertyName == nameof(MangaSeries.IsSelected))
                .Subscribe(_ =>
                {
                    if (captured.IsSelected)
                        this.bindingQueueDispatcher.Add(new BindingSeries { Series = captured, Status = BindingStartStatus.Configuring, AddedAt = DateTime.Now, UpdatedAt = DateTime.Now });
                    else
                        this.bindingQueueDispatcher.Remove(captured.SeriesId);

                    var count = this.series.Count(x => x.IsSelected);
                    this.CanStartBinding.Value = count > 0;
                    this.SelectedCount.Value = count;
                })
                .AddTo(this.isSelectedSubscriptions);
        }
    }

    /// <inheritdoc/>
    public async ValueTask InitializeDataAsync()
    {
        // 初回のみ DB から取得して Store へ反映する
        if (this.series.Count == 0)
        {
            var result = await this.mangaSeriesManager.GetAllSeriesAsync();
            this.series.Clear();
            this.series.AddRange(result);
        }

        // 毎回: Store の状態を元に IsSelected を復元する
        foreach (var s in this.series)
            s.IsSelected = this.bindingQueueDispatcher.Contains(s.SeriesId);

        // 毎回: タグ再同期
        this.refreshSeriesTagsFromStore();

        // 毎回: 購読再設定・ボタン状態更新
        this.resubscribeIsSelected();
        var count = this.series.Count(s => s.IsSelected);
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

        // タグマスタをポップアップ用リストに反映（再同期）
        this.SelectableTagsForPopup.Clear();
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
        this.isSelectedSubscriptions.Dispose();
        this.disposableBag.Dispose();
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
}
