using MangaBinder.Bindings;
using MangaBinder.Binding.Inspection;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using System.Diagnostics;
using System.IO;
using Wpf.Ui;
using MangaBinder.Handlers;

namespace MangaBinder.Binding;

/// <summary>
/// 製本工程-巻選択画面の ViewModel です。
/// </summary>
public class VolumeSelectionPageViewModel : IDisposable, IDataInitializable
{
    /// <summary>作品選択状態ストア。</summary>
    private readonly SeriesWorkspaceStore workspaceStore;

    /// <summary>スコープファクトリー。</summary>
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <summary>ナビゲーションサービス。</summary>
    private readonly INavigationService navigationService;

    /// <summary>コンテントダイアログサービス。</summary>
    private readonly IContentDialogService contentDialogService;

    /// <summary>アプリケーション設定。</summary>
    private readonly AppSettings appSettings;

    private DisposableBag disposableBag;

    /// <summary>選択中の作品名を取得します。</summary>
    public BindableReactiveProperty<string> SeriesTitle { get; }

    /// <summary>選択中の作品エンティティを取得します（サムネイル・巻数情報表示用）。</summary>
    public BindableReactiveProperty<MangaSeries?> SelectedSeries { get; }

    /// <summary>素材を解析中かどうかを取得します。</summary>
    public BindableReactiveProperty<bool> IsLoading { get; }

    /// <summary>読み込み中メッセージを取得します。</summary>
    public BindableReactiveProperty<string> LoadingMessage { get; }

    /// <summary>素材サマリ文字列を取得します。</summary>
    public BindableReactiveProperty<string> MaterialSummaryText { get; }

    /// <summary>選択中の巻サマリ文字列を取得します。</summary>
    public BindableReactiveProperty<string> SelectedVolumeSummaryText { get; }

    /// <summary>次工程へ進めるかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> CanGoNext { get; }

    /// <summary>作品中間フォルダが既に存在するかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> HasExistingWorkFolder { get; }

    /// <summary>中間フォルダを再作成するかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> RecreateWorkFolder => this.workspaceStore.RecreateWorkFolder;

    /// <summary>次工程へ進むコマンドを取得します。</summary>
    public ReactiveCommand GoNextCommand { get; }

    /// <summary>前画面へ戻るコマンドを取得します。</summary>
    public ReactiveCommand GoBackCommand { get; }

    /// <summary>TreeView で現在選択中のノードを取得します。</summary>
    public BindableReactiveProperty<MaterialVolumeNode?> SelectedNode { get; }

    /// <summary>選択中ノードのチェック状態を反転するコマンドを取得します。</summary>
    public ReactiveCommand ToggleSelectedNodeCheckedCommand { get; }

    /// <summary>TreeView で選択した製本対象の一覧を取得します。</summary>
    public NotifyCollectionChangedSynchronizedViewList<BindingQueueItem> BindingQueueItems { get; }

    /// <summary>内部保持する製本キューのリスト。</summary>
    private readonly ObservableList<BindingQueueItem> bindingQueueItems;

    /// <summary>一度でも D&amp;D で並び替えを行ったかどうかを示します。</summary>
    private bool hasManualOrder;

    /// <summary>製本キュー ListView の DropHandler を取得します。</summary>
    public BindingQueueDropHandler DropHandler { get; }

    /// <summary>製本キュー除外領域の DropHandler を取得します。</summary>
    public BindingQueueRemoveDropHandler RemoveDropHandler { get; } = new BindingQueueRemoveDropHandler();

    /// <summary>素材 TreeView の DragHandler を取得します。</summary>
    public MaterialVolumeNodeDragHandler TreeViewDragHandler { get; } = new MaterialVolumeNodeDragHandler();

    /// <summary>右ListView で現在選択中の製本キュー項目を取得します。</summary>
    public BindableReactiveProperty<BindingQueueItem?> SelectedBindingQueueItem { get; }

    /// <summary>選択中の製本キュー項目を除外するコマンドを取得します。</summary>
    public ReactiveCommand RemoveSelectedQueueItemCommand { get; }

    /// <summary>素材構造のルートノードの子一覧を TreeView に公開します。</summary>
    public NotifyCollectionChangedSynchronizedViewList<MaterialVolumeNode> RootNodes { get; }

    /// <summary>内部保持するルートノードのリスト。</summary>
    private readonly ObservableList<MaterialVolumeNode> rootNodes;

    /// <summary>
    /// <see cref="VolumeSelectionPageViewModel"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="workspaceStore">作品選択状態ストア。</param>
    /// <param name="serviceScopeFactory">スコープファクトリー。</param>
    /// <param name="navigationService">ナビゲーションサービス。</param>
    public VolumeSelectionPageViewModel(SeriesWorkspaceStore workspaceStore, IServiceScopeFactory serviceScopeFactory, INavigationService navigationService, IContentDialogService contentDialogService, AppSettings appSettings)
    {
        this.workspaceStore = workspaceStore;
        this.serviceScopeFactory = serviceScopeFactory;
        this.navigationService = navigationService;
        this.contentDialogService = contentDialogService;
        this.appSettings = appSettings;

        this.SeriesTitle = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);
        this.SelectedSeries = new BindableReactiveProperty<MangaSeries?>(null)
            .AddTo(ref this.disposableBag);

        this.IsLoading = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);
        this.LoadingMessage = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);
        this.MaterialSummaryText = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);

        this.SelectedVolumeSummaryText = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);
        this.CanGoNext = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);

        this.HasExistingWorkFolder = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);

        this.GoNextCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.GoNextCommand.Subscribe(_ => this.executeGoNextAsync())
            .AddTo(ref this.disposableBag);

        this.GoBackCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.GoBackCommand.Subscribe(_ => this.navigationService.GoBack())
            .AddTo(ref this.disposableBag);

        this.SelectedNode = new BindableReactiveProperty<MaterialVolumeNode?>(null)
            .AddTo(ref this.disposableBag);
        this.ToggleSelectedNodeCheckedCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.ToggleSelectedNodeCheckedCommand.Subscribe(_ => this.toggleSelectedNodeChecked())
            .AddTo(ref this.disposableBag);

        this.SelectedBindingQueueItem = new BindableReactiveProperty<BindingQueueItem?>(null)
            .AddTo(ref this.disposableBag);
        this.RemoveSelectedQueueItemCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.RemoveSelectedQueueItemCommand.Subscribe(_ => this.removeSelectedQueueItem())
            .AddTo(ref this.disposableBag);

        this.rootNodes = new ObservableList<MaterialVolumeNode>();
        this.RootNodes = this.rootNodes
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        this.bindingQueueItems = new ObservableList<BindingQueueItem>();
        this.BindingQueueItems = this.bindingQueueItems
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        this.DropHandler = new BindingQueueDropHandler(this.bindingQueueItems, this.notifyManualOrderSet);
    }

    /// <inheritdoc/>
    public async ValueTask InitializeDataAsync()
    {
        // 既存ノードを破棄
        foreach (var node in this.rootNodes)
            node.Dispose();
        this.rootNodes.Clear();
        this.bindingQueueItems.Clear();
        this.MaterialSummaryText.Value = string.Empty;
        this.SelectedVolumeSummaryText.Value = string.Empty;
        this.CanGoNext.Value = false;
        this.hasManualOrder = false;
        this.workspaceStore.SelectedMaterialVolumes.Clear();

        if (this.workspaceStore.SelectedSeries.Count == 0)
            return;

        var series = this.workspaceStore.SelectedSeries[0];
        this.SeriesTitle.Value = series.Title;
        this.SelectedSeries.Value = series;

        this.updateWorkFolderState();

        var sources = series.Sources.Count > 0 ? series.Sources : [];
        var validSources = sources.Where(s => Directory.Exists(s.Path)).ToList();
        if (validSources.Count == 0)
            return;

        this.IsLoading.Value = true;
        this.LoadingMessage.Value = "素材を解析しています...";

        try
        {
            // ── 軽量サマリ走査（archive を開かない）──
            this.MaterialSummaryText.Value = await Task.Run(() =>
            {
                int folderCount = 0;
                int archiveCount = 0;
                long archiveTotalBytes = 0;
                int epubCount = 0;

                foreach (var source in validSources)
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(source.Path))
                    {
                        if (Directory.Exists(entry))
                        {
                            folderCount++;
                        }
                        else
                        {
                            var ext = Path.GetExtension(entry).ToLowerInvariant();
                            var fileType = SupportedExtensionHelper.GetFileType(ext);
                            if (fileType == FileType.Archive)
                            {
                                archiveCount++;
                                archiveTotalBytes += new FileInfo(entry).Length;
                            }
                            else if (fileType == FileType.Epub)
                            {
                                epubCount++;
                            }
                        }
                    }
                }

                var sizeText = archiveTotalBytes == 0
                    ? string.Empty
                    : archiveTotalBytes >= 1024L * 1024 * 1024
                        ? $"（{archiveTotalBytes / (1024.0 * 1024 * 1024):F1} GB）"
                        : $"（{archiveTotalBytes / (1024.0 * 1024):F1} MB）";

                return $"フォルダ：{folderCount}件\n圧縮ファイル：{archiveCount}件{sizeText}\nEPUB：{epubCount}件";
            });

            // ── 本解析（バックグラウンド）──
            using var scope = this.serviceScopeFactory.CreateScope();
            var extractor = scope.ServiceProvider.GetRequiredService<ISeriesExtractor>();

            foreach (var source in validSources)
            {
                var rootNode = await extractor.ExtractAsync(source.Path, CancellationToken.None);
                this.rootNodes.Add(rootNode);
            }

            // 全ノードの IsChecked 変更を購読してサマリを更新する
            this.subscribeCheckedNodes(this.rootNodes);
        }
        finally
        {
            this.IsLoading.Value = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var node in this.rootNodes)
            node.Dispose();

        this.disposableBag.Dispose();
    }

    /// <summary>
    /// 指定ノード以下の全ノードの IsChecked 変更を購読します。
    /// </summary>
    private void subscribeCheckedNodes(IEnumerable<MaterialVolumeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == MaterialVolumeNodeType.Folder || node.NodeType == MaterialVolumeNodeType.Epub)
            {
                node.IsChecked
                    .Subscribe(value => this.onNodeCheckedChanged(node, value))
                    .AddTo(ref this.disposableBag);
            }
            this.subscribeCheckedNodes(node.Children);
        }
    }

    /// <summary>
    /// ノードのチェック状態変更時に製本キューを同期します。
    /// </summary>
    private void onNodeCheckedChanged(MaterialVolumeNode node, bool isChecked)
    {
        if (isChecked && node.CanCheck.Value)
        {
            // 重複追加防止
            if (this.bindingQueueItems.Any(q => ReferenceEquals(q.SourceNode, node)))
                return;

            using var scope = this.serviceScopeFactory.CreateScope();
            var volumeNumberExtractor = scope.ServiceProvider.GetRequiredService<VolumeNumberExtractor>();
            var result = volumeNumberExtractor.Extract(node.Name.Value);
            var item = new BindingQueueItem(node, result.Success ? result.VolumeNumber : null);
            this.insertSorted(item);
        }
        else
        {
            var existing = this.bindingQueueItems.FirstOrDefault(q => ReferenceEquals(q.SourceNode, node));
            if (existing is not null)
                this.bindingQueueItems.Remove(existing);
        }

        this.updateCanGoNext();
    }

    /// <summary>
    /// D&amp;D 成功時に呼び出し、以降の自動ソートを無効化します。
    /// </summary>
    private void notifyManualOrderSet()
        => this.hasManualOrder = true;

    /// <summary>
    /// 巻番号順（null 末尾）で挿入します。<br/>
    /// <see cref="hasManualOrder"/> が <see langword="true"/> の場合は末尾追加します。
    /// </summary>
    private void insertSorted(BindingQueueItem item)
    {
        if (this.hasManualOrder)
        {
            this.bindingQueueItems.Add(item);
            return;
        }

        var index = 0;
        for (; index < this.bindingQueueItems.Count; index++)
        {
            var existing = this.bindingQueueItems[index];
            var existingNum = existing.VolumeNumber.Value;
            var newNum = item.VolumeNumber.Value;

            if (newNum is null)
                break; // null は末尾

            if (existingNum is null || newNum < existingNum)
                break;
        }
        this.bindingQueueItems.Insert(index, item);
    }

    /// <summary>
    /// 作品中間フォルダの存在状態を更新します。
    /// </summary>
    private void updateWorkFolderState()
    {
        var series = this.SelectedSeries.Value;

		if (series is null || !this.appSettings.HasValidWorkFolder)
        {
            this.HasExistingWorkFolder.Value = false;
            this.RecreateWorkFolder.Value = false;
            return;
        }

        var seriesFolderPath = this.appSettings.CreateWorkSeriesFolderPath(series.Title);
        var exists = Directory.Exists(seriesFolderPath);

        Debug.WriteLine("===== WorkFolderState =====");
        Debug.WriteLine($"SeriesTitle       : [{series.Title}]");
        Debug.WriteLine($"SeriesTitleLength : [{series.Title.Length}]");
        Debug.WriteLine($"WorkFolderPath    : [{this.appSettings.WorkFolderPath.Value}]");
        Debug.WriteLine($"SeriesFolderPath  : [{seriesFolderPath}]");
        Debug.WriteLine($"Path.GetFullPath  : [{Path.GetFullPath(seriesFolderPath)}]");
        Debug.WriteLine($"Directory.Exists  : [{exists}]");
        Debug.WriteLine($"HasValidWorkFolder: [{this.appSettings.HasValidWorkFolder}]");

        this.HasExistingWorkFolder.Value = exists;

        if (!exists)
            this.RecreateWorkFolder.Value = false;
    }

    /// <summary>
    /// CanGoNext を更新します。
    /// </summary>
    private void updateCanGoNext()
        => this.CanGoNext.Value = this.bindingQueueItems.Count > 0;

    /// <summary>
    /// 選択済み巻を収集してバリデーション後に次画面へ遷移します。
    /// </summary>
    private async void executeGoNextAsync()
    {
        // ① ワークフォルダ未設定 / 不存在
        if (!this.appSettings.HasValidWorkFolder)
        {
            await this.showErrorDialogAsync(
                "ワークフォルダが設定されていないか、存在しません。\n設定画面で確認してください。");
            return;
        }

        // ② 製本対象0件
        if (this.bindingQueueItems.Count == 0)
        {
            await this.showErrorDialogAsync("製本対象が選択されていません。");
            return;
        }

        // ② 巻番号未入力
        if (this.bindingQueueItems.Any(q => q.VolumeNumber.Value is null))
        {
            await this.showErrorDialogAsync("巻番号が未入力の項目があります。");
            return;
        }

        // ③ 巻番号重複
        var numbers = this.bindingQueueItems.Select(q => q.VolumeNumber.Value!.Value).ToList();
        if (numbers.Count != numbers.Distinct().Count())
        {
            await this.showErrorDialogAsync("巻番号が重複しています。");
            return;
        }

        // ④ 抜け巻警告
        var sorted = numbers.OrderBy(n => n).ToList();
        var hasMissing = sorted.Zip(sorted.Skip(1), (a, b) => b - a).Any(diff => diff > 1);
        if (hasMissing)
        {
            var result = await this.showWarningDialogAsync(
                "抜け巻があります。\nこのまま続行しますか？");
            if (result != Wpf.Ui.Controls.ContentDialogResult.Primary)
                return;
        }

        // 遷移処理
        this.workspaceStore.SelectedMaterialVolumes.Clear();
        foreach (var item in this.bindingQueueItems)
        {
            var node = item.SourceNode;
            var volumeNumber = item.VolumeNumber.Value!.Value;
            var sourceType = node.ArchiveEntryPrefix is not null
                ? MaterialVolumeNodeType.Archive
                : node.NodeType;
            this.workspaceStore.SelectedMaterialVolumes.Add(new BindingSourceVolume
            {
                DisplayName = node.Name.Value,
                VolumeNumber = volumeNumber,
                NodeType = node.NodeType,
                SourceType = sourceType,
                SourcePath = node.SourcePath,
                ArchiveEntryPrefix = node.ArchiveEntryPrefix,
                FullPath = node.FullPath.Value,
                OutputVolumeFolderName = this.appSettings.CreateWorkVolumeFolderName(volumeNumber),
            });
        }

        this.navigationService.NavigateWithHierarchy(typeof(SeriesInspectionPage));
    }

    /// <summary>
    /// ブロックエラー用 ContentDialog を表示します。
    /// </summary>
    private async Task showErrorDialogAsync(string message)
    {
        var dialog = new Wpf.Ui.Controls.ContentDialog
        {
            Title = "エラー",
            Content = message,
            CloseButtonText = "OK",
        };
        await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    /// <summary>
    /// 確認警告用 ContentDialog を表示します。
    /// </summary>
    private async Task<Wpf.Ui.Controls.ContentDialogResult> showWarningDialogAsync(string message)
    {
        var dialog = new Wpf.Ui.Controls.ContentDialog
        {
            Title = "確認",
            Content = message,
            PrimaryButtonText = "続行",
            SecondaryButtonText = "戻る",
        };
        return await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
    }

    /// <summary>
    /// 選択中ノードのチェック状態を反転します。
    /// </summary>
    private void toggleSelectedNodeChecked()
    {
        if (this.SelectedNode.Value is null)
            return;

        if (!this.SelectedNode.Value.CanCheck.Value)
            return;

        this.SelectedNode.Value.IsChecked.Value = !this.SelectedNode.Value.IsChecked.Value;
    }

    /// <summary>
    /// 右ListView で選択中の製本キュー項目を除外します。
    /// </summary>
    private void removeSelectedQueueItem()
    {
        if (this.SelectedBindingQueueItem.Value is null)
            return;

        this.SelectedBindingQueueItem.Value.SourceNode.IsChecked.Value = false;
    }

    /// <summary>
    /// 前画面に戻る処理を実行します。
    /// </summary>
    private void executeGoBack()
    {
        // TODO: 前画面への遷移処理を実装する
    }
}
