using MangaBinder.Bindings.Inspection;
using MangaBinder.Controls;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using System.Diagnostics;
using System.IO;
using Wpf.Ui;
using Wpf.Ui.Controls;
using MangaBinder.Helpers;
using HalationGhost.Wpf.Ui;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程-巻選択画面の ViewModel です。
/// </summary>
public class VolumeSelectionPageViewModel : IDisposable, IDataInitializable, IBackRequestHandler
{
    /// <summary>製本後ZIPサイズの推定係数（将来的に調整可能）。</summary>
    private const double EstimatedZipSizeRatio = 1.0;

    /// <summary>作品選択状態ストア。</summary>
    private readonly SeriesWorkspaceStore workspaceStore;

    /// <summary>製本工程の正本状態ストア。</summary>
    private readonly BindingStore bindingStore;

    /// <summary>スコープファクトリー。</summary>
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <summary>ナビゲーションサービス。</summary>
    private readonly INavigationService navigationService;

    /// <summary>コンテントダイアログサービス。</summary>
    private readonly IContentDialogService contentDialogService;

    /// <summary>スナックバーサービス。</summary>
    private readonly ISnackbarService snackbarService;

    /// <summary>アプリケーション設定。</summary>
    private readonly AppSettings appSettings;

    /// <summary>サムネイル画像ローダー。</summary>
    private readonly ThumbnailImageLoader thumbnailImageLoader;

    /// <summary>ローディングサービス。</summary>
    private readonly LoadingService loadingService;

    /// <summary>巻選択画面が所有する巻情報表示用ViewModel。</summary>
    private readonly SeriesVolumeStatusViewModel volumeStatusViewModel;

    private DisposableBag disposableBag;

    /// <summary>選択中の作品名を取得します。</summary>
    public BindableReactiveProperty<string> SeriesTitle { get; }

    /// <summary>選択中の作品エンティティを取得します（サムネイル・巻数情報表示用）。</summary>
    public BindableReactiveProperty<MangaSeries?> SelectedSeries { get; }

    /// <summary>選択中の作品の巻情報表示用ViewModel を取得します。</summary>
    public BindableReactiveProperty<SeriesVolumeStatusViewModel?> SelectedSeriesVolumeStatus { get; }

    /// <summary>
    /// 作品サムネイルカード用の ViewModel を取得します。
    /// ThumbnailSource と VolumeStatus を管理し、左側パネルのサムネイルカードに使用されます。
    /// </summary>
    public MangaSeriesCardViewModel MangaSeriesCard { get; private set; }

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
    public BindableReactiveProperty<bool> RecreateWorkFolder => this.bindingStore.RecreateWorkFolder;

    /// <summary>素材展開方法の選択値を取得します（0=新規作成、1=既存を使用）。</summary>
    public BindableReactiveProperty<int> ImageExpansionMethod { get; }

    /// <summary>素材展開方法のオプション一覧を取得します。</summary>
    public List<string> ImageExpansionOptions { get; }

    /// <summary>巻フォルダ名の桁数を取得します（1, 2, 3 など）。</summary>
    public BindableReactiveProperty<int> VolumeFolderDigits => this.bindingStore.VolumeFolderDigits;

    /// <summary>巻フォルダ名の桁数選択肢一覧を取得します。</summary>
    public List<VolumeFolderDigitOption> VolumeFolderDigitOptions { get; }

    /// <summary>次工程へ進むコマンドを取得します。</summary>
    public ReactiveCommand GoNextCommand { get; }

    /// <summary>前画面へ戻るコマンドを取得します。</summary>
    public ReactiveCommand GoBackCommand { get; }

    /// <summary>指定された MaterialItemViewModel の選択状態を反転するコマンドを取得します。</summary>
    public ReactiveCommand<MaterialItemViewModel> ToggleMaterialSelectionCommand { get; }

    /// <summary>Reactive側の右ListView で現在選択中の項目を取得または設定します。</summary>
    public BindableReactiveProperty<BindingVolumeViewModel?> SelectedBindingVolume { get; }

    /// <summary>現在選択中の SelectedBindingVolume を選択巻一覧から除外するコマンドを取得します。</summary>
    public ReactiveCommand RemoveSelectedBindingVolumeCommand { get; }

    /// <summary>指定された MaterialItemViewModel を削除するコマンドを取得します。</summary>
    public ReactiveCommand<MaterialItemViewModel> DeleteMaterialCommand { get; }

    /// <summary>BindingStore.BindingVolumes の変更購読用 DisposableBag。InitializeDataAsync ごとにリセットされます。</summary>
    private DisposableBag bindingVolumesSubscriptionBag;

    /// <summary>Reactive側の新しい素材 TreeView の DragHandler を取得します。</summary>
    public MaterialItemDragHandler MaterialItemDragHandler { get; } = new MaterialItemDragHandler();

    /// <summary>Reactive側の新しい選択巻一覧の DropHandler を取得します。</summary>
    public BindingVolumeDropHandler BindingVolumeDropHandler { get; }

    /// <summary>Reactive側の新しい選択巻一覧除外領域の DropHandler を取得します。</summary>
    public BindingVolumeRemoveDropHandler BindingVolumeRemoveDropHandler { get; }

    /// <summary>Reactive側の新しい素材ツリーの ViewModel 一覧を取得します。</summary>
    public NotifyCollectionChangedSynchronizedViewList<MaterialItemViewModel> MaterialItems { get; }

    /// <summary>Reactive側の新しい選択巻一覧の ViewModel 一覧を取得します。</summary>
    public NotifyCollectionChangedSynchronizedViewList<BindingVolumeViewModel> BindingVolumes { get; }

    /// <summary>内部保持する新しい素材ツリーの synchronized view。</summary>
    private ISynchronizedView<MaterialItem, MaterialItemViewModel>? materialItemsView;

    /// <summary>内部保持する新しい選択巻一覧の synchronized view。</summary>
    private ISynchronizedView<BindingVolume, BindingVolumeViewModel>? bindingVolumesView;

    /// <summary>
    /// <see cref="VolumeSelectionPageViewModel"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="workspaceStore">作品選択状態ストア。</param>
    /// <param name="bindingStore">製本工程の正本状態ストア。</param>
    /// <param name="serviceScopeFactory">スコープファクトリー。</param>
    /// <param name="navigationService">ナビゲーションサービス。</param>
    /// <param name="contentDialogService">コンテントダイアログサービス。</param>
    /// <param name="snackbarService">スナックバーサービス。</param>
    /// <param name="appSettings">アプリケーション設定。</param>
    /// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
    /// <param name="loadingService">ローディングサービス。</param>
    public VolumeSelectionPageViewModel(
        SeriesWorkspaceStore workspaceStore,
        BindingStore bindingStore,
        IServiceScopeFactory serviceScopeFactory,
        INavigationService navigationService,
        IContentDialogService contentDialogService,
        ISnackbarService snackbarService,
        AppSettings appSettings,
        ThumbnailImageLoader thumbnailImageLoader,
        LoadingService loadingService)
    {
        this.workspaceStore = workspaceStore;
        this.bindingStore = bindingStore;
        this.serviceScopeFactory = serviceScopeFactory;
        this.navigationService = navigationService;
        this.contentDialogService = contentDialogService;
        this.snackbarService = snackbarService;
        this.appSettings = appSettings;
        this.thumbnailImageLoader = thumbnailImageLoader;
        this.loadingService = loadingService;

        // 巻選択画面が所有する巻情報表示用ViewModel を生成
        this.volumeStatusViewModel = new SeriesVolumeStatusViewModel()
            .AddTo(ref this.disposableBag);

        this.SeriesTitle = new BindableReactiveProperty<string>(string.Empty)
            .AddTo(ref this.disposableBag);
        this.SelectedSeries = new BindableReactiveProperty<MangaSeries?>(null)
            .AddTo(ref this.disposableBag);
        this.SelectedSeriesVolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel?>(null)
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
        this.GoBackCommand.Subscribe(_ => this.executeGoBack())
            .AddTo(ref this.disposableBag);

        this.ToggleMaterialSelectionCommand = new ReactiveCommand<MaterialItemViewModel>()
            .AddTo(ref this.disposableBag);
        this.ToggleMaterialSelectionCommand.Subscribe(item => this.toggleMaterialSelection(item))
            .AddTo(ref this.disposableBag);

        // Reactive側の新除外操作コマンドを初期化
        this.SelectedBindingVolume = new BindableReactiveProperty<BindingVolumeViewModel?>(null)
            .AddTo(ref this.disposableBag);

        this.RemoveSelectedBindingVolumeCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.RemoveSelectedBindingVolumeCommand.Subscribe(_ =>
        {
            var selectedVolume = this.SelectedBindingVolume.Value;
            if (selectedVolume is not null)
            {
                this.removeBindingVolume(selectedVolume);
            }
        })
        .AddTo(ref this.disposableBag);

        this.DeleteMaterialCommand = new ReactiveCommand<MaterialItemViewModel>()
            .AddTo(ref this.disposableBag);
        this.DeleteMaterialCommand.Subscribe(item => this.executeDeleteMaterialAsync(item))
            .AddTo(ref this.disposableBag);

        // Reactive側の新素材ツリーを初期化
        // BindingStore.Materials から MaterialItemViewModel へ変換
        var materialItemsView = this.bindingStore.Materials
            .CreateView(material => new MaterialItemViewModel(material));

        this.materialItemsView = materialItemsView;

        this.MaterialItems = materialItemsView
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        // ViewChanged イベントで削除・置換・ソート時に ViewModel を Dispose
        materialItemsView.ViewChanged += this.onMaterialItemsViewChanged;

        // Reactive側の新選択巻一覧を初期化
        // BindingStore.BindingVolumes から BindingVolumeViewModel へ変換
        var bindingVolumesView = this.bindingStore.BindingVolumes
            .CreateView(bindingVolume => new BindingVolumeViewModel(bindingVolume));

        this.bindingVolumesView = bindingVolumesView;

        this.BindingVolumes = bindingVolumesView
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
            .AddTo(ref this.disposableBag);

        // MangaSeriesCard: 作品サムネイルカード用ViewModel
        this.MangaSeriesCard = new MangaSeriesCardViewModel()
            .AddTo(ref this.disposableBag);

        // 素材展開方法の選択変更を監視し、RecreateWorkFolder を更新
        this.ImageExpansionMethod.Subscribe(selectedIndex =>
        {
            // RecreateWorkFolder == true となる条件は、
            // HasExistingWorkFolder == true かつ ImageExpansionMethod == 0 の場合だけ
            this.bindingStore.RecreateWorkFolder.Value =
                this.HasExistingWorkFolder.Value && selectedIndex == 0;
        }).AddTo(ref this.disposableBag);

        // SelectedSeries 変更時に同期
        this.SelectedSeries.Subscribe(series =>
        {
            if (series is not null)
            {
                // 巻選択画面が所有する SeriesVolumeStatusViewModel に series を設定
                this.volumeStatusViewModel.Series.Value = series;

                // ThumbnailImageLoader で最終表示用 ImageSource を取得
                var imageSource = this.thumbnailImageLoader.Load(series);

                // MangaSeriesCard へ設定
                this.MangaSeriesCard.ThumbnailSource.Value = imageSource;
                this.MangaSeriesCard.VolumeStatus.Value = this.volumeStatusViewModel;

                // MangaSeriesCard の Series へ接続
                if (this.MangaSeriesCard.Series.Value != series)
                {
                    this.MangaSeriesCard.Series.Value = series;
                }
                else if (this.MangaSeriesCard.Series.Value == series)
                {
                    // 同一インスタンスの場合は ForceNotify() で再通知させる
                    this.MangaSeriesCard.Series.ForceNotify();
                }

                // 既存 Binding との互換性: SelectedSeriesVolumeStatus に同一インスタンスを参照させる
                this.SelectedSeriesVolumeStatus.Value = this.volumeStatusViewModel;
            }
            else
            {
                // series が null の場合はクリア
                this.volumeStatusViewModel.Series.Value = null;
                this.MangaSeriesCard.ThumbnailSource.Value = null;
                this.MangaSeriesCard.VolumeStatus.Value = null;
                this.MangaSeriesCard.Series.Value = null;

                // 既存 Binding との互換性: SelectedSeriesVolumeStatus もクリア
                this.SelectedSeriesVolumeStatus.Value = null;
            }
        }).AddTo(ref this.disposableBag);

        // Reactive側の新しいD&D Handlerを初期化
        this.BindingVolumeDropHandler = new BindingVolumeDropHandler(
            item => this.selectMaterialFromDrop(item),
            (item, newIndex) => this.moveBindingVolumeFromDrop(item, newIndex));

        this.BindingVolumeRemoveDropHandler = new BindingVolumeRemoveDropHandler(
            item => this.removeBindingVolume(item));
    }

    /// <inheritdoc/>
    public async ValueTask InitializeDataAsync()
    {
        // ① Singleton VM の前回表示状態をリセット
        // UI 状態をリセット
        this.SeriesTitle.Value = string.Empty;
        this.SelectedSeries.Value = null;
        this.SelectedBindingVolume.Value = null;

        // 素材サマリをリセット
        this.MaterialSummaryText.Value = string.Empty;
        this.MaterialCountText.Value = "0 件";
        this.MaterialFolderCountText.Value = string.Empty;
        this.MaterialArchiveCountText.Value = string.Empty;
        this.MaterialEpubCountText.Value = string.Empty;

        // 選択巻サマリと GoNext をリセット
        this.SelectedVolumeSummaryText.Value = string.Empty;
        this.CanGoNext.Value = false;

        // BindingStore.BindingVolumes の購読をリセット
        this.bindingVolumesSubscriptionBag.Dispose();
        this.bindingVolumesSubscriptionBag = new DisposableBag();

        // BindingStore.BindingVolumes の変更を監視して CanGoNext と SelectedVolumeSummaryText を自動更新
        // 購読開始は BindingTarget の取得やInitializeDataAsync の後処理より前に行い、
        // Manager.InitializeAsync() による初期化・Clear に追従できるようにする
        this.bindingStore.BindingVolumes.ObserveCountChanged()
            .Subscribe(_ =>
            {
                this.updateCanGoNext();
                this.updateSelectedVolumeSummary();
            })
            .AddTo(ref this.bindingVolumesSubscriptionBag);

        // 購読開始直後に現在値を明示反映
        // BindingVolumes が0件の場合でも "0 MB" / false の正しい状態が確定する
        this.updateCanGoNext();
        this.updateSelectedVolumeSummary();

        // ② BindingStore.BindingTarget から BindingSeries を取得
        var bindingTarget = this.bindingStore.BindingTarget.Value;
        if (bindingTarget is null)
        {
            // BindingTarget が無い場合は初期化完了
            this.updateWorkFolderState();
            this.updateVolumeFolderDigits();
            return;
        }

        // ③ BindingSeries から MangaSeries を取得
        var series = bindingTarget.Series;
        if (series is null)
        {
            // Series が取得できない場合も初期化完了
            this.updateWorkFolderState();
            this.updateVolumeFolderDigits();
            return;
        }

        // ④ Series が存在する場合は UIを設定
        this.SeriesTitle.Value = series.Title;
        this.SelectedSeries.Value = series;

        // ⑤ WorkFolder 設定と VolumeFolderDigits を初期化
        this.updateWorkFolderState();
        this.updateVolumeFolderDigits();

        // ⑥ VolumeSelectionManager で新 Reactive 側の素材初期化を実行
        await this.initializeMaterialsAsync();
    }

    /// <summary>
    /// 素材フォルダを非同期で読み込みます。
    /// </summary>
    /// <inheritdoc/>
    public void Dispose()
    {
        // MaterialItems の全子 ViewModel を破棄
        foreach (var viewModel in this.MaterialItems)
        {
            viewModel?.Dispose();
        }

        // materialItemsView への参照をクリア
        this.materialItemsView = null;

        // BindingVolumesView の参照をクリア
        // BindingVolumeViewModel は BindingVolume を所有しないため、
		// ここでは参照をクリアするだけで十分
		this.bindingVolumesView = null;

		// BindingVolumes の購読を破棄
		this.bindingVolumesSubscriptionBag.Dispose();

		this.disposableBag.Dispose();
	}

	/// <summary>
	/// 作品中間フォルダの存在状態を更新します。
	/// </summary>
	private void updateWorkFolderState()
	{
		var series = this.SelectedSeries.Value;

		if (series is null || !this.appSettings.HasValidWorkFolder)
		{
			// ケース1: series == null または HasValidWorkFolder == false
			this.HasExistingWorkFolder.Value = false;
			this.ImageExpansionMethod.Value = 0;
			this.bindingStore.RecreateWorkFolder.Value = false;
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

		if (!exists)
		{
			// ケース2: Work設定は有効だが、対象作品のWork作品フォルダが存在しない
			this.HasExistingWorkFolder.Value = false;
			this.ImageExpansionMethod.Value = 0;
			this.bindingStore.RecreateWorkFolder.Value = false;
		}
		else
		{
			// ケース3: 対象作品のWork作品フォルダが存在する
			// 既存の画像を使用する（ImageExpansionMethod = 1）をデフォルトに
			this.HasExistingWorkFolder.Value = true;
			this.ImageExpansionMethod.Value = 1;
			this.bindingStore.RecreateWorkFolder.Value = false;
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
			this.bindingStore.VolumeFolderDigits.Value = 2; // デフォルト値
			return;
		}

		// Math.Max(2, MaxVolumeDigits) で初期値を決定
		this.bindingStore.VolumeFolderDigits.Value = Math.Max(2, series.MaxVolumeDigits);
	}

	/// <summary>
	/// CanGoNext を更新します。
	/// </summary>
	private void updateCanGoNext()
		=> this.CanGoNext.Value = this.bindingStore.BindingVolumes.Count > 0;

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

        // ② 製本対象0件（BindingStore.BindingVolumes を正本に変更）
        if (this.bindingStore.BindingVolumes.Count == 0)
        {
            await ContentDialogHelper.ShowErrorAsync(
                this.contentDialogService,
                "製本対象が選択されていません。");
            return;
        }

        // ③ 巻番号未入力（BindingStore.BindingVolumes から確認）
        if (this.bindingStore.BindingVolumes.Any(volume => volume.VolumeNumber.Value is null))
        {
            await ContentDialogHelper.ShowErrorAsync(
                this.contentDialogService,
                "巻番号が未入力の項目があります。");
            return;
        }

        // ④ 巻番号重複（BindingStore.BindingVolumes から確認）
        var numbers = this.bindingStore.BindingVolumes
            .Select(volume => volume.VolumeNumber.Value!.Value)
            .ToList();
        if (numbers.Count != numbers.Distinct().Count())
        {
            // 重複している巻番号を抽出
            var duplicateNumbers = numbers
                .GroupBy(n => n)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(n => n)
                .ToList();

            // メッセージを構築
            var duplicateText = string.Join(", ", duplicateNumbers.Select(n => $"{n:0.#}巻"));
            var snackbarMessage = $"巻番号が重複しています：{duplicateText}";

            this.snackbarService.Show(
                "巻番号が重複しています",
                snackbarMessage,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.Warning24 },
                TimeSpan.MaxValue);
            return;
        }

        // ⑤ 抜け巻警告（BindingStore.BindingVolumes から確認）
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

        // ⑥ BindingStore.BindingVolumes から BindingSourceVolume へ変換（現在順を保持）
        // ローカルで変換結果を構築し、すべての変換が成功してから
        // SeriesWorkspaceStore へ互換出力する
        var outgoingVolumes = new List<BindingSourceVolume>();
        foreach (var volume in this.bindingStore.BindingVolumes)
        {
            var material = volume.Material;
            var volumeNumber = volume.VolumeNumber.Value!.Value;

            // SourceType の決定ルール：
            // ArchiveEntryPrefix が空文字でない → Archive
            // ItemType == Epub → Epub
            // それ以外 → Folder
            var sourceType = material.ArchiveEntryPrefix != string.Empty
                ? MaterialItemType.Archive
                : (material.ItemType == MaterialItemType.Epub
                    ? MaterialItemType.Epub
                    : MaterialItemType.Folder);

            outgoingVolumes.Add(new BindingSourceVolume
            {
                DisplayName = material.Name,
                VolumeNumber = volumeNumber,
                NodeType = material.ItemType,
                SourceType = sourceType,
                SourcePath = material.SourcePath,
                ArchiveEntryPrefix = material.ArchiveEntryPrefix != string.Empty ? material.ArchiveEntryPrefix : null,
                FullPath = material.FullPath,
                OutputVolumeFolderName = this.appSettings.CreateWorkVolumeFolderName(
                    volumeNumber,
                    this.bindingStore.VolumeFolderDigits.Value),
                ExpectedImageFileCount = material.FileCount,
            });
        }

        // ⑦ すべての変換が成功したら SeriesWorkspaceStore へ互換出力
        this.workspaceStore.RecreateWorkFolder.Value = this.bindingStore.RecreateWorkFolder.Value;
        this.workspaceStore.VolumeFolderDigits = this.bindingStore.VolumeFolderDigits.Value;

        this.workspaceStore.SelectedMaterialVolumes.Clear();
        foreach (var sourceVolume in outgoingVolumes)
        {
            this.workspaceStore.SelectedMaterialVolumes.Add(sourceVolume);
        }

        // ⑧ 遷移
        this.navigationService.NavigateWithHierarchy(typeof(SeriesInspectionPage));
    }

    /// <summary>
    /// 指定された MaterialItemViewModel の選択状態を反転します。
    /// VolumeSelectionManager をスコープ内で解決して選択操作を実行します。
    /// </summary>
    private void toggleMaterialSelection(MaterialItemViewModel item)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
        manager.ToggleMaterialSelection(item.Material, item.IsSelectionOverrideEnabled.Value);
    }

    /// <summary>
    /// 指定された BindingVolumeViewModel を選択巻一覧から除外します。
    /// VolumeSelectionManager をスコープ内で解決して除外操作を実行します。
    /// </summary>
    private void removeBindingVolume(BindingVolumeViewModel item)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
        manager.UnselectMaterial(item.Material);
    }

    /// <summary>
    /// D&D により TreeView から選択巻一覧へドロップされた MaterialItemViewModel を処理します。
    /// VolumeSelectionManager.SelectMaterial() を呼び出して選択します。
    /// </summary>
    /// <param name="item">ドロップされた MaterialItemViewModel。</param>
    private void selectMaterialFromDrop(MaterialItemViewModel item)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
        manager.SelectMaterial(item.Material, item.IsSelectionOverrideEnabled.Value);
    }

    /// <summary>
    /// D&D により選択巻一覧内で並び替えされた BindingVolumeViewModel を処理します。
    /// VolumeSelectionManager.MoveBindingVolume() を呼び出して移動します。
    /// </summary>
    /// <param name="item">移動対象の BindingVolumeViewModel。</param>
    /// <param name="newIndex">移動先のインデックス。</param>
    private void moveBindingVolumeFromDrop(BindingVolumeViewModel item, int newIndex)
    {
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();
        manager.MoveBindingVolume(item.BindingVolume, newIndex);
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

    /// <summary>
    /// 選択済み巻の合計サイズから推定ZIPサイズを計算し、SelectedVolumeSummaryText を更新します。
    /// </summary>
    private void updateSelectedVolumeSummary()
    {
        // 選択済み巻（BindingStore.BindingVolumes）の TotalImageBytes を合計
        var totalBytes = this.bindingStore.BindingVolumes
            .Sum(volume => volume.Material.TotalImageBytes);

        if (totalBytes == 0)
        {
            this.SelectedVolumeSummaryText.Value = "0 MB";
            return;
        }

        // 推定ZIPサイズを計算
        var estimatedBytes = (long)(totalBytes * EstimatedZipSizeRatio);

        // サイズを 1024 ベースでフォーマット
        var sizeText = string.Empty;

        if (estimatedBytes >= 1024L * 1024 * 1024)
        {
            // 1GB以上はGB表示（小数1桁）
            var sizeGB = estimatedBytes / (1024.0 * 1024 * 1024);
            sizeText = $"{sizeGB:F1} GB";
        }
        else
        {
            // 1GB未満はMB表示（小数なし）
            var sizeMB = estimatedBytes / (1024.0 * 1024);
            sizeText = $"{(long)sizeMB} MB";
        }

        this.SelectedVolumeSummaryText.Value = sizeText;
    }

    /// <summary>
    /// MaterialItems の ViewChanged イベント ハンドラ。
    /// ISynchronizedView での削除・置換・ソート時に MaterialItemViewModel を適切に Dispose する。
    /// </summary>
    private void onMaterialItemsViewChanged(in SynchronizedViewChangedEventArgs<MaterialItem, MaterialItemViewModel> e)
    {
        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                // 削除された MaterialItemViewModel を Dispose
                if (e.IsSingleItem)
                {
                    e.OldItem.View?.Dispose();
                }
                else
                {
                    foreach (var viewModel in e.OldViews)
                    {
                        viewModel?.Dispose();
                    }
                }
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                // 置換前の MaterialItemViewModel を Dispose
                if (e.IsSingleItem)
                {
                    e.OldItem.View?.Dispose();
                }
                else
                {
                    foreach (var viewModel in e.OldViews)
                    {
                        viewModel?.Dispose();
                    }
                }
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                // Reset は Sort / Reverse / Clear の場合が考えられる
                // IsClear で判定し、Clear の場合だけ旧 MaterialItemViewModel を Dispose
                if (e.SortOperation.IsClear)
                {
                    foreach (var viewModel in e.OldViews)
                    {
                        viewModel?.Dispose();
                    }
                }
                // Sort / Reverse の場合は現在有効な MaterialItemViewModel を Dispose しない
                break;
        }
    }

    /// <summary>
    /// VolumeSelectionManager を使用して素材を初期化します。
    /// LoadingService でロードを表示しながら、method-local scope で Manager を解決します。
    /// </summary>
    private async ValueTask initializeMaterialsAsync()
    {
        using (this.loadingService.Begin("素材を解析しています..."))
        {
            // UI描画機会を確保
            await Task.Yield();

            // method-local DI scope で VolumeSelectionManager を解決
            using var scope = this.serviceScopeFactory.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();

            // Manager.InitializeAsync() を呼び出して素材初期化
            var result = await manager.InitializeAsync(CancellationToken.None);

            // 初期化結果をチェックしてエラー処理・警告を実行
            if (result.Status == MaterialFolderStatus.DriveNotReady)
            {
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
            }

            if (result.Status == MaterialFolderStatus.NoMaterialSource ||
                result.Status == MaterialFolderStatus.MaterialSourceNotFound)
            {
                this.snackbarService.Show(
                    "素材フォルダが見つかりません",
                    "素材フォルダが存在しません。",
                    ControlAppearance.Danger,
                    new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                    TimeSpan.MaxValue);
                return;
            }

            // NestedArchive 警告
            if (result.HasNestedArchive)
            {
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

            // Success の場合は BindingStore.Materials が既に更新されており、
            // MaterialItems（Projection）が自動的に更新される
            // 素材サマリを計算して表示用プロパティを更新
            await this.updateMaterialSummaryAsync();
        }
    }

    /// <summary>
    /// 指定された MaterialItemViewModel を削除するための非同期処理を実行します。
    /// ContentDialog で削除方法を確認し、VolumeSelectionManager.DeleteMaterial を呼び出します。
    /// </summary>
    /// <param name="item">削除対象の MaterialItemViewModel。</param>
    private async void executeDeleteMaterialAsync(MaterialItemViewModel item)
    {
        // 対象確認
        if (item is null || !item.CanDeleteMaterial)
        {
            return;
        }

        // ContentDialog を生成
        var dialog = new ContentDialog
        {
            Title = "素材を削除",
            Content = $"「{item.Material.Name}」を削除します。\n\n" +
                      "製本対象の巻に含まれている場合は、製本対象からも削除されます。",
            PrimaryButtonText = "完全に削除する",
            SecondaryButtonText = "ごみ箱に入れる",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary
        };

        // ContentDialog を表示
        var result = await this.contentDialogService.ShowAsync(
            dialog,
            CancellationToken.None);

        // ユーザーが選択した削除方法を決定
        bool sendToRecycleBin;
        switch (result)
        {
            case ContentDialogResult.Primary:
                // 完全削除
                sendToRecycleBin = false;
                break;

            case ContentDialogResult.Secondary:
                // ごみ箱へ移動
                sendToRecycleBin = true;
                break;

            case ContentDialogResult.None:
            default:
                // キャンセル
                return;
        }

        // Method-local scope で VolumeSelectionManager を取得
        using var scope = this.serviceScopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();

        // DeleteMaterial() を実行
        var succeeded = manager.DeleteMaterial(item.Material, sendToRecycleBin);

        // 結果に応じて処理
        if (!succeeded)
        {
            // 削除失敗時は Snackbar を表示
            this.snackbarService.Show(
                "素材を削除できませんでした",
                Constants.Messages.MaterialInUse,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
                TimeSpan.MaxValue);
            return;
        }

        // 削除成功時は成功通知なし、素材内訳を更新
        await this.updateMaterialSummaryAsync();
    }

    /// <summary>
    /// BindingStore.Materials から素材サマリを計算し、表示用プロパティを更新します。
    /// Root直下の子要素のみを集計対象とします。
    /// </summary>
    private async ValueTask updateMaterialSummaryAsync()
    {
        // Root直下の子要素を取得
        var rootChildren = this.bindingStore.Materials.SelectMany(root => root.Children).ToList();

        // ItemType ごとに分類
        var folderCount = rootChildren.Count(item => item.ItemType == MaterialItemType.Folder);
        var archiveCount = rootChildren.Count(item => item.ItemType == MaterialItemType.Archive);
        var epubCount = rootChildren.Count(item => item.ItemType == MaterialItemType.Epub);
        var totalCount = folderCount + archiveCount + epubCount;

        // 圧縮ファイルサイズを計算
        var archivePaths = rootChildren
            .Where(item => item.ItemType == MaterialItemType.Archive)
            .Select(item => item.FullPath)
            .ToList();

        var archiveTotalBytes = await StorageSizeHelper.GetArchiveOnlyAsync(archivePaths, CancellationToken.None);
        var archiveSizeText = archiveTotalBytes > 0
            ? $"（{StorageSizeHelper.FormatSize(archiveTotalBytes)}）"
            : string.Empty;

        // 各表示テキストを生成
        this.MaterialCountText.Value = $"{totalCount} 件";
        this.MaterialFolderCountText.Value = $"フォルダ：{folderCount}";
        this.MaterialArchiveCountText.Value = $"圧縮ファイル：{archiveCount}{archiveSizeText}";
        this.MaterialEpubCountText.Value = $"EPUB：{epubCount}";

        // MaterialSummaryText を各個別サマリから生成
        this.MaterialSummaryText.Value =
            $"フォルダ：{folderCount}\n" +
            $"圧縮ファイル：{archiveCount}{archiveSizeText}\n" +
            $"EPUB：{epubCount}";
    }

    /// <summary>
    /// ナビゲーション戻る処理を実行します。
    /// </summary>
    private void executeGoBack()
    {
        this.navigationService.GoBack();
    }

    /// <inheritdoc/>
    public async ValueTask OnBackRequestedAsync()
    {
        await ValueTask.CompletedTask;
        this.executeGoBack();
    }
}
