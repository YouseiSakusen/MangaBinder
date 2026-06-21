using MangaBinder.Bindings.Inspection;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using System.Diagnostics;
using System.IO;
using Wpf.Ui;
using MangaBinder.Handlers;
using Wpf.Ui.Controls;
using MangaBinder.Helpers;

namespace MangaBinder.Bindings;

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

    /// <summary>スナックバーサービス。</summary>
    private readonly ISnackbarService snackbarService;

    /// <summary>素材フォルダローダー。</summary>
    private readonly SeriesMaterialFolderLoader seriesMaterialFolderLoader;

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

    /// <summary>アイキャッチカード用の素材数を取得します。</summary>
    public BindableReactiveProperty<string> MaterialCountText { get; }

    /// <summary>素材内訳：フォルダ数を取得します。</summary>
    public BindableReactiveProperty<string> MaterialFolderCountText { get; }

    /// <summary>素材内訳：圧縮ファイル数とサイズを取得します。</summary>
    public BindableReactiveProperty<string> MaterialArchiveCountText { get; }

    /// <summary>素材内訳：EPUB数を取得します。</summary>
    public BindableReactiveProperty<string> MaterialEpubCountText { get; }

    /// <summary>選択中の巻サマリ文字列を取得します。</summary>
    public BindableReactiveProperty<string> SelectedVolumeSummaryText { get; }

    /// <summary>次工程へ進めるかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> CanGoNext { get; }

    /// <summary>作品中間フォルダが既に存在するかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> HasExistingWorkFolder { get; }

    /// <summary>中間フォルダを再作成するかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> RecreateWorkFolder => this.workspaceStore.RecreateWorkFolder;

    /// <summary>素材展開方法の選択値を取得します（0=新規作成、1=既存を使用）。</summary>
    public BindableReactiveProperty<int> ImageExpansionMethod { get; }

    /// <summary>素材展開方法のオプション一覧を取得します。</summary>
    public List<string> ImageExpansionOptions { get; }

    /// <summary>巻フォルダ名の桁数を取得します（1, 2, 3 など）。</summary>
    public BindableReactiveProperty<int> VolumeFolderDigits { get; }

    /// <summary>巻フォルダ名の桁数選択肢一覧を取得します。</summary>
    public List<VolumeFolderDigitOption> VolumeFolderDigitOptions { get; }

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
    /// <param name="contentDialogService">コンテントダイアログサービス。</param>
    /// <param name="snackbarService">スナックバーサービス。</param>
    /// <param name="seriesMaterialFolderLoader">素材フォルダローダー。</param>
    /// <param name="appSettings">アプリケーション設定。</param>
    public VolumeSelectionPageViewModel(
        SeriesWorkspaceStore workspaceStore,
        IServiceScopeFactory serviceScopeFactory,
        INavigationService navigationService,
        IContentDialogService contentDialogService,
        ISnackbarService snackbarService,
        SeriesMaterialFolderLoader seriesMaterialFolderLoader,
        AppSettings appSettings)
    {
        this.workspaceStore = workspaceStore;
        this.serviceScopeFactory = serviceScopeFactory;
        this.navigationService = navigationService;
        this.contentDialogService = contentDialogService;
        this.snackbarService = snackbarService;
        this.seriesMaterialFolderLoader = seriesMaterialFolderLoader;
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
        this.MaterialCountText = new BindableReactiveProperty<string>("0 件")
            .AddTo(ref this.disposableBag);
        this.MaterialFolderCountText = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);
        this.MaterialArchiveCountText = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);
        this.MaterialEpubCountText = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);

        this.SelectedVolumeSummaryText = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);
        this.CanGoNext = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);

        this.HasExistingWorkFolder = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);

        this.ImageExpansionMethod = new BindableReactiveProperty<int>(0)
            .AddTo(ref this.disposableBag);

        this.ImageExpansionOptions = new List<string>
        {
            "作品フォルダを新規作成する（既存フォルダ削除）",
            "既存の画像を使用する"
        };

        this.VolumeFolderDigits = new BindableReactiveProperty<int>(2)
            .AddTo(ref this.disposableBag);

        this.VolumeFolderDigitOptions = new List<VolumeFolderDigitOption>
        {
            new VolumeFolderDigitOption(1, "1桁", "（例：1巻）"),
            new VolumeFolderDigitOption(2, "2桁", "（例：01巻）"),
            new VolumeFolderDigitOption(3, "3桁", "（例：001巻）"),
        };

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

        // 素材展開方法の選択変更を監視し、RecreateWorkFolder を更新
        this.ImageExpansionMethod.Subscribe(selectedIndex =>
        {
            // 0 = 新規作成（RecreateWorkFolder = true）
            // 1 = 既存を使用（RecreateWorkFolder = false）
            this.workspaceStore.RecreateWorkFolder.Value = selectedIndex == 0;
        }).AddTo(ref this.disposableBag);

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
        this.MaterialFolderCountText.Value = string.Empty;
        this.MaterialArchiveCountText.Value = string.Empty;
        this.MaterialEpubCountText.Value = string.Empty;
        this.SelectedVolumeSummaryText.Value = string.Empty;
        this.CanGoNext.Value = false;
        this.hasManualOrder = false;
        this.workspaceStore.SelectedMaterialVolumes.Clear();

        // BindingTarget から製本対象作品を取得
        var series = this.workspaceStore.BindingTarget;
        if (series is null)
            return;

        this.SeriesTitle.Value = series.Title;
        this.SelectedSeries.Value = series;

        this.updateWorkFolderState();
        this.updateVolumeFolderDigits();

        // ローディング開始
        this.IsLoading.Value = true;
        this.LoadingMessage.Value = "素材を解析しています...";

        // ProgressRing が描画される機会を作る
        await Task.Yield();

        // 画面表示後にバックグラウンド実行する
        _ = this.loadMaterialsAsync(series);
    }

    /// <summary>
    /// 素材フォルダを非同期で読み込みます。
    /// </summary>
    private async Task loadMaterialsAsync(MangaSeries series)
    {
        try
        {
            var result = await this.seriesMaterialFolderLoader.GetMaterialsAsync(series, CancellationToken.None);

            switch (result.Status)
            {
                case MaterialFolderStatus.DriveNotReady:
                    var driveLetter = string.IsNullOrEmpty(result.TargetPath)
                        ? string.Empty
                        : Path.GetPathRoot(result.TargetPath) ?? string.Empty;
                    var driveMessage = string.IsNullOrEmpty(driveLetter)
                        ? "ドライブの接続を確認してください。"
                        : $"ドライブ({driveLetter})の接続を確認してください。";
                    this.snackbarService.Show(
                        "ドライブが接続されていません",
                        driveMessage,
                        ControlAppearance.Caution,
                        new SymbolIcon { Symbol = SymbolRegular.Warning24 },
                        TimeSpan.MaxValue);
                    return;

                case MaterialFolderStatus.NoMaterialSource:
                case MaterialFolderStatus.MaterialSourceNotFound:
                    this.snackbarService.Show(
                        "素材フォルダが見つかりません",
                        "素材フォルダが存在しません。",
                        ControlAppearance.Danger,
                        new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                        TimeSpan.MaxValue);
                    return;
            }

            // NestedArchive 判定：圧縮ファイルの中に圧縮ファイルが含まれている場合
            if (result.HasNestedArchive)
            {
                // TreeView 構築をスキップ
                this.rootNodes.Clear();
                this.CanGoNext.Value = false;

                // Snackbar の本文を組み立て
                var snackbarBody = this.buildNestedArchiveWarningMessage(result.NestedArchiveFileNames);

                // Snackbar で警告を表示
                this.snackbarService.Show(
                    "製本できない素材が含まれています",
                    snackbarBody,
                    ControlAppearance.Danger,
                    new SymbolIcon { Symbol = SymbolRegular.Warning24 },
                    TimeSpan.MaxValue);
                return;
            }

            // ── サマリ集計（Materials ツリーから）──
            this.updateMaterialSummary(result.Materials);

            // ── MaterialItem → MaterialVolumeNode 変換（UIスレッドで実行）──
            this.rootNodes.Clear();
            foreach (var materialItem in result.Materials)
            {
                var rootNode = this.convertToVolumeNode(materialItem);
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
    /// <see cref="MaterialItem"/> を <see cref="MaterialVolumeNode"/> に変換します（再帰的）。
    /// </summary>
    private MaterialVolumeNode convertToVolumeNode(MaterialItem item, MaterialVolumeNode? parent = null)
    {
        var reason = string.IsNullOrEmpty(item.SelectionDisabledReason) ? null : item.SelectionDisabledReason;
        var node = new MaterialVolumeNode(
            item.Name,
            item.FullPath,
            item.ItemType,
            isSelectableByDefault: item.IsSelectableByDefault,
            selectionDisabledReason: reason)
        {
            FileSizeText = item.FileSizeText,
            FileCount = item.FileCount,
            SourcePath = item.SourcePath,
            ArchiveEntryPrefix = string.IsNullOrEmpty(item.ArchiveEntryPrefix) ? null : item.ArchiveEntryPrefix,
        };

        if (parent is not null)
            node.SetParent(parent);

        foreach (var child in item.Children)
        {
            var childNode = this.convertToVolumeNode(child, node);
            node.Children.Add(childNode);
        }

        return node;
    }

    /// <summary>
    /// Materials ツリーからサマリ情報を集計してプロパティを更新します。
    /// </summary>
    private void updateMaterialSummary(IReadOnlyList<MaterialItem> materials)
    {
        var folderCount = 0;
        var archiveCount = 0;
        long archiveTotalBytes = 0;
        var epubCount = 0;

        // Root ノード直下の子だけカウント（Root 自身は集計対象外）
        foreach (var root in materials)
        {
            foreach (var child in root.Children)
            {
                switch (child.ItemType)
                {
                    case MaterialItemType.Folder:
                        folderCount++;
                        break;
                    case MaterialItemType.Archive:
                        archiveCount++;
                        // FileSizeText から逆算せず FileInfo で取得
                        if (File.Exists(child.FullPath))
                            archiveTotalBytes += new FileInfo(child.FullPath).Length;
                        break;
                    case MaterialItemType.Epub:
                        epubCount++;
                        break;
                }
            }
        }

        // アイキャッチカード用の素材数を設定
        var materialCount = folderCount + archiveCount + epubCount;
        this.MaterialCountText.Value = $"{materialCount} 件";

        this.MaterialFolderCountText.Value = $"フォルダ：{folderCount}";

        var archiveSizeText = archiveTotalBytes == 0
            ? string.Empty
            : archiveTotalBytes >= 1024L * 1024 * 1024
                ? $"（{archiveTotalBytes / (1024.0 * 1024 * 1024):F1} GB）"
                : $"（{archiveTotalBytes / (1024.0 * 1024):F1} MB）";
        this.MaterialArchiveCountText.Value = $"圧縮ファイル：{archiveCount}{archiveSizeText}";

        this.MaterialEpubCountText.Value = $"EPUB：{epubCount}";

        this.MaterialSummaryText.Value = $"フォルダ：{folderCount}\n圧縮ファイル：{archiveCount}{archiveSizeText}\nEPUB：{epubCount}";
    }

    /// <summary>
    /// 指定ノード以下の全ノードの IsChecked 変更を購読します。
    /// </summary>
    private void subscribeCheckedNodes(IEnumerable<MaterialVolumeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == MaterialItemType.Folder || node.NodeType == MaterialItemType.Epub)
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
			this.ImageExpansionMethod.Value = 0;
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

		// フォルダが存在しない場合
		if (!exists)
		{
			this.RecreateWorkFolder.Value = false;
			this.ImageExpansionMethod.Value = 0; // 新規作成固定
		}
	}

	/// <summary>
	/// 巻フォルダ名の桁数を更新します。
	/// </summary>
	private void updateVolumeFolderDigits()
	{
		var series = this.SelectedSeries.Value;
		if (series is null)
		{
			this.VolumeFolderDigits.Value = 2; // デフォルト値
			return;
		}

		// Math.Max(2, MaxVolumeDigits) で初期値を決定
		this.VolumeFolderDigits.Value = Math.Max(2, series.MaxVolumeDigits);
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
            await ContentDialogHelper.ShowErrorAsync(
                this.contentDialogService,
                "ワークフォルダが設定されていないか、存在しません。\n設定画面で確認してください。");
            return;
        }

        // ② 製本対象0件
        if (this.bindingQueueItems.Count == 0)
        {
            await ContentDialogHelper.ShowErrorAsync(
                this.contentDialogService,
                "製本対象が選択されていません。");
            return;
        }

        // ② 巻番号未入力
        if (this.bindingQueueItems.Any(q => q.VolumeNumber.Value is null))
        {
            await ContentDialogHelper.ShowErrorAsync(
                this.contentDialogService,
                "巻番号が未入力の項目があります。");
            return;
        }

        // ③ 巻番号重複
        var numbers = this.bindingQueueItems.Select(q => q.VolumeNumber.Value!.Value).ToList();
        if (numbers.Count != numbers.Distinct().Count())
        {
            await ContentDialogHelper.ShowErrorAsync(
                this.contentDialogService,
                "巻番号が重複しています。");
            return;
        }

        // ④ 抜け巻警告
        var sorted = numbers.OrderBy(n => n).ToList();
        var hasMissing = sorted.Zip(sorted.Skip(1), (a, b) => b - a).Any(diff => diff > 1);
        if (hasMissing)
        {
            var confirmed = await ContentDialogHelper.ShowConfirmAsync(
                this.contentDialogService,
                "確認",
                "抜け巻があります。\nこのまま続行しますか？",
                "続行");
            if (!confirmed)
                return;
        }

        // 遷移処理
        // 巻フォルダ名の桁数を Workspace へ保存
        this.workspaceStore.VolumeFolderDigits = this.VolumeFolderDigits.Value;

        this.workspaceStore.SelectedMaterialVolumes.Clear();
        foreach (var item in this.bindingQueueItems)
        {
            var node = item.SourceNode;
            var volumeNumber = item.VolumeNumber.Value!.Value;
            var sourceType = node.ArchiveEntryPrefix is not null
                ? MaterialItemType.Archive
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
                OutputVolumeFolderName = this.appSettings.CreateWorkVolumeFolderName(
                    volumeNumber,
                    this.workspaceStore.VolumeFolderDigits),
                ExpectedImageFileCount = item.FileCount,
            });
        }

        this.navigationService.NavigateWithHierarchy(typeof(SeriesInspectionPage));
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

    /// <summary>
    /// NestedArchive 警告メッセージを構築します。
    /// ファイル名がある場合は名前を含め、ない場合は汎用メッセージを返します。
    /// </summary>
    private string buildNestedArchiveWarningMessage(IReadOnlyList<string> fileNames)
    {
        if (fileNames.Count == 0)
        {
            // ファイル名が取得できない場合は汎用メッセージ
            return "圧縮ファイル内に圧縮ファイルが含まれているファイルが存在します。対象のファイルを手作業で解凍してください。";
        }

        // ファイル名を含めたメッセージを構築
        var fileNameList = string.Join("\n", fileNames);
        return $"以下の圧縮ファイル内に、圧縮ファイルが含まれています。\n\n{fileNameList}\n\n上記のファイルを手作業で展開してください。";
    }
}

