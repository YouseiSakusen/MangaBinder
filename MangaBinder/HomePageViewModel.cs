using MangaBinder.Bindings;
using MangaBinder.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObservableCollections;
using R3;
using System.Collections.Specialized;
using Wpf.Ui;
using Wpf.Ui.Controls;

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

    /// <summary>タグ変更追跡ストア。</summary>
    private readonly SeriesTagStore seriesTagStore;

    /// <summary>Home 画面用派生 Store。SeriesCardViewModel の生成・所有・Dispose を担当。</summary>
    private readonly HomeSeriesStore homeSeriesStore;

    /// <summary>MangaSeries の正本リストを管理するストア。</summary>
    private readonly MangaSeriesStore mangaSeriesStore;

    /// <summary>製本待ち状態ストア。</summary>
    private readonly BindingQueueStore bindingQueueStore;

    /// <summary>アプリケーション設定。</summary>
    private readonly AppSettings appSettings;

    /// <summary>Snackbar 通知サービス。</summary>
    private readonly ISnackbarService snackbarService;

    /// <summary>タイトルジャンプの戻る履歴スタック。</summary>
    private readonly Stack<double> titleJumpHistory = new();

    private DisposableBag disposableBag;

    /// <summary>
    /// ListView にバインドする SeriesCardViewModel の一覧を取得します。
    /// </summary>
    public NotifyCollectionChangedSynchronizedViewList<HomeSeriesCardViewModel> Series { get; }

    /// <summary>製本開始コマンドです。</summary>
    public ReactiveCommand<Unit> StartBindingCommand { get; }

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
    /// Home の CheckBox で製本待ち状態を切り替えるコマンドです。<see cref="HomeSeriesCardViewModel"/> をパラメータとして受け取ります。
    /// </summary>
    public ReactiveCommand<HomeSeriesCardViewModel> ToggleBindingQueueCommand { get; }

    /// <summary>
    /// タイトルジャンプ入力テキストを取得または設定します。
    /// </summary>
    public BindableReactiveProperty<string?> TitleJumpInput { get; }

    /// <summary>
    /// タイトルジャンプ実行時のスクロール目標となるVerticalOffsetを取得します。
    /// </summary>
    public BindableReactiveProperty<double> JumpRequestedVerticalOffset { get; }

    /// <summary>
    /// タイトルジャンプスクロール要求をカウントします。
    /// スクロール要求が発生するたびにインクリメントされます。
    /// </summary>
    public BindableReactiveProperty<int> JumpScrollRequest { get; }

    /// <summary>
    /// タイトルジャンプコマンドです。
    /// </summary>
    public ReactiveCommand<Unit> TitleJumpCommand { get; }

    /// <summary>
    /// タイトルジャンプから戻ることが可能な状態を取得します。
    /// 履歴が1件以上ある場合に true を返します。
    /// </summary>
    public BindableReactiveProperty<bool> CanTitleJumpBack { get; }

    /// <summary>
    /// タイトルジャンプから戻るコマンドです。
    /// </summary>
    public ReactiveCommand<Unit> TitleJumpBackCommand { get; }

    /// <summary>
    /// <see cref="HomePageViewModel"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="serviceScopeFactory">スコープファクトリー。</param>
    /// <param name="navigationService">ナビゲーションサービス。</param>
    /// <param name="workspaceStore">作品選択状態ストア。</param>
    /// <param name="appSettings">アプリケーション設定。</param>
    /// <param name="seriesTagStore">タグ変更追跡ストア。</param>
    /// <param name="homeSeriesStore">Home 画面用派生 Store。</param>
    /// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
    /// <param name="bindingQueueStore">製本待ち状態ストア。</param>
    /// <param name="snackbarService">Snackbar 通知サービス。</param>
    public HomePageViewModel(ILogger<HomePageViewModel> logger, IServiceScopeFactory serviceScopeFactory, INavigationService navigationService, SeriesWorkspaceStore workspaceStore, AppSettings appSettings, SeriesTagStore seriesTagStore, HomeSeriesStore homeSeriesStore, MangaSeriesStore mangaSeriesStore, BindingQueueStore bindingQueueStore, ISnackbarService snackbarService)
    {
        this.logger = logger;
        this.serviceScopeFactory = serviceScopeFactory;
        this.navigationService = navigationService;
        this.workspaceStore = workspaceStore;
        this.appSettings = appSettings;
        this.seriesTagStore = seriesTagStore;
        this.homeSeriesStore = homeSeriesStore;
        this.mangaSeriesStore = mangaSeriesStore;
        this.bindingQueueStore = bindingQueueStore;
        this.snackbarService = snackbarService;

        // Home 用一覧を HomeSeriesStore から取得
        this.Series = this.homeSeriesStore.HomeCards;

        // StartBindingCommand の CanExecute を BindingQueueStore.IsEmpty から導出
        // Queue が空でない時に実行可能 (!IsEmpty = Count > 0)
        this.StartBindingCommand = new ReactiveCommand<Unit>(
            this.bindingQueueStore.IsEmpty.Select(isEmpty => !isEmpty),
            initialCanExecute: this.bindingQueueStore.Count.Value > 0)
            .AddTo(ref this.disposableBag);
        this.StartBindingCommand.Subscribe(_ =>
        {
            this.workspaceStore.SelectedSeries.Clear();
            this.workspaceStore.SelectedSeries.AddRange(this.Series.Where(c => c.IsSelected.Value).Select(c => c.Series.Value));
            this.navigationService.NavigateWithHierarchy(typeof(VolumeSelectionPage));
        });

        this.SavedSeriesListVerticalOffset = new BindableReactiveProperty<double>(this.appSettings.SeriesListVerticalOffset.Value)
            .AddTo(ref this.disposableBag);

        this.TitleJumpInput = new BindableReactiveProperty<string?>(null)
            .AddTo(ref this.disposableBag);

        this.JumpRequestedVerticalOffset = new BindableReactiveProperty<double>(0.0)
            .AddTo(ref this.disposableBag);

        this.JumpScrollRequest = new BindableReactiveProperty<int>(0)
            .AddTo(ref this.disposableBag);

        this.TitleJumpCommand = new ReactiveCommand<Unit>()
            .AddTo(ref this.disposableBag);
        this.TitleJumpCommand.Subscribe(_ => this.executeTitleJump());

        // タイトルジャンプ戻る機能の初期化
        var canTitleJumpBack = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);
        this.CanTitleJumpBack = canTitleJumpBack;

        this.TitleJumpBackCommand = new ReactiveCommand<Unit>(this.CanTitleJumpBack, initialCanExecute: false)
            .AddTo(ref this.disposableBag);
        this.TitleJumpBackCommand.Subscribe(_ => this.executeTitleJumpBack());

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

        this.ToggleBindingQueueCommand = new ReactiveCommand<HomeSeriesCardViewModel>()
            .AddTo(ref this.disposableBag);
        this.ToggleBindingQueueCommand.Subscribe(cardViewModel => this.toggleBindingQueue(cardViewModel));

        // DEBUG: スクロール復元調査用
        // this.SavedSeriesListVerticalOffset
        // 	.Subscribe(v => Debug.WriteLine($"[HomePageViewModel] SavedSeriesListVerticalOffset 変化: {v}"))
            // 	.AddTo(ref this.disposableBag);
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

        // 毎回: HomeState 更新
        using var stateScope = this.serviceScopeFactory.CreateScope();
        var stateRepository = stateScope.ServiceProvider.GetRequiredService<MangaRepository>();
        var homeState = await stateRepository.GetHomeStateInformationAsync();
        this.HomeStateInformation.SeriesCount.Value                       = homeState.SeriesCount.Value;
        this.HomeStateInformation.HasMaterialSourceFolder.Value           = homeState.HasMaterialSourceFolder.Value;
        this.HomeStateInformation.HasCompletedMaterialFolderScanJob.Value = homeState.HasCompletedMaterialFolderScanJob.Value;
        this.HomeStateInformation.EmptyStateKind.Value                    = homeState.EmptyStateKind.Value;

        // EditTarget をクリア
        // 保存通知は ExistingSeriesSaveManager から MangaSeriesStore.NotifySeriesChanged() を通じて流れる
        this.workspaceStore.EditTarget = null;
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

    /// <summary>
    /// Home 画面の CheckBox で選択状態を切り替えます。
    /// BindingQueueDispatcher を通じて Queue を更新し、
    /// 表示更新は HomeSeriesStore → IsSelected で自動的に戻ります。
    /// </summary>
    private void toggleBindingQueue(HomeSeriesCardViewModel cardViewModel)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<BindingQueueDispatcher>();

        // 現在の IsSelected.Value から判定
        // false → true : Add (まだQueue内にないので追加)
        // true → false : Remove (Queue内にあるので削除)
        if (cardViewModel.IsSelected.Value)
        {
            // 既に Queue に入っているので削除
            dispatcher.Remove(cardViewModel.Series.Value.SeriesId);
        }
        else
        {
            // Queue にまだ入っていないので追加
            dispatcher.Add(new BindingSeries
            {
                Series = cardViewModel.Series.Value,
                Status = BindingStartStatus.Configuring,
                AddedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        }
    }

    /// <summary>
    /// タイトル先頭一致によるジャンプを実行します。
    /// TextBox入力から共通の検索ワード解析を用いてジャンプキーを抽出し、
    /// 対象作品を検索してスクロール要求を発行します。
    /// </summary>
    private void executeTitleJump()
    {
        var input = this.TitleJumpInput.Value;

        // 入力が空または空白のみの場合はスキップ
        if (string.IsNullOrWhiteSpace(input))
            return;

        // MangaSeriesSearchMatcher を用いて共通の検索ワード解析を実行
        var matcher = new MangaSeriesSearchMatcher(input);

        if (!matcher.IsValid)
            return;

        var normalizedWords = matcher.GetSearchWords();
        var displayWords = matcher.GetDisplayWords();

        // 0ワードの場合はスキップ
        if (normalizedWords.Count == 0)
            return;

        // 先頭1ワードを抽出（検索判定用は正規化済み、表示用は入力由来）
        var jumpKeyNormalized = normalizedWords[0];
        var jumpKeyDisplay = displayWords[0];

        // 一致する作品をタイトル先頭一致で検索（正規化済みワードで比較）
        var targetIndex = -1;
        var index = 0;

        foreach (var cardViewModel in this.Series)
        {
            var normalizedTitle = cardViewModel.Series.Value.NormalizedTitleInternal ?? string.Empty;

            // 大文字・小文字を区別しないで先頭一致を判定
            if (normalizedTitle.StartsWith(jumpKeyNormalized, StringComparison.OrdinalIgnoreCase))
            {
                targetIndex = index;
                break;
            }

            index++;
        }

        // 複数ワード場合と該当なし場合で通知を表示
        if (normalizedWords.Count > 1)
        {
            if (targetIndex >= 0)
            {
                // 複数ワード＆一致あり：先頭1ワード対応の通知を表示
                var message = $"タイトルジャンプは1ワードのみ対応しています。先頭の「{jumpKeyDisplay}」でジャンプしました。";
                this.snackbarService.Show(
                    "タイトルジャンプ",
                    message,
                    ControlAppearance.Secondary,
                    new SymbolIcon { Symbol = SymbolRegular.Info24 },
                    TimeSpan.FromSeconds(5));
            }
            else
            {
                // 複数ワード＆一致なし：1ワード対応＋該当なしの複合通知
                var message = $"タイトルジャンプは1ワードのみ対応しています。先頭の「{jumpKeyDisplay}」で探しましたが、該当する作品が見つかりませんでした。";
                this.snackbarService.Show(
                    "タイトルジャンプ",
                    message,
                    ControlAppearance.Secondary,
                    new SymbolIcon { Symbol = SymbolRegular.Info24 },
                    TimeSpan.FromSeconds(5));
            }
        }
        else if (targetIndex < 0)
        {
            // 1ワード＆一致なし：該当なし通知
            var message = $"「{jumpKeyDisplay}」で始まる作品が見つかりませんでした。";
            this.snackbarService.Show(
                "タイトルジャンプ",
                message,
                ControlAppearance.Secondary,
                new SymbolIcon { Symbol = SymbolRegular.Info24 },
                TimeSpan.FromSeconds(5));
        }

        // 該当なしの場合はスキップ
        if (targetIndex < 0)
            return;

        // ジャンプ実行直前の現在位置を履歴へPush（ジャンプ先と異なる場合のみ）
        var currentOffset = this.SavedSeriesListVerticalOffset.Value;
        if (Math.Abs(currentOffset - targetIndex) > double.Epsilon)
        {
            this.titleJumpHistory.Push(currentOffset);
            this.CanTitleJumpBack.Value = true;
        }

        // スクロール要求を設定
        this.JumpRequestedVerticalOffset.Value = targetIndex;
        this.JumpScrollRequest.Value++;
    }

    /// <summary>
    /// タイトルジャンプから戻るコマンドを実行します。
    /// 保存されている前の位置へスクロール要求を発行します。
    /// </summary>
    private void executeTitleJumpBack()
    {
        // 履歴が空の場合はスキップ
        if (this.titleJumpHistory.Count == 0)
            return;

        // 履歴から最後のOffsetをPop
        var previousOffset = this.titleJumpHistory.Pop();

        // スクロール要求を設定
        this.JumpRequestedVerticalOffset.Value = previousOffset;
        this.JumpScrollRequest.Value++;

        // 履歴の残件数に応じて戻るButtonの有効状態を更新
        this.CanTitleJumpBack.Value = this.titleJumpHistory.Count > 0;

        // 戻る成功時にタイトルジャンプ入力をクリア
        this.TitleJumpInput.Value = string.Empty;
    }
}

