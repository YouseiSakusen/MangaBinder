using MangaBinder.Bindings;
using MangaBinder.Controls;
using MangaBinder.Core.Series;
using MangaBinder.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;
using ObservableCollections;
using R3;
using Reactive.Bindings.R3;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows.Media.Imaging;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Microsoft.Win32;

namespace MangaBinder.Series;

/// <summary>
/// 編集ページの ViewModel です。
/// </summary>
public class EditorPageViewModel : IDataInitializable, INavigationLeavingAware, IDisposable
{
	private readonly MangaSeriesManager seriesManager;
	private readonly SeriesWorkspaceStore workspaceStore;
	private readonly IContentDialogService contentDialogService;
	private readonly INavigationService navigationService;
	private readonly MangaSeriesStore mangaSeriesStore;
	private readonly ThumbnailPicker thumbnailPicker;
	private readonly ISnackbarService snackbarService;
	private readonly AppSettings appSettings;
	private readonly MaterialManager materialManager;
	private readonly OwnedVolumeEstimator ownedVolumeEstimator;
	private SeriesTagSelectorViewModel tagSelector = null!;
	private DisposableBag disposableBag;

	/// <summary>
	/// 次回のタイトル LostFocus 時に再判定が必要かどうかを表します。
	/// showDifferentSeriesDialogAsync() で「タイトルを再入力」が選択された場合に true に設定されます。
	/// </summary>
	private bool needsTitleRevalidation;

	/// <summary>編集対象の Series を取得します。</summary>
	public BindableReactiveProperty<MangaSeries?> EditingSeries { get; }

	/// <summary>
	/// タグ選択・表示状態を管理する ViewModel です。
	/// </summary>
	public SeriesTagSelectorViewModel TagSelector => this.tagSelector;

	/// <summary>タイトルを取得または設定します。バリデーション機能付き。</summary>
	public ValidatableReactiveProperty<string?> Title { get; }

	/// <summary>1件ヒット時の既存作品候補を取得します。</summary>
	public BindableReactiveProperty<MangaSeries?> DuplicateSeriesFound { get; }

	/// <summary>作者を取得または設定します。</summary>
	public BindableReactiveProperty<string?> Author { get; }

	/// <summary>出版社を取得または設定します。</summary>
	public BindableReactiveProperty<string?> Publisher { get; }

	/// <summary>開始巻を取得または設定します。</summary>
	public BindableReactiveProperty<double> StartVolume { get; }

	/// <summary>完結巻を取得または設定します。null の場合は未入力です。</summary>
	public BindableReactiveProperty<double?> EndVolume { get; }

	/// <summary>全巻所持しているかどうかを取得または設定します。</summary>
	public BindableReactiveProperty<bool> IsOwnedCompleted { get; }

	/// <summary>所持推定巻数を取得または設定します。</summary>
	public BindableReactiveProperty<double?> OwnedMaxVolume { get; }

	/// <summary>説明を取得または設定します。</summary>
	public BindableReactiveProperty<string?> Description { get; }

	/// <summary>メモを取得または設定します。</summary>
	public BindableReactiveProperty<string?> Memo { get; }

	/// <summary>全巻所持が編集可能かどうかを取得または設定します。完結巻が null ではない場合のみ編集可能です。</summary>
	public BindableReactiveProperty<bool> CanEditOwnedCompleted { get; }

	/// <summary>タイトル入力欄へのフォーカス要求を取得します。</summary>
	public BindableReactiveProperty<int> TitleFocusRequest { get; }

	/// <summary>素材ファイル一覧を取得します。</summary>
	public ObservableCollection<MaterialFileItemViewModel> MaterialFiles { get; }

	/// <summary>素材ファイル一覧のヘッダー表示用文字列を取得します。</summary>
	public BindableReactiveProperty<string> MaterialFilesDisplay { get; }

	/// <summary>サマリカード用の素材ファイル数表示テキストを取得します。</summary>
	public BindableReactiveProperty<string> MaterialFileCountText { get; }

	/// <summary>素材ファイルが空かどうかを取得します。EmptyState の表示制御に使用します。</summary>
	public BindableReactiveProperty<bool> IsMaterialFilesEmpty { get; }

	/// <summary>素材ファイルが存在するかどうかを取得します。ListView の表示制御に使用します。</summary>
	public BindableReactiveProperty<bool> HasMaterialFiles { get; }

	/// <summary>完結巻入力中テキストを処理し、CanEditOwnedCompleted を制御するコマンドを取得します。</summary>
	public ReactiveCommand<string?> EndVolumeTextInputCommand { get; }

	/// <summary>巻情報編集用の ViewModel を取得します。</summary>
	public EditorSeriesVolumeStatusViewModel VolumeStatus { get; }

	/// <summary>
	/// サムネイルプレビュー画像を取得または設定します。
	/// クリップボードから貼り付けた画像が優先表示されます。
	/// null の場合は、既存のサムネイル（EditingSeries.Value のサムネイル）が表示されます。
	/// </summary>
	public BindableReactiveProperty<BitmapSource?> ThumbnailPreviewImageSource { get; }

	/// <summary>
	/// 貼り付けたサムネイルの PNG byte[] を取得します。
	/// 保存時に利用するための一時保持です。
	/// </summary>
	private byte[]? PastedThumbnailBytes { get; set; }

	/// <summary>
	/// クリップボードからサムネイルを貼り付けるコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> PasteThumbnailCommand { get; }

	/// <summary>
	/// ファイルから画像を選択してサムネイルを設定するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> SelectThumbnailCommand { get; }

	/// <summary>
	/// 編集画面を閉じてメイン画面へ戻るコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> BackCommand { get; }

	/// <summary>
	/// 作品を WorkMangaSeries へ一時保存するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> SaveWorkSeriesCommand { get; }

	/// <summary>
	/// 一時保存ボタンの有効/無効状態を取得します。
	/// 新規作品または登録待ち作品で、かつ今回の編集セッションで追加した素材がない場合に有効です。
	/// </summary>
	public BindableReactiveProperty<bool> SaveWorkSeriesCommandCanExecute { get; }

	/// <summary>
	/// ファイル/フォルダをドラッグアンドドロップで追加するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<string[]> DropFilesCommand { get; }

	/// <summary>
	/// 素材ファイルドラッグオーバー中かどうかを取得または設定します。
	/// Behavior の IsDragOver と双方向バインドされます。
	/// </summary>
	public BindableReactiveProperty<bool> IsMaterialDragOver { get; }

	/// <summary>
	/// 登録ボタンの有効/無効状態を取得します。
	/// 素材ファイルがある場合に有効になります。
	/// </summary>
	public BindableReactiveProperty<bool> RegisterSeriesCommandCanExecute { get; }

	/// <summary>
	/// 作品を正式に MangaSeries へ登録するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> RegisterSeriesCommand { get; }

	/// <summary>
	/// 登録先に選択可能な素材フォルダ一覧を取得します。
	/// </summary>
	public ObservableList<SourceFolder> MaterialSourceFolders { get; }

	/// <summary>
	/// 登録先に選択された素材フォルダを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<SourceFolder?> SelectedMaterialSourceFolder { get; }

	/// <summary>
	/// 登録先素材フォルダを変更可能かどうかを取得します。
	/// 新規作品・登録待ち作品の場合は true、既存作品の場合は false です。
	/// </summary>
	public BindableReactiveProperty<bool> CanSelectMaterialSourceFolder { get; }

	/// <summary>
	/// ファイル選択ダイアログから素材ファイルを追加するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> AddMaterialFileCommand { get; }

	/// <summary>
	/// フォルダ選択ダイアログから素材フォルダを追加するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> AddMaterialFolderCommand { get; }

	/// <summary>
	/// タイトル欄がフォーカスを失ったことを通知するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> TitleLostFocusCommand { get; }

	/// <summary>
	/// EditorPageViewModel の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="seriesManager">作品管理マネージャー。</param>
	/// <param name="workspaceStore">作業領域ストア。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="mangaSeriesStore">作品タグストア。</param>
	/// <param name="thumbnailPicker">サムネイル操作ピッカー。</param>
	/// <param name="snackbarService">Snackbar サービス。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="materialManager">素材パス解析マネージャー。</param>
	/// <param name="ownedVolumeEstimator">所持推定計算機。</param>
	public EditorPageViewModel(
		MangaSeriesManager seriesManager,
		SeriesWorkspaceStore workspaceStore,
		IContentDialogService contentDialogService,
		INavigationService navigationService,
		MangaSeriesStore mangaSeriesStore,
		ThumbnailPicker thumbnailPicker,
		ISnackbarService snackbarService,
		AppSettings appSettings,
		MaterialManager materialManager,
		OwnedVolumeEstimator ownedVolumeEstimator)
	{
		this.seriesManager = seriesManager ?? throw new ArgumentNullException(nameof(seriesManager));
		this.workspaceStore = workspaceStore ?? throw new ArgumentNullException(nameof(workspaceStore));
		this.contentDialogService = contentDialogService ?? throw new ArgumentNullException(nameof(contentDialogService));
		this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
		this.mangaSeriesStore = mangaSeriesStore ?? throw new ArgumentNullException(nameof(mangaSeriesStore));
		this.thumbnailPicker = thumbnailPicker ?? throw new ArgumentNullException(nameof(thumbnailPicker));
		this.snackbarService = snackbarService ?? throw new ArgumentNullException(nameof(snackbarService));
		this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
		this.materialManager = materialManager ?? throw new ArgumentNullException(nameof(materialManager));
		this.ownedVolumeEstimator = ownedVolumeEstimator ?? throw new ArgumentNullException(nameof(ownedVolumeEstimator));

		this.EditingSeries = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		// TagSelector の初期化
		this.tagSelector = new SeriesTagSelectorViewModel(mangaSeriesStore)
			.AddTo(ref this.disposableBag);

		this.DuplicateSeriesFound = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		// DuplicateSeriesFound が非 null に更新された場合、既存作品ダイアログを表示
		this.DuplicateSeriesFound
			.Subscribe(duplicateSeries =>
			{
				if (duplicateSeries != null)
				{
					// 二重実行を防ぐため、即座に DuplicateSeriesFound をクリア
					this.DuplicateSeriesFound.Value = null;
					// 非同期で dialog を表示
					_ = this.showExistingSeriesDialogAsync(duplicateSeries);
				}
			})
			.AddTo(ref this.disposableBag);

		this.Title = new ValidatableReactiveProperty<string?>(null)
			.SetValidateNotifyError(this.validateTitle)
			.AddTo(ref this.disposableBag);

		this.Author = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		this.Publisher = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		this.StartVolume = new BindableReactiveProperty<double>(1.0)
			.AddTo(ref this.disposableBag);

		this.EndVolume = new BindableReactiveProperty<double?>(null)
			.AddTo(ref this.disposableBag);

		this.IsOwnedCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.OwnedMaxVolume = new BindableReactiveProperty<double?>(null)
			.AddTo(ref this.disposableBag);

		this.Description = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		this.Memo = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		// CanEditOwnedCompleted: EndVolume が null ではない場合のみ true
		this.CanEditOwnedCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// EndVolume の変更を監視し、CanEditOwnedCompleted を制御
		this.EndVolume
			.Subscribe(endVolume =>
			{
				if (endVolume == null)
				{
					// 完結巻が null になった場合
					this.CanEditOwnedCompleted.Value = false;
					this.IsOwnedCompleted.Value = false;
				}
				else
				{
					// 完結巻が入力された場合
					this.CanEditOwnedCompleted.Value = true;
				}
			})
			.AddTo(ref this.disposableBag);

		this.TitleFocusRequest = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		var materialFilesSource = new ObservableCollection<MaterialFileItemViewModel>();
		this.MaterialFiles = materialFilesSource;

		// MaterialFilesDisplay: MaterialFiles.Count に基づいて表示文字列を生成
		this.MaterialFilesDisplay = new BindableReactiveProperty<string>(this.getMaterialFilesDisplayText())
			.AddTo(ref this.disposableBag);

		// MaterialFileCountText: サマリカード用の素材ファイル数表示テキスト
		this.MaterialFileCountText = new BindableReactiveProperty<string>(this.getMaterialFileCountText())
			.AddTo(ref this.disposableBag);

		// IsMaterialFilesEmpty: EmptyState 表示制御用
		this.IsMaterialFilesEmpty = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		// HasMaterialFiles: ListView 表示制御用
		this.HasMaterialFiles = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// MaterialFiles の変更を監視して更新
		void OnMaterialFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			var isEmpty = this.MaterialFiles.Count == 0;
			this.MaterialFilesDisplay.Value = this.getMaterialFilesDisplayText();
			this.MaterialFileCountText.Value = this.getMaterialFileCountText();
			this.IsMaterialFilesEmpty.Value = isEmpty;
			this.HasMaterialFiles.Value = !isEmpty;
		}

		this.MaterialFiles.CollectionChanged += OnMaterialFilesCollectionChanged;

		// EndVolumeTextInputCommand: 完結巻入力中テキストを処理するコマンド
		this.EndVolumeTextInputCommand = new ReactiveCommand<string?>()
			.AddTo(ref this.disposableBag);
		this.EndVolumeTextInputCommand.Subscribe(text =>
		{
			this.HandleEndVolumeTextInput(text);
		});

		// ThumbnailPreviewImageSource: プレビュー用画像ソース
		this.ThumbnailPreviewImageSource = new BindableReactiveProperty<BitmapSource?>(null)
			.AddTo(ref this.disposableBag);

		// PasteThumbnailCommand: クリップボードからサムネイルを貼り付けるコマンド
		this.PasteThumbnailCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.PasteThumbnailCommand.Subscribe(_ =>
		{
			this.PasteThumbnailAsync();
		});

		// SelectThumbnailCommand: ファイルから画像を選択するコマンド
		this.SelectThumbnailCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.SelectThumbnailCommand.Subscribe(async _ =>
		{
			await this.SelectThumbnailAsync();
		});

		// BackCommand: 戻るコマンド
		this.BackCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.BackCommand.Subscribe(_ =>
		{
			this.navigationService.GoBack();
		});

		// SaveWorkSeriesCommandCanExecute: 一時保存ボタンの有効/無効状態
		this.SaveWorkSeriesCommandCanExecute = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// SaveWorkSeriesCommand: 一時保存コマンド
		this.SaveWorkSeriesCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.SaveWorkSeriesCommand.Subscribe(async _ =>
		{
			await this.SaveWorkSeriesAsync();
		});

		// DropFilesCommand: ドラッグアンドドロップでファイルを追加
		this.DropFilesCommand = new ReactiveCommand<string[]>()
			.AddTo(ref this.disposableBag);
		this.DropFilesCommand.Subscribe(async filePaths =>
		{
			await this.AddMaterialFilesFromDropAsync(filePaths);
		});

		// IsMaterialDragOver: 素材ファイルドラッグオーバー状態
		this.IsMaterialDragOver = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// RegisterSeriesCommandCanExecute: 登録ボタンの有効/無効状態
		this.RegisterSeriesCommandCanExecute = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// RegisterSeriesCommand: 正式登録コマンド
		this.RegisterSeriesCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.RegisterSeriesCommand.Subscribe(async _ =>
		{
			await this.RegisterSeriesAsync();
		});

		// MaterialSourceFolders: 登録先に選択可能な素材フォルダ一覧
		this.MaterialSourceFolders = new ObservableList<SourceFolder>();

		// SelectedMaterialSourceFolder: 選択された素材フォルダ
		this.SelectedMaterialSourceFolder = new BindableReactiveProperty<SourceFolder?>(null)
			.AddTo(ref this.disposableBag);

		// CanSelectMaterialSourceFolder: 登録先素材フォルダ変更可能かどうか
		this.CanSelectMaterialSourceFolder = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// AddMaterialFileCommand: ファイル選択コマンド
		this.AddMaterialFileCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.AddMaterialFileCommand.Subscribe(_ =>
		{
			this.AddMaterialFileAsync();
		});

		// AddMaterialFolderCommand: フォルダ選択コマンド
		this.AddMaterialFolderCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.AddMaterialFolderCommand.Subscribe(_ =>
		{
			this.AddMaterialFolderAsync();
		});

		// TitleLostFocusCommand: タイトル欄がフォーカスを失った通知
		this.TitleLostFocusCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.TitleLostFocusCommand.Subscribe(_ =>
		{
			this.handleTitleLostFocus();
		});

		// VolumeStatus: 巻情報編集用ViewModel
		this.VolumeStatus = new EditorSeriesVolumeStatusViewModel()
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 素材ファイル一覧のヘッダー表示文字列を取得します。
	/// </summary>
	/// <returns>0件の場合は "無し"、1件以上の場合は "{Count}件"。</returns>
	private string getMaterialFilesDisplayText()
	{
		return this.MaterialFiles.Count == 0
			? "無し"
			: $"{this.MaterialFiles.Count}件";
	}

	/// <summary>
	/// サマリカード用の素材ファイル数表示テキストを取得します。
	/// </summary>
	/// <returns>0件の場合は "0件"、1件以上の場合は "{Count}件"。</returns>
	private string getMaterialFileCountText()
	{
		return this.MaterialFiles.Count == 0
			? "0件"
			: $"{this.MaterialFiles.Count}件";
	}

	/// <summary>
	/// 指定された作品の編集を開始します。
	/// 編集セッション管理は SeriesManager に委譲します。
	/// </summary>
	/// <param name="series">編集対象の作品。</param>
	public void StartEdit(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		// 編集セッション開始を SeriesManager に依頼
		this.seriesManager.BeginEdit(series);

		// SeriesManager から編集対象を取得
		var editingSeries = this.seriesManager.GetEditingSeries();
		this.EditingSeries.Value = editingSeries;

		// ReactiveProperty に値を設定
		if (editingSeries != null)
		{
			this.Title.Value = editingSeries.Title;
			this.DuplicateSeriesFound.Value = null;
			this.Author.Value = editingSeries.Author;
			this.Publisher.Value = editingSeries.Publisher;
			this.Description.Value = editingSeries.Description;
			this.Memo.Value = editingSeries.Memo;

			// 巻情報を VolumeStatus に読み込み
			this.VolumeStatus.LoadFromSeries(editingSeries);

			// TagSelector へ対象作品を設定（onTagsChanged は不要）
			this.tagSelector.SetTarget(editingSeries);

			// 素材ファイル一覧を初期化（既存作品のみ）
			this.MaterialFiles.Clear();
			if (!editingSeries.IsWork)
			{
				var materialFiles = this.seriesManager.GetMaterialFiles(editingSeries);
				foreach (var item in materialFiles)
				{
					var viewModel = MaterialFileItemViewModel.FromDto(item);
					this.MaterialFiles.Add(viewModel);
				}
			}

			// ヘッダー表示用文字列を更新
			this.MaterialFilesDisplay.Value = this.getMaterialFilesDisplayText();

			// サマリカード用の素材ファイル数表示テキストを更新
			this.MaterialFileCountText.Value = this.getMaterialFileCountText();

			// EmptyState 表示制御を更新
			var isEmpty = this.MaterialFiles.Count == 0;
			this.IsMaterialFilesEmpty.Value = isEmpty;
			this.HasMaterialFiles.Value = !isEmpty;

			// タイトル入力欄へのフォーカスを要求
			this.TitleFocusRequest.Value++;

			// 既存作品の場合、登録先素材フォルダを設定し ComboBox を無効化
			if (editingSeries.SeriesId != 0)
			{
				var sourceFolder = this.findMatchingSourceFolderForExistingSeries(editingSeries);
				this.SelectedMaterialSourceFolder.Value = sourceFolder;
				System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.StartEdit] 既存作品。sourceFolder: {(sourceFolder == null ? "null" : sourceFolder.FolderPath.Value)}");
				this.CanSelectMaterialSourceFolder.Value = false;
			}
			else
			{
				// 新規作品・登録待ち作品の場合は ComboBox を有効化
				this.CanSelectMaterialSourceFolder.Value = true;
			}

			// 一時保存ボタンの有効/無効を更新
			this.UpdateSaveWorkSeriesCommandCanExecute();

			// 貼り付けたサムネイルをクリア
			this.ClearPastedThumbnail();
		}
	}

	/// <summary>
	/// 既存作品の Material フォルダに対応する SourceFolder を検索します。
	/// 複数の MaterialSource がある場合は先頭を基準に検索します。
	/// 複数のSourceFolderが一致する場合は、FolderPathが最も長いものを優先します。
	/// </summary>
	/// <param name="series">既存作品。</param>
	/// <returns>一致した SourceFolder、または見つからない場合は null。</returns>
	private SourceFolder? findMatchingSourceFolderForExistingSeries(MangaSeries series)
	{
		// Material フォルダのみを対象
		var materialSources = series.Sources
			.Where(s => s.Role == FolderRole.Material)
			.ToList();

		// Material フォルダがない場合は null
		if (materialSources.Count == 0)
		{
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.findMatchingSourceFolderForExistingSeries] Material フォルダなし。SeriesId: {series.SeriesId}");
			return null;
		}

		// 先頭の MaterialSource を基準に検索
		var targetMaterialPath = Path.GetFullPath(materialSources[0].Path);
		System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.findMatchingSourceFolderForExistingSeries] 検索開始。targetMaterialPath: {targetMaterialPath}");

		// AppSettings の Material フォルダで照合
		var materialFolders = this.appSettings.SourceFolders
			.Where(f => f.Role.Value == FolderRole.Material)
			.ToList();

		System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.findMatchingSourceFolderForExistingSeries] AppSettings Material フォルダ数: {materialFolders.Count}");

		// 各フォルダと照合
		var matchedFolders = new List<(SourceFolder folder, int pathLength)>();

		foreach (var sourceFolder in materialFolders)
		{
			var sourceFolderPath = Path.GetFullPath(sourceFolder.FolderPath.Value);
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.findMatchingSourceFolderForExistingSeries] 照合対象: {sourceFolderPath}");

			// targetMaterialPath が sourceFolderPath の配下にあるかチェック
			if (this.isPathUnderFolder(targetMaterialPath, sourceFolderPath))
			{
				System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.findMatchingSourceFolderForExistingSeries] 一致: {sourceFolderPath}");
				matchedFolders.Add((sourceFolder, sourceFolderPath.Length));
			}
		}

		// 一致フォルダがない場合
		if (matchedFolders.Count == 0)
		{
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.findMatchingSourceFolderForExistingSeries] 一致フォルダなし");
			return null;
		}

		// FolderPath が最も長いものを優先（最も詳細度の高い一致）
		var result = matchedFolders
			.OrderByDescending(x => x.pathLength)
			.First()
			.folder;

		System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.findMatchingSourceFolderForExistingSeries] 結果: {result.FolderPath.Value}");
		return result;
	}

	/// <summary>
	/// targetPath が folderPath の配下にあるかを判定します。
	/// </summary>
	/// <param name="targetPath">判定対象のパス。</param>
	/// <param name="folderPath">親フォルダのパス。</param>
	/// <returns>targetPath が folderPath の配下にある場合は true。</returns>
	private bool isPathUnderFolder(string targetPath, string folderPath)
	{
		var targetFull = Path.GetFullPath(targetPath);
		var folderFull = Path.GetFullPath(folderPath);

		// folderFull の末尾に区切り文字がない場合は追加
		if (!folderFull.EndsWith(Path.DirectorySeparatorChar))
		{
			folderFull += Path.DirectorySeparatorChar;
		}

		// targetFull が folderFull の配下にあるかを判定
		return targetFull.StartsWith(folderFull, StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(targetFull, folderFull.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 一時保存ボタンと登録ボタンの有効/無効状態を更新します。
	/// 新規作品・登録待ち作品の場合は一時保存ボタンが有効、既存作品の場合は登録ボタンが有効になります。
	/// </summary>
	private void UpdateSaveWorkSeriesCommandCanExecute()
	{
		if (this.EditingSeries.Value == null)
		{
			this.SaveWorkSeriesCommandCanExecute.Value = false;
			this.RegisterSeriesCommandCanExecute.Value = false;
			return;
		}

		// 既存作品（SeriesId != 0）の場合
		if (this.EditingSeries.Value.SeriesId != 0)
		{
			// 一時保存は不可、登録は常に可能
			this.SaveWorkSeriesCommandCanExecute.Value = false;
			this.RegisterSeriesCommandCanExecute.Value = true;
			return;
		}

		// D&D で追加された素材がある場合
		if (this.MaterialFiles.Count > 0)
		{
			this.SaveWorkSeriesCommandCanExecute.Value = false;
			this.RegisterSeriesCommandCanExecute.Value = true;
			return;
		}

		// 新規作品・登録待ち作品で素材なし
		this.SaveWorkSeriesCommandCanExecute.Value = true;
		this.RegisterSeriesCommandCanExecute.Value = false;
	}

	/// <summary>
	/// タイトルのバリデーション処理。
	/// タイトル重複チェックを行い、結果に応じてエラーメッセージまたは null を返す。
	/// 編集対象に応じて判定ロジックを変更します：
	/// - 新規作品・登録待ち作品：編集中の作品自身を候補から除外
	/// - 既存作品：編集中の作品と入力タイトルの一致を確認してから判定
	/// </summary>
	/// <param name="title">検証するタイトル。</param>
	/// <returns>エラーメッセージ、またはエラーなしの場合は null。</returns>
	private string? validateTitle(string? title)
	{
		// null / 空白なら重複チェックしない
		if (string.IsNullOrWhiteSpace(title))
		{
			this.DuplicateSeriesFound.Value = null;
			return null;
		}

		// タイトル重複チェック
		var duplicates = this.seriesManager.FindSameTitle(title);

		// 既存作品編集時の特別処理
		if (this.EditingSeries.Value != null &&
			this.EditingSeries.Value.SeriesId != 0 &&
			!this.EditingSeries.Value.IsWork)
		{
			// 既存作品編集中
			// 自分自身が含まれているかを確認
			var selfInDuplicates = duplicates.FirstOrDefault(s =>
				s.SeriesId == this.EditingSeries.Value.SeriesId);

			if (selfInDuplicates != null)
			{
				// 自分自身が含まれている
				// 他作品を候補から取り出す
				var othersExceptSelf = duplicates
					.Where(s => s.SeriesId != this.EditingSeries.Value.SeriesId)
					.ToList();

				if (othersExceptSelf.Count == 0)
				{
					// 自分自身のみ一致 → 正常
					this.DuplicateSeriesFound.Value = null;
					return null;
				}
				else if (othersExceptSelf.Count == 1)
				{
					// 自分自身 + 他作品1件 → ContentDialog表示
					this.DuplicateSeriesFound.Value = othersExceptSelf[0];
					return null;
				}
				else
				{
					// 自分自身 + 他作品2件以上 → 複数一致エラー
					this.DuplicateSeriesFound.Value = null;

					// Snackbarでエラー通知
					this.snackbarService.Show(
						"エラー",
						"同じタイトルの作品が複数見つかりました。",
						ControlAppearance.Danger,
						new SymbolIcon { Symbol = SymbolRegular.Warning24 },
						TimeSpan.MaxValue);

					return "同じタイトルの作品が複数見つかりました。";
				}
			}
			else
			{
				// 自分自身が含まれていない（0件一致）
				// 入力タイトルが別作品として判定された
				this.DuplicateSeriesFound.Value = null;

				// ContentDialog を表示
				_ = this.showDifferentSeriesDialogAsync();

				return null;
			}
		}

		// 新規作品または登録待ち作品の処理
		// 編集中の作品自身を除外
		if (this.EditingSeries.Value != null)
		{
			// 正式作品の場合は SeriesId で除外
			if (this.EditingSeries.Value.SeriesId != 0)
			{
				duplicates = duplicates
					.Where(s => s.SeriesId != this.EditingSeries.Value.SeriesId)
					.ToList();
			}
			// 登録待ち作品の場合は WorkId で除外
			else if (this.EditingSeries.Value.WorkId != 0)
			{
				duplicates = duplicates
					.Where(s => s.WorkId != this.EditingSeries.Value.WorkId)
					.ToList();
			}
		}

		// 0件なら重複候補なし
		if (duplicates.Count == 0)
		{
			this.DuplicateSeriesFound.Value = null;
			return null;
		}

		// 1件なら既存作品候補として保持（エラーではない）
		if (duplicates.Count == 1)
		{
			this.DuplicateSeriesFound.Value = duplicates[0];
			return null;
		}

		// 2件以上ならエラー
		this.DuplicateSeriesFound.Value = null;

		// Snackbarでエラー通知
		this.snackbarService.Show(
			"エラー",
			"同じタイトルの作品が複数見つかりました。",
			ControlAppearance.Danger,
			new SymbolIcon { Symbol = SymbolRegular.Warning24 },
			TimeSpan.MaxValue);

		return "同じタイトルの作品が複数見つかりました。";
	}

	/// <summary>
	/// ナビゲーション完了後に呼ばれる初期データ読み込み処理。
	/// workspaceStore.EditTarget から編集対象を取得し、StartEdit を実行します。
	/// また、AppSettings から素材フォルダ一覧を取得します。
	/// </summary>
	public ValueTask InitializeDataAsync()
	{
		// Material フォルダ一覧を取得・設定
		var materialFolders = this.appSettings.SourceFolders
			.Where(f => f.Role.Value == FolderRole.Material)
			.ToList();
		this.MaterialSourceFolders.Clear();
		foreach (var folder in materialFolders)
		{
			this.MaterialSourceFolders.Add(folder);
		}

		var editTarget = this.workspaceStore.EditTarget;

		// 新規作品・登録待ち作品の場合は先頭を初期選択
		if (editTarget == null || editTarget.SeriesId == 0)
		{
			this.CanSelectMaterialSourceFolder.Value = true;
			if (this.MaterialSourceFolders.Count > 0)
			{
				this.SelectedMaterialSourceFolder.Value = this.MaterialSourceFolders[0];
			}
		}

		if (editTarget is not null)
		{
			this.StartEdit(editTarget);
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// 画面から離れる際の後処理。
	/// 遷移先が状態保持を要求していない場合に、編集対象の参照をクリアします。
	/// </summary>
	/// <param name="request">遷移先から受け取った要求。</param>
	public ValueTask OnNavigatingFromAsync(NavigationLeavingRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!request.PreserveState)
		{
			this.workspaceStore.EditTarget = null;
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// 完結巻入力中のテキストを処理し、UI 制御状態を更新します。
	/// </summary>
	/// <param name="text">NumberBox から入力されたテキスト。</param>
	private void HandleEndVolumeTextInput(string? text)
	{
		// テキストが空または null である、または数値として解釈できない場合
		if (string.IsNullOrEmpty(text) || !double.TryParse(text, out var value))
		{
			this.CanEditOwnedCompleted.Value = false;
			this.IsOwnedCompleted.Value = false;
			return;
		}

		// 値が 1 以上である場合、CanEditOwnedCompleted を有効化
		if (value >= 1)
		{
			this.CanEditOwnedCompleted.Value = true;
		}
		else
		{
			// 値が 1 未満である場合
			this.CanEditOwnedCompleted.Value = false;
			this.IsOwnedCompleted.Value = false;
		}
	}

	/// <summary>
	/// 既存作品編集中に入力タイトルが別作品として判定された場合の ContentDialog を表示します。
	/// ユーザーがタイトルを再入力するか、前画面へ戻るかを選択できます。
	/// </summary>
	private async ValueTask showDifferentSeriesDialogAsync()
	{
		// ダイアログを作成
		var dialog = new ContentDialog
		{
			Title = "タイトル変更の確認",
			Content = "入力したタイトルが別作品として判定されたため変更できません。\n新規作品として登録してください。",
			PrimaryButtonText = "タイトルを再入力",
			CloseButtonText = "前画面に戻る",
		};

		// ダイアログを表示して結果を取得
		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);

		// ユーザーが「タイトルを再入力」を選択した場合
		if (result == ContentDialogResult.Primary)
		{
			// 次回の LostFocus 時に再判定を実行するようフラグを設定
			this.needsTitleRevalidation = true;

			// EditorPage に留まり、タイトルへフォーカスを戻す
			// TitleFocusRequest を更新してタイトル入力欄へフォーカスを移す
			// SelectAllOnFocusBehavior により、入力済みタイトルが全選択される
			this.TitleFocusRequest.Value++;
		}
		else
		{
			// 「前画面に戻る」の場合
			// ナビゲーション履歴に従って戻る
			this.navigationService.GoBack();
		}
	}

	/// <summary>
	/// タイトル欄がフォーカスを失ったときの処理を実行します。
	/// 再判定待ちフラグが立っている場合のみ、現在値のまま ForceValidate() を実行します。
	/// </summary>
	private void handleTitleLostFocus()
	{
		// 再判定待ちフラグが false の場合は何もしない（通常のバリデーションに任せる）
		if (!this.needsTitleRevalidation)
		{
			return;
		}

		// フラグをリセット
		this.needsTitleRevalidation = false;

		// 現在値のまま validateTitle() を再実行
		this.Title.ForceValidate();
	}

	/// <summary>
	/// 既存作品ダイアログを非同期で表示し、ユーザーの選択に応じた処理を実行します。
	/// </summary>
	/// <param name="duplicateSeries">既存の作品。</param>
	private async ValueTask showExistingSeriesDialogAsync(MangaSeries duplicateSeries)
	{
		// ダイアログ用に MaintenanceSeriesCardViewModel を生成
		var cardViewModel = new MaintenanceSeriesCardViewModel(duplicateSeries);

		// ダイアログコンテンツを作成
		var content = new ExistingSeriesDialogContent
		{
			DataContext = cardViewModel,
		};

		// ダイアログを作成
		var dialog = new ContentDialog
		{
			Title = "既に登録済みです。",
			Content = content,
			PrimaryButtonText = "作品を開く",
			CloseButtonText = "前画面に戻る",
		};

		// ダイアログを表示して結果を取得
		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);

		// ユーザーが「作品を開く」を選択した場合
		if (result == ContentDialogResult.Primary)
		{
			// DuplicateSeriesFound を null にリセット（ダイアログループ防止）
			this.DuplicateSeriesFound.Value = null;
			// 既存作品を読み込み
			this.StartEdit(duplicateSeries);
		}
		else
		{
			// 「前画面に戻る」の場合、遷移元の画面へ戻る
			// DuplicateSeriesFound を null にリセット
			this.DuplicateSeriesFound.Value = null;
			// ナビゲーション履歴に従って戻る
			this.navigationService.GoBack();
		}

		// ダイアログ終了後にカード ViewModel を Dispose
		cardViewModel.Dispose();
	}

	/// <summary>
	/// クリップボードからサムネイルを貼り付けます。
	/// 成功時はプレビュー画像を更新し、PNG byte[] を保持します。
	/// 失敗時はログのみ出力し、UI には何も反映されません。
	/// </summary>
	private void PasteThumbnailAsync()
	{
		try
		{
			// ThumbnailPicker からクリップボード画像を取得
			var bitmapSource = this.thumbnailPicker.GetFromClipboard();
			if (bitmapSource == null)
			{
				System.Diagnostics.Debug.WriteLine("[EditorPageViewModel.PasteThumbnailAsync] クリップボードに画像がありません。");
				return;
			}

			// BitmapSource を PNG byte[] に変換
			var pngBytes = this.thumbnailPicker.ToBytes(bitmapSource);
			if (pngBytes == null || pngBytes.Length == 0)
			{
				System.Diagnostics.Debug.WriteLine("[EditorPageViewModel.PasteThumbnailAsync] 画像を PNG 形式に変換できませんでした。");
				return;
			}

			// 成功時：プレビュー画像を更新し、PNG byte[] を保持
			this.ThumbnailPreviewImageSource.Value = bitmapSource;
			this.PastedThumbnailBytes = pngBytes;
		}
		catch (Exception ex)
		{
			// 予期しないエラーが発生した場合もログ出力のみ
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.PasteThumbnailAsync] 例外発生: {ex.Message}");
		}
	}

	/// <summary>
	/// 貼り付けたサムネイルの PNG byte[] を取得します。
	/// 保存処理で利用します。
	/// </summary>
	/// <returns>貼り付けた PNG byte[]、存在しない場合は null。</returns>
	public byte[]? GetPastedThumbnailBytes()
		=> this.PastedThumbnailBytes;

	/// <summary>
	/// 貼り付けたサムネイル情報をクリアします。
	/// </summary>
	public void ClearPastedThumbnail()
	{
		this.ThumbnailPreviewImageSource.Value = null;
		this.PastedThumbnailBytes = null;
	}

	/// <summary>
	/// ファイルから画像を選択してサムネイルを設定します。
	/// 成功時はプレビュー画像と JPEG byte[] を更新します。
	/// キャンセル時は何もしません。
	/// 失敗時はログ出力と Snackbar で通知します。
	/// </summary>
	private async ValueTask SelectThumbnailAsync()
	{
		try
		{
			// ThumbnailPicker からファイル選択ダイアログを表示
			var result = await this.thumbnailPicker.PickFromFileAsync(CancellationToken.None);

			if (result.IsCanceled)
			{
				// キャンセルされた場合は何もしない
				return;
			}

			if (!result.Success || result.PreviewImage == null || result.ThumbnailBytes == null)
			{
				// 読み込み失敗時：ログとユーザー通知
				var errorMessage = result.ErrorMessage ?? "画像の読み込みに失敗しました。";
				System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.SelectThumbnailAsync] {errorMessage}");

				// Snackbar で通知
				this.snackbarService.Show(
					"エラー",
					errorMessage,
					ControlAppearance.Caution,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.FromSeconds(3));
				return;
			}

			// 成功時：プレビュー画像と JPEG byte[] を更新
			this.ThumbnailPreviewImageSource.Value = result.PreviewImage;
			this.PastedThumbnailBytes = result.ThumbnailBytes;
		}
		catch (Exception ex)
		{
			// 予期しないエラーが発生した場合もログとユーザー通知
			var errorMessage = $"サムネイル選択中にエラーが発生しました: {ex.Message}";
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.SelectThumbnailAsync] 例外発生: {errorMessage}");

			this.snackbarService.Show(
				"エラー",
				errorMessage,
				ControlAppearance.Caution,
				new SymbolIcon { Symbol = SymbolRegular.Warning24 },
				TimeSpan.FromSeconds(3));
		}
	}

	/// <summary>
	/// 編集画面の入力値を指定された MangaSeries オブジェクトへ反映し、完成データへ更新します。
	/// Worker（MaterialFolderScanner / GoogleBooksImporter）と同等の品質を目指します。
	/// 既存作品（SeriesId != 0 かつ IsWork == false）の場合は、共通処理のみを実施して返ります。
	/// </summary>
	/// <param name="targetSeries">値を反映する対象の MangaSeries。</param>
	private void UpdateEditingSeriesFromUI(MangaSeries targetSeries)
	{
		if (targetSeries == null)
			return;

		var titleInput = this.Title.Value ?? string.Empty;

		// === 【共通処理】基本情報 ===
		targetSeries.Title = titleInput;
		targetSeries.Author = this.Author.Value ?? string.Empty;
		targetSeries.Publisher = this.Publisher.Value ?? string.Empty;
		targetSeries.Description = this.Description.Value ?? string.Empty;
		targetSeries.Memo = this.Memo.Value ?? string.Empty;

		// === 【共通処理】タイトル派生値（MangaTitleHelper を利用） ===
		targetSeries.NormalizedTitleInternal = MangaTitleHelper.NormalizeTitleInternal(titleInput);
		targetSeries.ShortTitle = MangaTitleHelper.GetShortTitle(titleInput, string.Empty);
		// NormalizedTitleExternal は Worker でも設定されていないため string.Empty のまま（登録待ち・新規のみで後から設定）

		// === 【共通処理】巻情報 ===
		targetSeries.StartVolume = (int)this.VolumeStatus.StartVolume.Value;

		// 完結巻の反映：EndVolume が入力されている場合は SeriesCompleted = true
		if (this.VolumeStatus.EndVolume.Value.HasValue && this.VolumeStatus.EndVolume.Value.Value >= 1)
		{
			targetSeries.EndVolume = (int)this.VolumeStatus.EndVolume.Value.Value;
			targetSeries.SeriesCompleted = true;
		}
		else
		{
			// 完結巻が未入力の場合
			targetSeries.EndVolume = 0;
			targetSeries.SeriesCompleted = false;
			targetSeries.IsOwnedCompleted = false;
		}

		// 所持推定巻数を反映
		targetSeries.OwnedMaxVolume = this.VolumeStatus.OwnedMaxVolume.Value.HasValue ? (int)this.VolumeStatus.OwnedMaxVolume.Value.Value : 0;

		// 全巻所持フラグを反映
		targetSeries.IsOwnedCompleted = this.VolumeStatus.IsOwnedCompleted.Value;

		// === 【共通処理】Description 出典 ===
		// 既存作品（SeriesId != 0 && IsWork == false）ではこのメソッド内では変更しない
		// 新規作品・登録待ち作品のみ、Description が入力されている場合は Manual、未入力の場合は None
		if (targetSeries.SeriesId == 0 || targetSeries.IsWork)
		{
			if (!string.IsNullOrEmpty(this.Description.Value))
			{
				targetSeries.DescriptionSource = DescriptionSource.Manual;
				targetSeries.DescriptionSourceTitle = string.Empty;
			}
			else
			{
				targetSeries.DescriptionSource = DescriptionSource.None;
				targetSeries.DescriptionSourceTitle = string.Empty;
			}
		}

		// === 【共通処理】GoogleBooks 関連 ===
		// UI から一時保存した時点では GoogleBooksImporter はまだ実行されていない
		// GoogleBooksImporter の取得条件・更新条件を満たす状態を保存
		targetSeries.GoogleBooksImportStatus = GoogleBooksImportStatus.NotImported;
		targetSeries.GoogleBooksImportedAt = string.Empty;
		targetSeries.GoogleBooksImportMessage = string.Empty;

		// === 既存作品の場合はここで終了 ===
		if (targetSeries.SeriesId != 0 && !targetSeries.IsWork)
		{
			// 既存作品では以降の初期化処理は実施しない
			return;
		}

		// === 【登録待ち・新規のみ】その他の初期値 ===
		// Worker で生成される MangaSeries と同等の状態を保持
		targetSeries.IsSourceMissing = false;
		targetSeries.HasNestedArchive = false;

		// === 【登録待ち・新規のみ】製本済み最終巻 ===
		// UI で編集不可のため、デフォルト値のまま
		targetSeries.BoundEndVolume = 0;

		// === 【登録待ち・新規のみ】外部用タイトル正規化 ===
		targetSeries.NormalizedTitleExternal = string.Empty;
		// NOTE: ManuallyEditedAt と IsOwnedMaxVolumeManuallyEdited は WorkMangaSeries テーブルに存在しないため設定しない
	}

	/// <summary>
	/// 素材追加時に重複が発見された場合の Warning Snackbar を表示します。
	/// </summary>
	/// <param name="alreadyAddedFiles">既に追加済みのファイル名リスト。</param>
	/// <param name="sameNameFiles">同名の素材ファイル名リスト。</param>
	private void showMaterialDuplicateWarningSnackbar(List<string> alreadyAddedFiles, List<string> sameNameFiles)
	{
		// どちらのリストも空の場合は表示しない
		if ((alreadyAddedFiles.Count == 0) && (sameNameFiles.Count == 0))
		{
			return;
		}

		string title;
		string message;

		// 重複素材の総件数
		var totalDuplicateCount = alreadyAddedFiles.Count + sameNameFiles.Count;

		if (totalDuplicateCount == 1)
		{
			// === 1 件の場合：ファイル名を表示 ===
			if (alreadyAddedFiles.Count == 1)
			{
				title = "素材追加";
				message = $"「{alreadyAddedFiles[0]}」は既に追加されています。";
			}
			else
			{
				title = "素材追加";
				message = $"同名の素材ファイル「{sameNameFiles[0]}」が既に存在するため追加できません。";
			}
		}
		else
		{
			// === 2 件以上の場合：件数でまとめて表示 ===
			var messageParts = new List<string>();
			if (alreadyAddedFiles.Count > 0)
			{
				messageParts.Add($"既に追加済みの素材：{alreadyAddedFiles.Count}件");
			}
			if (sameNameFiles.Count > 0)
			{
				messageParts.Add($"同名の素材ファイル：{sameNameFiles.Count}件");
			}
			title = "素材追加";
			message = string.Join("\n", messageParts);
		}

		// 5 秒で自動閉じ、Caution（黄色）で表示
		this.snackbarService.Show(
			title,
			message,
			ControlAppearance.Caution,
			new SymbolIcon { Symbol = SymbolRegular.Warning24 },
			TimeSpan.FromSeconds(5));
	}

	/// <summary>
	/// 編集中の作品を WorkMangaSeries テーブルへ一時保存します。
	/// 保存対象となるサムネイル JPEG byte[] がある場合は、MangaSeriesManager 経由で WorkThumbnail フォルダへ保存します。
	/// 成功後は自動的に作品管理画面へ戻ります。
	/// </summary>
	private async ValueTask SaveWorkSeriesAsync()
	{
		if (this.EditingSeries.Value == null)
			return;

		try
		{
			var editingSeries = this.EditingSeries.Value;

			// EditingSeries を完成状態へ更新
			this.UpdateEditingSeriesFromUI(editingSeries);

			// サムネイル JPEG byte[] を取得
			var thumbnailBytes = this.GetPastedThumbnailBytes();

			// WorkMangaSeries へ一時保存
			var workId = await this.seriesManager.SaveWorkSeriesAsync(editingSeries, thumbnailBytes);

			// 成功ログ
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.SaveWorkSeriesAsync] 一時保存成功。WorkId={workId}");

			// 作品管理画面へ戻る
			this.navigationService.Navigate(typeof(MaintenancePage));
		}
		catch (Exception ex)
		{
			// エラーが発生した場合はログとユーザー通知
			// 開発中のアプリであるため、Exception.Message と StackTrace を表示
			var errorMessageBody = $"{ex.Message}\n\nStackTrace:\n{ex.StackTrace}";
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.SaveWorkSeriesAsync] 例外発生: {ex}");

			// VolumeSelectionPage と同じ通知方式：赤色（Danger）で自動では閉じない
			this.snackbarService.Show(
				"一時保存に失敗しました",
				errorMessageBody,
				ControlAppearance.Danger,
				new SymbolIcon { Symbol = SymbolRegular.Warning24 },
				TimeSpan.MaxValue);
		}
	}

	/// <summary>
	/// ドラッグアンドドロップされたファイル/フォルダを解析し、素材として MaterialFiles に追加します。
	/// 以下の基準で判定・追加します：
	/// - ファイル：SupportedExtensionHelper.IsArchive() で対応アーカイブのみ追加
	/// - フォルダ（epub のみ）：フォルダ内の epub ファイルを単体で追加
	/// - フォルダ（画像のみ）：フォルダ自体を追加
	/// - それ以外：追加しない（エラー表示なし）
	/// 
	/// 重複判定は以下の順で行われます：
	/// 1. 同一 FullPath が存在する場合は追加しません（「既に追加済み」として記録）
	/// 2. 同一 FullPath は異なるが、同名の FileName が存在する場合は追加しません（「同名ファイル」として記録）
	/// 3. 上記以外は通常どおり追加します
	/// 
	/// 追加できなかった素材が存在する場合、処理終了後に Warning Snackbar を 1 回だけ表示します。
	/// </summary>
	/// <param name="droppedPaths">ドラッグアンドドロップされたファイル/フォルダのパス配列。</param>
	private async ValueTask AddMaterialFilesFromDropAsync(string[] droppedPaths)
	{
		if (droppedPaths == null || droppedPaths.Length == 0)
		{
			return;
		}

		try
		{
			// 既存の FullPath と FileName を取得
			var existingFullPaths = this.MaterialFiles.Select(m => m.FullPath).ToHashSet();
			var existingFileNames = this.MaterialFiles.Select(m => m.FileName).ToHashSet();

			// 候補を解析
			var candidates = this.materialManager.AnalyzePaths(droppedPaths, existingFullPaths.ToList());

			// 重複判定結果を記録
			var alreadyAddedFiles = new List<string>();
			var sameNameFiles = new List<string>();
			var addedAny = false;

			foreach (var candidate in candidates)
			{
				// ① 同一 FullPath の重複判定
				if (existingFullPaths.Contains(candidate.FullPath))
				{
					alreadyAddedFiles.Add(candidate.FileName);
					continue;
				}

				// ② 同名ファイルの重複判定
				if (existingFileNames.Contains(candidate.FileName))
				{
					sameNameFiles.Add(candidate.FileName);
					continue;
				}

				// ③ 重複なし → 追加
				var item = new MaterialFileItem
				{
					Name = candidate.FileName,
					FullPath = candidate.FullPath,
					ItemType = candidate.Type,
					SizeBytes = candidate.Size,
					CanRemove = true,
				};
				var viewModel = MaterialFileItemViewModel.FromDto(item);
				this.MaterialFiles.Add(viewModel);
				addedAny = true;
			}

			if (addedAny)
			{
				// ヘッダー表示用文字列を更新
				this.MaterialFilesDisplay.Value = this.getMaterialFilesDisplayText();

				// ボタン有効制御を更新
				this.UpdateSaveWorkSeriesCommandCanExecute();

				// 所持推定を再計算
				this.RecalculateOwnedVolumeEstimate();
			}

			// === 追加できなかった素材の通知 ===
			if (alreadyAddedFiles.Count > 0 || sameNameFiles.Count > 0)
			{
				this.showMaterialDuplicateWarningSnackbar(alreadyAddedFiles, sameNameFiles);
			}
		}
		catch (Exception ex)
		{
			// ドラッグアンドドロップ処理中のエラーをログ出力
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.AddMaterialFilesFromDropAsync] 例外発生: {ex.Message}");
		}
	}

	/// <summary>
	/// 現在の MaterialFiles から所持推定を再計算し、有効な結果の場合のみ OwnedMaxVolume を更新します。
	/// 推定不能の場合は現在の所持推定値を変更しません。
	/// </summary>
	private void RecalculateOwnedVolumeEstimate()
	{
		try
		{
			var entryNames = this.MaterialFiles.Select(m => m.FileName).ToList();
			if (entryNames.Count == 0)
			{
				return;
			}

			var result = this.ownedVolumeEstimator.Estimate(entryNames);
			if (result.OwnedMaxVolume > 0)
			{
				this.VolumeStatus.OwnedMaxVolume.Value = result.OwnedMaxVolume;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.RecalculateOwnedVolumeEstimate] 例外発生: {ex.Message}");
		}
	}

	/// <summary>
	/// ファイル選択ダイアログを表示して素材ファイルを追加します。
	/// </summary>
	private void AddMaterialFileAsync()
	{
		try
		{
			var dialog = new OpenFileDialog
			{
				Title = "素材ファイルを選択してください",
				Multiselect = true,
				Filter = SupportedExtensionHelper.ArchiveOpenFileDialogFilter,
				FilterIndex = 1,
			};

			if (dialog.ShowDialog() ?? false)
			{
				if (dialog.FileNames != null && dialog.FileNames.Length > 0)
				{
					_ = this.AddMaterialFilesFromDropAsync(dialog.FileNames);
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.AddMaterialFileAsync] 例外発生: {ex.Message}");
		}
	}

	/// <summary>
	/// フォルダ選択ダイアログを表示して素材フォルダを追加します。
	/// 複数フォルダの同時選択に対応しています。
	/// </summary>
	private void AddMaterialFolderAsync()
	{
		try
		{
			var dialog = new OpenFolderDialog
			{
				Title = "素材フォルダを選択してください",
				Multiselect = true,
			};

			if (dialog.ShowDialog() ?? false)
			{
				if (dialog.FolderNames != null && dialog.FolderNames.Length > 0)
				{
					_ = this.AddMaterialFilesFromDropAsync(dialog.FolderNames);
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.AddMaterialFolderAsync] 例外発生: {ex.Message}");
		}
	}

	/// <summary>
	/// 編集中の作品を正式な MangaSeries として登録します。
	/// </summary>
	private async ValueTask RegisterSeriesAsync()
	{
		try
		{
			// 入力値の検証
			var editingSeries = this.EditingSeries.Value;
			if (editingSeries == null)
			{
				this.snackbarService.Show(
					"エラー",
					"編集中の作品情報がありません。",
					ControlAppearance.Caution,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.FromSeconds(3));
				return;
			}

			// UI から編集対象へ最新値を反映
			this.UpdateEditingSeriesFromUI(editingSeries);

			if (string.IsNullOrWhiteSpace(editingSeries.Title))
			{
				this.snackbarService.Show(
					"エラー",
					"タイトルを入力してください。",
					ControlAppearance.Caution,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.FromSeconds(3));
				return;
			}

			// 既存作品（SeriesId != 0 かつ IsWork == false）の場合
			if (editingSeries.SeriesId != 0 && !editingSeries.IsWork)
			{
				await this.UpdateExistingSeriesAsync();
				return;
			}

			// 新規作品・登録待ち作品の場合は従来の登録処理
			if (this.MaterialFiles.Count == 0)
			{
				this.snackbarService.Show(
					"エラー",
					"素材ファイルが登録されていません。",
					ControlAppearance.Caution,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.FromSeconds(3));
				return;
			}

			if (this.SelectedMaterialSourceFolder.Value == null)
			{
				this.snackbarService.Show(
					"エラー",
					"登録先の素材フォルダを選択してください。",
					ControlAppearance.Caution,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.FromSeconds(3));
				return;
			}

			// 素材 DTO に変換
			var materialFileDtos = this.MaterialFiles
				.Select(item => new MaterialFile
				{
					FullPath = item.FullPath,
					Type = item.ItemType,
					CanRemove = item.CanRemove,
				})
				.ToList();

			// 登録を実行
			var registeredSeries = await this.seriesManager.RegisterSeriesAsync(
				editingSeries,
				materialFileDtos,
				this.SelectedMaterialSourceFolder.Value,
				this.PastedThumbnailBytes);

			// 登録成功
			this.snackbarService.Show(
				"成功",
				$"『{registeredSeries.Title}』を登録しました。",
				ControlAppearance.Success,
				new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24 },
				TimeSpan.FromSeconds(3));

			// 作品管理画面へ戻る
			this.navigationService.Navigate(typeof(MaintenancePage));
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.RegisterSeriesAsync] 例外発生: {ex.Message}");
			this.snackbarService.Show(
				"エラー",
				$"登録に失敗しました: {ex.Message}",
				ControlAppearance.Caution,
				new SymbolIcon { Symbol = SymbolRegular.Warning24 },
				TimeSpan.FromSeconds(3));
		}
	}

	/// <summary>
	/// 既存の正式作品を更新します。
	/// 編集画面で変更可能な項目のみが UPDATE 対象です。
	/// </summary>
	private async ValueTask UpdateExistingSeriesAsync()
	{
		try
		{
			var editingSeries = this.EditingSeries.Value;
			if (editingSeries == null)
				return;

			// 素材 DTO に変換
			var materialFileDtos = this.MaterialFiles
				.Select(item => new MaterialFile
				{
					FullPath = item.FullPath,
					Type = item.ItemType,
					CanRemove = item.CanRemove,
				})
				.ToList();

			// MangaSeriesManager.UpdateExistingSeriesAsync() を呼び出し
			var updatedSeries = await this.seriesManager.UpdateExistingSeriesAsync(
				editingSeries,
				materialFileDtos,
				this.SelectedMaterialSourceFolder.Value,
				this.PastedThumbnailBytes);

			// 更新成功
			this.snackbarService.Show(
				"成功",
				$"『{updatedSeries.Title}』を更新しました。",
				ControlAppearance.Success,
				new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24 },
				TimeSpan.FromSeconds(3));

			// NavigationHierarchy に従って前画面へ戻る
			this.navigationService.GoBack();
		}
		catch (Exception ex)
		{
			// 素材フォルダ名検証エラーを判定
			if (ex.Message.Contains("素材フォルダ名の変更判定でエラーが発生しました"))
			{
				// 結果文字列を解析して適切なメッセージを表示
				if (ex.Message.Contains(MaterialFolderRenameCheckResult.CurrentFolderNotFound.ToString()))
				{
					this.snackbarService.Show(
						"エラー",
						"登録されている素材フォルダが見つからないため、作品を更新できません。\n素材フォルダを確認してください。",
						ControlAppearance.Danger,
						new SymbolIcon { Symbol = SymbolRegular.Warning24 },
						TimeSpan.MaxValue);
				}
				else if (ex.Message.Contains(MaterialFolderRenameCheckResult.RenameTargetAlreadyExists.ToString()))
				{
					this.snackbarService.Show(
						"エラー",
						"変更後の素材フォルダ名と同じフォルダが既に存在するため、作品を更新できません。\n素材フォルダを確認してください。",
						ControlAppearance.Danger,
						new SymbolIcon { Symbol = SymbolRegular.Warning24 },
						TimeSpan.MaxValue);
				}
				else if (ex.Message.Contains(MaterialFolderRenameCheckResult.RenameNeeded.ToString()))
				{
					this.snackbarService.Show(
						"エラー",
						"素材フォルダ名の変更が必要です。\nフォルダRename処理は現在未実装のため、作品を更新できません。",
						ControlAppearance.Danger,
						new SymbolIcon { Symbol = SymbolRegular.Warning24 },
						TimeSpan.MaxValue);
				}
				else
				{
					this.snackbarService.Show(
						"エラー",
						$"更新に失敗しました: {ex.Message}",
						ControlAppearance.Caution,
						new SymbolIcon { Symbol = SymbolRegular.Warning24 },
						TimeSpan.FromSeconds(3));
				}
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.UpdateExistingSeriesAsync] 例外発生: {ex.Message}");
				this.snackbarService.Show(
					"エラー",
					$"更新に失敗しました: {ex.Message}",
					ControlAppearance.Caution,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.FromSeconds(3));
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.ClearPastedThumbnail();
		this.tagSelector.Dispose();
		this.disposableBag.Dispose();
	}
}

