using MangaBinder.Bindings;
using MangaBinder.Controls;
using MangaBinder.Settings;
using MangaBinder.Tags;
using ObservableCollections;
using R3;
using Reactive.Bindings.R3.Notifiers;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using MangaBinder.Helpers;
using HalationGhost.Wpf.Ui;
using HalationGhost.Wpf.Ui.Navigation;

namespace MangaBinder.Series;

/// <summary>
/// 編集ページの ViewModel です。
/// UI 入力、画面表示、Command、Dialog、Snackbar、Navigation を担当し、
/// 編集・素材操作・検証・保存の業務処理は <see cref="MangaSeriesManager"/> へ委譲します。
/// </summary>
public partial class EditorPageViewModel : IDataInitializable, INavigationLeavingAware, INavigationDisposable
{
	private readonly IServiceScopeFactory serviceScopeFactory;
	private readonly EditorStore editorStore;
	private readonly IContentDialogService contentDialogService;
	private readonly INavigationService navigationService;
	private readonly MangaSeriesStore mangaSeriesStore;
	private readonly TagStore tagStore;
	private readonly ISnackbarService snackbarService;
	private readonly LoadingService loadingService;
	private readonly ThumbnailImageLoader thumbnailImageLoader;
	private DisposableBag disposableBag;
	private NotifyCollectionChangedSynchronizedViewList<TagNotifier>? selectedTagsView;
	private CancellationTokenSource? materialSizeCancellationTokenSource;
	private bool isInitializing;
	private bool disposed;

	/// <summary>編集対象の Series を取得します。</summary>
	public BindableReactiveProperty<MangaSeries?> EditingSeries { get; }

	/// <summary>
	/// タグ選択 Popup 用 ViewModel です。Editor では Popup 終了時の Dirty 登録は行わず、既存の作品保存処理で反映します。
	/// </summary>
	public SeriesTagPickerViewModel TagPicker { get; }

	/// <summary>
	/// 選択済みタグ表示用の共有 <see cref="TagNotifier"/> 一覧を取得します。
	/// 対象作品の <see cref="MangaSeriesNotifier.Tags"/> と同期し、タグ名の変更・タグの追加・削除を反映します。
	/// 要素の <see cref="TagNotifier"/> は <see cref="TagStore"/> の所有物のため Dispose しません。
	/// </summary>
	public BindableReactiveProperty<NotifyCollectionChangedSynchronizedViewList<TagNotifier>?> SelectedTags { get; }

	/// <summary>タイトルを取得または設定します。</summary>
	public BindableReactiveProperty<string?> Title { get; }

	/// <summary>
	/// タイトル入力欄へのフォーカス要求カウンタを取得または設定します。
	/// FocusAndSelectAllBehavior.Request にバインドされ、値が変化するとフォーカスが移ります。
	/// </summary>
	public BindableReactiveProperty<int> TitleFocusRequest { get; }

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

	/// <summary>未完作品として手動設定されているかどうかを取得または設定します。</summary>
	public BindableReactiveProperty<bool> IsIncomplete { get; }

	/// <summary>全巻所持が編集可能かどうかを取得または設定します。完結巻が null ではない場合のみ編集可能です。</summary>
	public BindableReactiveProperty<bool> CanEditOwnedCompleted { get; }

	/// <summary>EditorPage 全体の初期化要求を取得します。</summary>
	public BooleanNotifier EditorPageInitializeRequest { get; }

	/// <summary>
	/// WPFのXAMLバインディングに対応した素材ファイル一覧のViewを取得します。
	/// <see cref="EditorStore.MaterialFiles"/> を表示用に変換した同期Viewです。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<MaterialFileItemViewModel> MaterialFilesView { get; }

	/// <summary>素材ファイル一覧のヘッダー表示用文字列を取得します。</summary>
	public BindableReactiveProperty<string> MaterialFilesDisplay { get; }

	/// <summary>サマリカード用の素材ファイル数表示テキストを取得します。</summary>
	public BindableReactiveProperty<string> MaterialFileCountText { get; }

	/// <summary>素材の合計サイズ表示テキストを取得します。</summary>
	public BindableReactiveProperty<string> MaterialTotalSizeText { get; }

	/// <summary>素材ファイルが空かどうかを取得します。EmptyState の表示制御に使用します。</summary>
	public BindableReactiveProperty<bool> IsMaterialFilesEmpty { get; }

	/// <summary>素材ファイルが存在するかどうかを取得します。ListView の表示制御に使用します。</summary>
	public BindableReactiveProperty<bool> HasMaterialFiles { get; }

	/// <summary>完結巻入力中テキストを処理し、CanEditOwnedCompleted を制御するコマンドを取得します。</summary>
	public ReactiveCommand<string?> EndVolumeTextInputCommand { get; }

	/// <summary>巻情報編集用の ViewModel を取得します。</summary>
	public EditorSeriesVolumeStatusViewModel VolumeStatus { get; }

	/// <summary>
	/// 素材フォルダ作成日時表示用の ViewModel を取得します。
	/// </summary>
	public BindableReactiveProperty<MaterialFolderCreatedAtViewModel> MaterialFolderCreatedAtViewModel { get; }

	/// <summary>
	/// 作品サムネイルカード用の ViewModel を取得します。
	/// Header、ThumbnailSource、VolumeStatus を管理し、左側パネルのサムネイルカードに使用されます。
	/// </summary>
	public MangaSeriesCardViewModel MangaSeriesCard { get; }

	/// <summary>
	/// サムネイルプレビュー画像を取得または設定します。
	/// クリップボードから貼り付けた画像が優先表示されます。
	/// null の場合は、既存のサムネイル（EditingSeries.Value のサムネイル）が表示されます。
	/// </summary>
	public BindableReactiveProperty<BitmapSource?> ThumbnailPreviewImageSource { get; }

	/// <summary>
	/// サムネイルの JPEG byte[] を一時保持します。
	/// ファイル参照と貼り付けの両方で使用されます。
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
	/// 編集内容を破棄して前の画面へ戻るコマンド（キャンセル）を取得します。
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
	/// 製本待ちへの登録状態を取得します。
	/// <see cref="EditorStore.IsBindingQueued"/> を共有し、所有・破棄はストアが行います。
	/// </summary>
	public BindableReactiveProperty<bool> IsBindingQueued { get; }

	/// <summary>
	/// あらすじ貼付整形の有効状態を取得または設定します。
	/// 整形貼付ToggleSwitchのON/OFF状態を管理するEditor画面ローカル状態です。
	/// </summary>
	public BindableReactiveProperty<bool> IsDescriptionPasteFormattingEnabled { get; }

	/// <summary>
	/// 作品を正式に MangaSeries へ登録するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> RegisterSeriesCommand { get; }

	/// <summary>
	/// WPFのXAMLバインディングに対応した素材フォルダ一覧のViewを取得します。
	/// <see cref="EditorStore.MaterialSourceFolders"/> の同期Viewです。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<SourceFolder> MaterialSourceFoldersView { get; }

	/// <summary>
	/// 登録先に選択された素材フォルダを取得します。
	/// <see cref="EditorStore.SelectedMaterialSourceFolder"/> を共有し、所有・破棄はストアが行います。
	/// </summary>
	public BindableReactiveProperty<SourceFolder?> SelectedMaterialSourceFolder { get; }

	/// <summary>
	/// 登録先素材フォルダを変更可能かどうかを取得します。
	/// <see cref="EditorStore.CanSelectMaterialSourceFolder"/> を共有し、所有・破棄はストアが行います。
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
	/// 素材一覧から追加素材を削除するコマンドを取得します。
	/// 削除対象の MaterialFileItemViewModel を CommandParameter として受け取ります。
	/// </summary>
	public ReactiveCommand<MaterialFileItemViewModel?> RemoveMaterialFileCommand { get; }

	/// <summary>
	/// 素材フォルダを開くコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> OpenMaterialFolderCommand { get; }

	/// <summary>
	/// 作品を削除できるかどうかを取得します。
	/// SeriesId != 0 または WorkId != 0 の場合に true になります。
	/// </summary>
	public BindableReactiveProperty<bool> CanDeleteSeries { get; }

	/// <summary>
	/// 作品削除ダイアログを表示するコマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> ShowDeleteSeriesDialogCommand { get; }

	/// <summary>
	/// <see cref="EditorPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="serviceScopeFactory">サービススコープファクトリ。</param>
	/// <param name="editorStore">編集セッション状態を保持するストア。</param>
	/// <param name="contentDialogService">コンテンツダイアログサービス。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="mangaSeriesStore">漫画シリーズストア。</param>
	/// <param name="tagStore">タグマスタの正本を保持するストア。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	/// <param name="loadingService">ローディングサービス。</param>
	/// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
	public EditorPageViewModel(
		IServiceScopeFactory serviceScopeFactory,
		EditorStore editorStore,
		IContentDialogService contentDialogService,
		INavigationService navigationService,
		MangaSeriesStore mangaSeriesStore,
		TagStore tagStore,
		ISnackbarService snackbarService,
		LoadingService loadingService,
		ThumbnailImageLoader thumbnailImageLoader)
	{
		this.serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
		this.editorStore = editorStore ?? throw new ArgumentNullException(nameof(editorStore));
		this.contentDialogService = contentDialogService ?? throw new ArgumentNullException(nameof(contentDialogService));
		this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
		this.mangaSeriesStore = mangaSeriesStore ?? throw new ArgumentNullException(nameof(mangaSeriesStore));
		this.tagStore = tagStore ?? throw new ArgumentNullException(nameof(tagStore));
		this.snackbarService = snackbarService ?? throw new ArgumentNullException(nameof(snackbarService));
		this.loadingService = loadingService ?? throw new ArgumentNullException(nameof(loadingService));
		this.thumbnailImageLoader = thumbnailImageLoader ?? throw new ArgumentNullException(nameof(thumbnailImageLoader));

		this.EditingSeries = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		// TagPicker の初期化
		this.TagPicker = new SeriesTagPickerViewModel(tagStore, serviceScopeFactory)
			.AddTo(ref this.disposableBag);

		this.SelectedTags = new BindableReactiveProperty<NotifyCollectionChangedSynchronizedViewList<TagNotifier>?>(null)
			.AddTo(ref this.disposableBag);

		this.Title = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		this.TitleFocusRequest = new BindableReactiveProperty<int>(0)
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

		this.IsIncomplete = new BindableReactiveProperty<bool>(false)
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

		this.EditorPageInitializeRequest = new BooleanNotifier(false);

		// MaterialFilesView: EditorStore の素材一覧（正本）を表示用 ViewModel へ変換する WPF バインディング対応の同期 View
		this.MaterialFilesView = this.editorStore.MaterialFiles
			.CreateView(item => MaterialFileItemViewModel.FromDto(item))
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// MaterialFilesDisplay: MaterialFiles.Count に基づいて表示文字列を生成
		this.MaterialFilesDisplay = new BindableReactiveProperty<string>(this.getMaterialFilesDisplayText())
			.AddTo(ref this.disposableBag);

		// MaterialFileCountText: サマリカード用の素材ファイル数表示テキスト
		this.MaterialFileCountText = new BindableReactiveProperty<string>(this.getMaterialFileCountText())
			.AddTo(ref this.disposableBag);

		// MaterialTotalSizeText: 素材の合計サイズ表示テキスト
		this.MaterialTotalSizeText = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		// IsMaterialFilesEmpty: EmptyState 表示制御用
		this.IsMaterialFilesEmpty = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		// HasMaterialFiles: ListView 表示制御用
		this.HasMaterialFiles = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// EditorStore.MaterialFiles の変更を監視して表示を更新（Dispose で解除）
		this.editorStore.MaterialFiles.CollectionChanged += this.onMaterialFilesCollectionChanged;

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
		this.PasteThumbnailCommand.Subscribe(async _ =>
		{
			await this.PasteThumbnailAsync();
		});

		// SelectThumbnailCommand: ファイルから画像を選択するコマンド
		this.SelectThumbnailCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.SelectThumbnailCommand.Subscribe(async _ =>
		{
			await this.SelectThumbnailAsync();
		});

		// BackCommand: キャンセルコマンド（編集状態を破棄して戻る）
		this.BackCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.BackCommand.Subscribe(_ =>
		{
			this.cancelEditing();
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

		// IsBindingQueued: 製本待ち登録状態（EditorStore が所有し、ここでは破棄しない）
		this.IsBindingQueued = this.editorStore.IsBindingQueued;

		// IsDescriptionPasteFormattingEnabled: あらすじ貼付整形の有効状態
		this.IsDescriptionPasteFormattingEnabled = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		// RegisterSeriesCommand: 正式登録コマンド
		this.RegisterSeriesCommand = new ReactiveCommand<Unit>(this.RegisterSeriesCommandCanExecute, initialCanExecute: false)
			.AddTo(ref this.disposableBag);
		this.RegisterSeriesCommand.Subscribe(async _ =>
		{
			await this.RegisterSeriesAsync();
		});

		// MaterialSourceFoldersView: EditorStore の素材フォルダ候補を WPF バインディング対応の同期 View として参照
		this.MaterialSourceFoldersView = this.editorStore.MaterialSourceFolders
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// SelectedMaterialSourceFolder / CanSelectMaterialSourceFolder: EditorStore が所有し、ここでは破棄しない
		this.SelectedMaterialSourceFolder = this.editorStore.SelectedMaterialSourceFolder;
		this.CanSelectMaterialSourceFolder = this.editorStore.CanSelectMaterialSourceFolder;

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

		// OpenMaterialFolderCommand: 素材フォルダを開くコマンド
		this.OpenMaterialFolderCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.OpenMaterialFolderCommand.Subscribe(async _ =>
		{
			await this.openMaterialFolderAsync();
		});

		// RemoveMaterialFileCommand: 追加素材を一覧から削除するコマンド
		this.RemoveMaterialFileCommand = new ReactiveCommand<MaterialFileItemViewModel?>()
			.AddTo(ref this.disposableBag);
		this.RemoveMaterialFileCommand.Subscribe(targetItem =>
		{
			this.RemoveMaterialFile(targetItem);
		});

		// VolumeStatus: 巻情報編集用ViewModel
		this.VolumeStatus = new EditorSeriesVolumeStatusViewModel()
			.AddTo(ref this.disposableBag);

		// MaterialFolderCreatedAtViewModel: 素材フォルダ作成日時表示用ViewModel
		var materialFolderCreatedAtViewModel = new MaterialFolderCreatedAtViewModel();
		this.MaterialFolderCreatedAtViewModel = new BindableReactiveProperty<MaterialFolderCreatedAtViewModel>(materialFolderCreatedAtViewModel)
			.AddTo(ref this.disposableBag);

		materialFolderCreatedAtViewModel.AddTo(ref this.disposableBag);

		// MangaSeriesCard: 作品サムネイルカード用ViewModel
		this.MangaSeriesCard = new MangaSeriesCardViewModel()
		{
			IsEditMode = true
		}
			.AddTo(ref this.disposableBag);

		// MangaSeriesCard の Header を "作品サムネイル" に初期設定
		this.MangaSeriesCard.Header.Value = "作品サムネイル";

		// Editor では SeriesMemo を表示しない（メモは画面右側に直接編集UIがある）
		this.MangaSeriesCard.ShowMemo.Value = false;

		// VolumeStatus.DisplayStatus の変更を購読し、MangaSeriesCard.VolumeStatus へ Reactive に反映
		this.VolumeStatus.DisplayStatus
			.Subscribe(displayStatus =>
			{
				this.MangaSeriesCard.VolumeStatus.Value = displayStatus;
			})
			.AddTo(ref this.disposableBag);

		// MangaSeriesCard の ThumbnailSource: ThumbnailPreviewImageSource と EditingSeries から最終表示画像を導出
		// ・ThumbnailPreviewImageSource が null でなければそれを使用
		// ・null かつ EditingSeries が null でなければ ThumbnailImageLoader.Load() の結果を使用
		// ・両方 null なら null
		// （MangaSeriesCard 生成後に配置し、Subscribe 即時発火時に MangaSeriesCard が存在することを保証）
		this.ThumbnailPreviewImageSource
			.CombineLatest(this.EditingSeries, (preview, series) =>
			{
				if (preview != null)
				{
					return (ImageSource?)preview;
				}

				if (series != null)
				{
					return this.thumbnailImageLoader.Load(series);
				}

				return null;
			})
			.Subscribe(imageSource =>
			{
				this.MangaSeriesCard.ThumbnailSource.Value = imageSource;
			})
			.AddTo(ref this.disposableBag);

		// IsIncomplete の変更を VolumeStatus の表示用 DisplayStatus へ同期（表示色反映のため）
		this.IsIncomplete
			.Subscribe(isIncomplete =>
			{
				if (this.VolumeStatus.DisplayStatus.Value != null)
				{
					this.VolumeStatus.DisplayStatus.Value.IsIncomplete.Value = isIncomplete;
				}
			})
			.AddTo(ref this.disposableBag);

		// CanDeleteSeries: 作品削除ボタンの表示制御
		this.CanDeleteSeries = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// EditingSeries が変更された場合、各 ViewModel と UI 状態を更新
		this.EditingSeries
			.Subscribe(editingSeries =>
			{
				if (editingSeries != null)
				{
					// SeriesId != 0 または WorkId != 0 の場合に削除可能
					this.CanDeleteSeries.Value = editingSeries.SeriesId != 0 || editingSeries.WorkId != 0;
				}
				else
				{
					this.CanDeleteSeries.Value = false;
				}

				// 素材フォルダ作成日時表示用の通知を流す
				if (this.MaterialFolderCreatedAtViewModel.Value.Series.Value != editingSeries)
				{
					this.MaterialFolderCreatedAtViewModel.Value.Series.Value = editingSeries;
				}
				else if (this.MaterialFolderCreatedAtViewModel.Value.Series.Value == editingSeries)
				{
					// 同一インスタンスの場合は ForceNotify() で再通知させる
					this.MaterialFolderCreatedAtViewModel.Value.Series.ForceNotify();
				}

				// MangaSeriesCard へ共有 MangaSeriesViewModel を接続
				this.connectSeriesViewModel(editingSeries);
			})
			.AddTo(ref this.disposableBag);

		// ShowDeleteSeriesDialogCommand: 作品削除ダイアログ表示コマンド
		this.ShowDeleteSeriesDialogCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.ShowDeleteSeriesDialogCommand.Subscribe(async _ =>
		{
			await this.showDeleteSeriesDialogAsync();
		});

		// 接続された共有 MangaSeriesNotifier のタグを、選択済みタグ表示へ連動する
		this.MangaSeriesCard.SeriesViewModel
			.Subscribe(this.onSeriesViewModelChanged)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 接続対象の MangaSeriesNotifier が変わったとき、選択済みタグ表示用の同期 View を作り直します。
	/// Notifier と TagNotifier は共有物のため Dispose せず、ここで生成した View だけを破棄します。
	/// </summary>
	private void onSeriesViewModelChanged(MangaSeriesNotifier? notifier)
	{
		this.SelectedTags.Value = null;
		this.selectedTagsView?.Dispose();
		this.selectedTagsView = notifier?.Tags
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
		this.SelectedTags.Value = this.selectedTagsView;
	}

	/// <summary>
	/// 作品カードへ渡す MangaSeriesNotifier を接続します。
	/// 編集用クローン専用の Notifier を <see cref="EditorStore"/> から取得します。
	/// Notifier の所有・破棄は <see cref="EditorStore"/> が行うため、ここでは破棄しません。
	/// </summary>
	private void connectSeriesViewModel(MangaSeries? editingSeries)
	{
		if (editingSeries is null)
		{
			this.MangaSeriesCard.SeriesViewModel.Value = null;
			return;
		}

		var notifier = this.editorStore.EditTargetNotifier
			?? throw new InvalidOperationException("EditorStore.EditTargetNotifier が初期化されていません。");
		if (!ReferenceEquals(notifier.Series.Value, editingSeries))
		{
			throw new InvalidOperationException("EditorStore.EditTargetNotifier と編集対象の MangaSeries インスタンスが一致しません。");
		}

		this.MangaSeriesCard.SeriesViewModel.Value = notifier;
	}

	/// <summary>
	/// 素材ファイル一覧のヘッダー表示文字列を取得します。
	/// </summary>
	/// <returns>0件の場合は "無し"、1件以上の場合は "{Count}件"。</returns>
	private string getMaterialFilesDisplayText()
	{
		return this.editorStore.MaterialFiles.Count == 0
			? "無し"
			: $"{this.editorStore.MaterialFiles.Count}件";
	}

	/// <summary>
	/// サマリカード用の素材ファイル数表示テキストを取得します。
	/// </summary>
	/// <returns>0件の場合は "0件"、1件以上の場合は "{Count}件"。</returns>
	private string getMaterialFileCountText()
	{
		return this.editorStore.MaterialFiles.Count == 0
			? "0件"
			: $"{this.editorStore.MaterialFiles.Count}件";
	}

	/// <summary>
	/// 素材一覧の件数に依存する表示用プロパティを Store の内容から更新します。
	/// </summary>
	private void refreshMaterialFilesDisplay()
	{
		this.MaterialFilesDisplay.Value = this.getMaterialFilesDisplayText();
		this.MaterialFileCountText.Value = this.getMaterialFileCountText();

		var isEmpty = this.editorStore.MaterialFiles.Count == 0;
		this.IsMaterialFilesEmpty.Value = isEmpty;
		this.HasMaterialFiles.Value = !isEmpty;
	}

	/// <summary>
	/// Store の素材一覧が変更されたときに表示用状態を更新します。
	/// </summary>
	/// <param name="e">コレクション変更イベント引数。</param>
	private void onMaterialFilesCollectionChanged(in NotifyCollectionChangedEventArgs<MaterialFileItem> e)
	{
		this.refreshMaterialFilesDisplay();

		if (!this.isInitializing)
		{
			_ = this.updateMaterialTotalSizeAsync();
		}
	}

	/// <summary>
	/// 一時保存ボタンの有効/無効状態を更新します。
	/// 新規作品・登録待ち作品の場合は一時保存ボタンが有効、既存作品の場合は登録ボタンが有効になります。
	/// タイトル確定状態も条件に含まれます。
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
		if (this.editorStore.MaterialFiles.Count > 0)
		{
			this.SaveWorkSeriesCommandCanExecute.Value = false;
			this.RegisterSeriesCommandCanExecute.Value = true;
			return;
		}

		// 新規作品・登録待ち作品で素材なし
		// 一時保存は常に可能（実際の保存時にタイトル必須チェックが行われる）
		this.SaveWorkSeriesCommandCanExecute.Value = true;
		this.RegisterSeriesCommandCanExecute.Value = false;
	}

	/// <summary>
	/// ナビゲーション完了後に呼ばれる初期データ読み込み処理。
	/// 編集用データは MangaSeriesManager が準備済みの <see cref="EditorStore"/> を参照し、
	/// ここでは画面表示用の値を反映するだけで、業務データの再取得・再構築は行いません。
	/// </summary>
	public ValueTask InitializeDataAsync()
	{
		// 初期化開始を記録
		this.isInitializing = true;

		try
		{
			var editingSeries = this.editorStore.EditTarget.Value
				?? throw new InvalidOperationException("EditorStore が初期化されていません。MangaSeriesManager.PrepareEditAsync を完了してから画面遷移してください。");

			this.EditingSeries.Value = editingSeries;

			this.Title.Value = editingSeries.Title;
			this.Author.Value = editingSeries.Author;
			this.Publisher.Value = editingSeries.Publisher;
			this.Description.Value = editingSeries.Description;
			this.Memo.Value = editingSeries.Memo;
			this.IsIncomplete.Value = editingSeries.IsIncomplete;

			// 巻情報を VolumeStatus に読み込み
			this.VolumeStatus.LoadFromSeries(editingSeries);

			// 素材一覧の表示状態を Store の内容から反映
			this.refreshMaterialFilesDisplay();
			this.MaterialTotalSizeText.Value = this.editorStore.MaterialFiles.Count == 0
				? string.Empty
				: StorageSizeHelper.FormatSize(this.editorStore.MaterialTotalSizeBytes);

			// 一時保存ボタンの有効/無効を更新
			this.UpdateSaveWorkSeriesCommandCanExecute();

			// 貼り付けたサムネイルをクリア
			this.ClearPastedThumbnail();

			// EditorPage 全体を初期表示状態へ戻す
			this.EditorPageInitializeRequest.SwitchValue();

			// タイトル入力欄へフォーカスして全選択する
			this.TitleFocusRequest.Value++;

			return ValueTask.CompletedTask;
		}
		finally
		{
			// 初期化完了を記録
			this.isInitializing = false;
		}
	}

	/// <summary>
	/// 素材ファイルの合計サイズを非同期で計算し、表示を更新します。
	/// 前回の計算がある場合はキャンセルしてから新規計算を開始します。
	/// OperationCanceledException は内部で処理され、外へ伝播しません。
	/// </summary>
	private async ValueTask updateMaterialTotalSizeAsync()
	{
		// 前回の計算をキャンセル
		if (this.materialSizeCancellationTokenSource != null)
		{
			this.materialSizeCancellationTokenSource.Cancel();
			this.materialSizeCancellationTokenSource.Dispose();
		}

		// 新しいキャンセルトークンソースを作成
		this.materialSizeCancellationTokenSource = new CancellationTokenSource();
		var currentToken = this.materialSizeCancellationTokenSource.Token;

		try
		{
			if (this.editorStore.MaterialFiles.Count == 0)
			{
				this.MaterialTotalSizeText.Value = string.Empty;
				return;
			}

			// 合計サイズの計算は Manager の責務
			using var scope = this.serviceScopeFactory.CreateScope();
			var manager = scope.ServiceProvider.GetRequiredService<MangaSeriesManager>();
			var totalBytes = await manager.CalculateMaterialTotalSizeAsync(currentToken);

			// キャンセルされた古い計算結果が MaterialTotalSizeText を更新しないようにチェック
			if (!currentToken.IsCancellationRequested)
			{
				this.MaterialTotalSizeText.Value = StorageSizeHelper.FormatSize(totalBytes);
			}
		}
		catch (OperationCanceledException)
		{
			// キャンセルは正常終了として扱う（外へは伝播させない）
		}
	}

	/// <summary>
	/// 画面から離れる際の後処理。
	/// 遷移先が状態保持を要求していない場合に、編集セッション固有の UI リソースと編集状態を破棄します。
	/// 保存成功・キャンセルで既に破棄済みの場合は何もしません。
	/// </summary>
	/// <param name="request">遷移先から受け取った要求。</param>
	public ValueTask OnNavigatingFromAsync(NavigationLeavingRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!request.PreserveState)
		{
			this.closeEditSession();
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// 編集セッションを終了します。
	/// 画面の編集対象と貼り付けサムネイルを解放し、<see cref="EditorStore.EditTarget"/> を null にして
	/// Store 側の既存クリア処理で編集状態（編集用クローン・Notifier・素材一覧など）を破棄します。
	/// 正本（<see cref="EditorStore.OriginalSeries"/>）や共有 Notifier には影響しません。
	/// </summary>
	private void closeEditSession()
	{
		this.ClearPastedThumbnail();

		// 編集用 Notifier が破棄される前に、画面側の接続を外す
		this.EditingSeries.Value = null;
		this.editorStore.EditTarget.Value = null;
	}

	/// <summary>
	/// 編集をキャンセルします。
	/// DB 保存・正本更新・素材ファイル移動は行わず、編集状態を破棄して前画面へ戻ります。
	/// </summary>
	private void cancelEditing()
	{
		this.closeEditSession();
		this.navigationService.GoBack();
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
	/// ContentDialog を生成して表示し、ユーザーの選択結果を返します。
	/// </summary>
	/// <param name="title">ダイアログのタイトル。</param>
	/// <param name="content">ダイアログのコンテンツ。string または UserControl を指定します。</param>
	/// <param name="primaryButtonText">主ボタンのテキスト。</param>
	/// <param name="closeButtonText">閉じるボタンのテキスト。</param>
	/// <returns>ユーザーの選択結果。</returns>
	private async ValueTask<ContentDialogResult> ShowContentDialogAsync(
		string title,
		object content,
		string primaryButtonText,
		string closeButtonText)
	{
		// ContentDialog を生成
		var dialog = new ContentDialog
		{
			Title = title,
			Content = content,
			PrimaryButtonText = primaryButtonText,
			CloseButtonText = closeButtonText,
		};

		// ダイアログを表示して結果を返す
		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
		return result;
	}

	/// <summary>
	/// 作品削除ダイアログを非同期で表示し、ユーザーの選択結果を返します。
	/// </summary>
	private async ValueTask showDeleteSeriesDialogAsync()
	{
		// 編集中の作品を取得
		var editingSeries = this.EditingSeries.Value;
		if (editingSeries == null)
		{
			return;
		}

		// 正式作品のみが対象
		if (editingSeries.IsWork)
		{
			// 登録待ち作品は未実装
			return;
		}

		// Scope を生成して MangaSeriesManager を取得
		using var scope = this.serviceScopeFactory.CreateScope();
		var mangaSeriesManager = scope.ServiceProvider.GetRequiredService<MangaBinderManager>();

		// IsBindingQueued() が true の場合は削除ダイアログを表示しない
		if (mangaSeriesManager.IsBindingQueued(editingSeries.SeriesId))
		{
			this.snackbarService.Show(
				"削除できません",
				"この作品は製本待ちに登録されています。先に製本待ちから削除してください。",
				ControlAppearance.Caution,
				new SymbolIcon { Symbol = SymbolRegular.Info24 },
				TimeSpan.FromSeconds(5));
			return;
		}

		// 素材ファイルが存在するかどうかを判定（現在保持している MaterialFiles の状態を利用）
		var hasMaterialFiles = this.editorStore.MaterialFiles.Count > 0;

		// ダイアログ用 ViewModel を生成（削除対象作品と素材有無を渡す）
		var dialogViewModel = new SeriesDeleteDialogContentViewModel(editingSeries, hasMaterialFiles);

		// ダイアログコンテンツを作成
		var content = new SeriesDeleteDialogContent
		{
			DataContext = dialogViewModel,
		};

		// ダイアログを表示して結果を取得
		var result = await this.ShowContentDialogAsync(
			"作品を削除",
			content,
			"削除",
			"キャンセル");

		// ユーザーが「削除」を選択した場合
		if (result == ContentDialogResult.Primary)
		{
			// 選択された削除方法を取得
			var deleteMethod = dialogViewModel.SelectedDeleteMethod.Value;

			// 作品削除処理を実行
			await mangaSeriesManager.DeleteExistingSeriesAsync(editingSeries, deleteMethod);

			// 削除成功後に前画面へ戻る
			this.navigationService.GoBack();
		}
		else
		{
			// キャンセルした場合、何もしない
		}

		// ダイアログ終了後に ViewModel を Dispose
		dialogViewModel.Dispose();
	}

	/// <summary>
	/// クリップボードからサムネイルを貼り付けます。
	/// 成功時はプレビュー画像を更新し、JPEG byte[] を保持します。
	/// 失敗またはキャンセル時は何もしません。
	/// </summary>
	private async ValueTask PasteThumbnailAsync()
	{
		try
		{
			// Scope を生成
			using var scope = this.serviceScopeFactory.CreateScope();
			var thumbnailPicker = scope.ServiceProvider.GetRequiredService<ThumbnailPicker>();

			// ThumbnailPicker からクリップボード画像を処理
			var result = await thumbnailPicker.PickFromClipboardAsync(CancellationToken.None);

			if (result.IsCanceled)
			{
				// クリップボードに画像がない場合は何もしない
				return;
			}

			if (!result.Success || result.PreviewImage == null || result.ThumbnailBytes == null)
			{
				// 処理失敗時：ログのみ出力
				var errorMessage = result.ErrorMessage ?? "画像の処理に失敗しました。";
				System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.PasteThumbnailAsync] {errorMessage}");
				return;
			}

			// 成功時：プレビュー画像を更新し、JPEG byte[] を保持
			this.ThumbnailPreviewImageSource.Value = result.PreviewImage;
			this.PastedThumbnailBytes = result.ThumbnailBytes;
		}
		catch (Exception ex)
		{
			// 予期しないエラーが発生した場合もログ出力のみ
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.PasteThumbnailAsync] 例外発生: {ex.Message}");
		}
	}

	/// <summary>
	/// サムネイルの JPEG byte[] を取得します。
	/// 参照と貼り付けで設定され、保存処理で利用します。
	/// </summary>
	/// <returns>サムネイル JPEG byte[]、存在しない場合は null。</returns>
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
			// Scope を生成
			using var scope = this.serviceScopeFactory.CreateScope();
			var thumbnailPicker = scope.ServiceProvider.GetRequiredService<ThumbnailPicker>();

			// ThumbnailPicker からファイル選択ダイアログを表示
			var result = await thumbnailPicker.PickFromFileAsync(CancellationToken.None);

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
	/// 画面の入力値を Manager へ渡すための入力値にまとめます。
	/// 編集用クローンへの反映は Manager が行います。
	/// </summary>
	/// <returns>画面で入力された値。</returns>
	private EditorInputValues createInputValues()
	{
		return new EditorInputValues
		{
			Title = this.Title.Value,
			Author = this.Author.Value,
			Publisher = this.Publisher.Value,
			Description = this.Description.Value,
			Memo = this.Memo.Value,
			IsIncomplete = this.IsIncomplete.Value,
			StartVolume = this.VolumeStatus.StartVolume.Value,
			EndVolume = this.VolumeStatus.EndVolume.Value,
			OwnedMaxVolume = this.VolumeStatus.OwnedMaxVolume.Value,
			IsOwnedCompleted = this.VolumeStatus.IsOwnedCompleted.Value,
		};
	}

	/// <summary>
	/// 素材追加時に重複が発見された場合の Warning Snackbar を表示します。
	/// </summary>
	/// <param name="alreadyAddedFiles">既に追加済みのファイル名リスト。</param>
	/// <param name="sameNameFiles">同名の素材ファイル名リスト。</param>
	private void showMaterialDuplicateWarningSnackbar(IReadOnlyList<string> alreadyAddedFiles, IReadOnlyList<string> sameNameFiles)
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
	/// 検証・保存は MangaSeriesManager へ委譲し、ここでは入力値の受け渡しと結果の通知・画面遷移だけを行います。
	/// 成功後は編集状態を破棄して作品管理画面へ戻ります。
	/// </summary>
	private async ValueTask SaveWorkSeriesAsync()
	{
		if (this.EditingSeries.Value == null)
			return;

		try
		{
			using var scope = this.serviceScopeFactory.CreateScope();
			var manager = scope.ServiceProvider.GetRequiredService<MangaSeriesManager>();

			// タイトル必須チェック（入力値の反映を含む）
			if (manager.ValidateForWorkSave(this.createInputValues()) != EditorValidationError.None)
			{
				this.showErrorSnackbar("エラー", "タイトルを入力してください。");
				return;
			}

			// サムネイル JPEG byte[] を取得
			var thumbnailBytes = this.GetPastedThumbnailBytes();

			// ローディング開始
			using (this.loadingService.Begin("作品を一時保存しています…"))
			{
				await Task.Yield();

				// WorkMangaSeries へ一時保存
				var workId = await manager.SaveWorkSeriesAsync(thumbnailBytes);

				// 成功ログ
				System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.SaveWorkSeriesAsync] 一時保存成功。WorkId={workId}");
			}

			// 編集状態を破棄して作品管理画面へ戻る
			this.closeEditSession();
			this.navigationService.Navigate(typeof(MaintenancePage));
		}
		catch (Exception ex)
		{
			// エラーが発生した場合はログとユーザー通知
			// 開発中のアプリであるため、Exception.Message と StackTrace を表示
			var errorMessageBody = $"{ex.Message}\n\nStackTrace:\n{ex.StackTrace}";
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.SaveWorkSeriesAsync] 例外発生: {ex}");

			// VolumeSelectionPage と同じ通知方式：赤色（Danger）で自動では閉じない
			this.showErrorSnackbar("一時保存に失敗しました", errorMessageBody);
		}
	}

	/// <summary>
	/// 赤色（Danger）でユーザーが閉じるまで表示するエラー Snackbar を表示します。
	/// </summary>
	/// <param name="title">タイトル。</param>
	/// <param name="message">本文。</param>
	private void showErrorSnackbar(string title, string message)
	{
		this.snackbarService.Show(
			title,
			message,
			ControlAppearance.Danger,
			new SymbolIcon { Symbol = SymbolRegular.Warning24 },
			TimeSpan.MaxValue);
	}

	/// <summary>
	/// ドラッグアンドドロップされたファイル/フォルダを素材として追加します。
	/// 素材の解析・重複判定・追加・並び替え・所持最大巻数の推定は MangaSeriesManager が行い、
	/// ここでは Loading 表示、結果の画面反映、重複時の Snackbar 通知だけを行います。
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
			using var scope = this.serviceScopeFactory.CreateScope();
			var manager = scope.ServiceProvider.GetRequiredService<MangaSeriesManager>();

			// ローディング開始
			using (this.loadingService.Begin("素材を追加しています..."))
			{
				await Task.Yield();

				var result = manager.AddMaterialFiles(droppedPaths);

				if (result.Added)
				{
					// ボタン有効制御を更新
					this.UpdateSaveWorkSeriesCommandCanExecute();

					// 自動推定された所持最大巻数を入力欄へ反映
					this.reflectOwnedMaxVolumeIfUpdated(result.OwnedMaxVolumeUpdated);

					// 素材サイズを計算
					await this.updateMaterialTotalSizeAsync();
				}

				// 追加できなかった素材の通知
				this.showMaterialDuplicateWarningSnackbar(result.AlreadyAddedFiles, result.SameNameFiles);
			}
		}
		catch (Exception ex)
		{
			// ドラッグアンドドロップ処理中のエラーをログ出力
			System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.AddMaterialFilesFromDropAsync] 例外発生: {ex.Message}");
		}
	}

	/// <summary>
	/// Manager が編集用クローンの所持最大巻数を自動推定で更新した場合に、その値を入力欄へ反映します。
	/// </summary>
	/// <param name="ownedMaxVolumeUpdated">自動推定により所持最大巻数が更新された場合は true。</param>
	private void reflectOwnedMaxVolumeIfUpdated(bool ownedMaxVolumeUpdated)
	{
		if (!ownedMaxVolumeUpdated)
		{
			return;
		}

		var target = this.editorStore.EditTarget.Value
			?? throw new InvalidOperationException("EditorStore が初期化されていません。");
		this.VolumeStatus.OwnedMaxVolume.Value = target.OwnedMaxVolume;
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
	/// 編集中の作品を正式な MangaSeries として保存します。
	/// 検証・保存方式（新規登録・既存作品更新）の判定・保存は MangaSeriesManager の責務です。
	/// ここでは入力値の受け渡し、確認 Dialog、Snackbar、画面遷移だけを行います。
	/// タイトルの検証は、この登録ボタン押下時にだけ実行されます。
	/// </summary>
	private async ValueTask RegisterSeriesAsync()
	{
		try
		{
			// Scope を生成（検証～保存まで同じ Scope を使用）
			using var scope = this.serviceScopeFactory.CreateScope();
			var manager = scope.ServiceProvider.GetRequiredService<MangaSeriesManager>();

			var editingSeries = this.EditingSeries.Value
				?? throw new InvalidOperationException("編集中の作品情報がありません。");

			// 入力値の反映と検証
			var validationError = manager.ValidateForRegister(this.createInputValues());
			if (validationError != EditorValidationError.None)
			{
				await this.showRegisterValidationErrorAsync(manager, validationError);
				return;
			}

			// 別ドライブへの大量素材移動は確認する
			if (manager.RequiresDifferentDriveConfirmation())
			{
				var differentDriveResult = await this.ShowContentDialogAsync(
					"別ドライブへの移動",
					"現在選択されている登録先素材フォルダは\n元の素材フォルダとは別ドライブです。\n\n素材ファイルはコピーではなく移動されます。\n\n続行しますか？",
					"移動して登録",
					"登録のキャンセル");

				if (differentDriveResult != ContentDialogResult.Primary)
				{
					return;
				}
			}

			// 保存処理のメッセージを決定
			var loadingMessage = editingSeries.SeriesId == 0 || editingSeries.IsWork
				? "作品を登録しています…"
				: "作品を更新しています…";

			SeriesSaveResult saveResult;
			using (this.loadingService.Begin(loadingMessage))
			{
				await Task.Yield();

				// 保存実行
				saveResult = await manager.RegisterSeriesAsync(this.PastedThumbnailBytes);
			}

			// 保存結果の判定
			if (saveResult.Series == null)
			{
				// 全件失敗：編集状態を保持して EditorPage に留まる
				this.UpdateSaveWorkSeriesCommandCanExecute();

				this.snackbarService.Show(
					"素材を移動できませんでした",
					Constants.Messages.MaterialInUse,
					ControlAppearance.Danger,
					new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
					TimeSpan.MaxValue);
			}
			else if (saveResult.FailedItems.Count == 0)
			{
				// 全件成功
				var savedTitle = saveResult.Series.Title;

				this.snackbarService.Show(
					"成功",
					$"『{savedTitle}』を保存しました。",
					ControlAppearance.Success,
					new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24 },
					TimeSpan.FromSeconds(3));

				// 編集状態を破棄して前画面へ戻る
				this.closeEditSession();
				this.navigationService.GoBack();
			}
			else
			{
				// 部分成功：正式登録成功だが一部素材移動失敗
				this.snackbarService.Show(
					"一部の素材を移動できませんでした",
					"作品の登録は完了しましたが、移動できなかった素材が元の場所に残っています。\n他のアプリで開いている場合は終了して、再度実行してください。",
					ControlAppearance.Danger,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.MaxValue);

				// 編集状態を破棄して前画面へ戻る
				this.closeEditSession();
				this.navigationService.GoBack();
			}
		}
		catch (Exception ex)
		{
			this.showSaveFailedSnackbar(ex);
		}
	}

	/// <summary>
	/// 登録時の検証エラーを Dialog または Snackbar で通知します。いずれの場合も保存は続行しません。
	/// </summary>
	/// <param name="manager">編集処理を担当する Manager。</param>
	/// <param name="validationError">検証エラーの種類。</param>
	private async ValueTask showRegisterValidationErrorAsync(MangaSeriesManager manager, EditorValidationError validationError)
	{
		switch (validationError)
		{
			case EditorValidationError.TitleRequired:
				this.showErrorSnackbar("エラー", "タイトルを入力してください。");

				// タイトル入力欄へフォーカスを移す
				this.TitleFocusRequest.Value++;
				break;

			case EditorValidationError.AuthorRequired:
				this.showErrorSnackbar("登録できません", "同一タイトルの作品が登録済みのため、作者を入力してください。");
				break;

			case EditorValidationError.DuplicateSeries:
				this.showErrorSnackbar("登録できません", "同一作品が既に登録されています。");
				break;

			case EditorValidationError.MaterialRequired:
				this.showErrorSnackbar("エラー", "素材ファイルが登録されていません。");
				break;

			case EditorValidationError.DestinationRequired:
				this.showErrorSnackbar("エラー", "登録先の素材フォルダを選択してください。");
				break;

			case EditorValidationError.TitleMismatch:
				await this.showTitleMismatchDialogAsync(manager);
				break;

			default:
				throw new InvalidOperationException($"未対応の検証エラーです: {validationError}");
		}
	}

	/// <summary>
	/// タイトル不一致の ContentDialog を表示します。
	/// 「登録中止」は入力済みタイトルを保持したまま中止し、「タイトルを元に戻す」は編集用クローンのタイトルを元に戻して中止します。
	/// </summary>
	/// <param name="manager">編集処理を担当する Manager。</param>
	private async ValueTask showTitleMismatchDialogAsync(MangaSeriesManager manager)
	{
		var dialog = new ContentDialog
		{
			Title = "タイトル不一致",
			Content = "別作品と判定されたため、登録できません。\n\nタイトルは、同一作品と判定できる範囲でのみ修正できます。\n\n修正できる例\n・全角・半角の統一：１４歳 → 14歳\n・スペースの削除：ドラゴン ボール → ドラゴンボール\n・英字の大小文字：Fate → FATE",
			PrimaryButtonText = "登録中止",
			SecondaryButtonText = "タイトルを元に戻す",
		};

		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);

		if (result == ContentDialogResult.Secondary)
		{
			this.Title.Value = manager.RestoreOriginalTitle();
		}
	}

	/// <summary>
	/// 保存処理で発生した例外を Snackbar で通知します。
	/// </summary>
	/// <param name="ex">発生した例外。</param>
	private void showSaveFailedSnackbar(Exception ex)
	{
		// 素材フォルダ名検証エラーを判定
		if (ex.Message.Contains("素材フォルダ名の変更判定でエラーが発生しました"))
		{
			// 結果文字列を解析して適切なメッセージを表示
			if (ex.Message.Contains(MaterialFolderRenameCheckResult.CurrentFolderNotFound.ToString()))
			{
				this.showErrorSnackbar(
					"エラー",
					"登録されている素材フォルダが見つからないため、作品を保存できません。\n素材フォルダを確認してください。");
			}
			else if (ex.Message.Contains(MaterialFolderRenameCheckResult.RenameTargetAlreadyExists.ToString()))
			{
				this.showErrorSnackbar(
					"エラー",
					"変更後の素材フォルダ名と同じフォルダが既に存在するため、作品を保存できません。\n素材フォルダを確認してください。");
			}
			else if (ex.Message.Contains(MaterialFolderRenameCheckResult.RenameNeeded.ToString()))
			{
				this.showErrorSnackbar(
					"エラー",
					"素材フォルダ名の変更が必要です。\nフォルダRename処理は現在未実装のため、作品を保存できません。");
			}
			else
			{
				this.snackbarService.Show(
					"エラー",
					$"保存に失敗しました: {ex.Message}",
					ControlAppearance.Caution,
					new SymbolIcon { Symbol = SymbolRegular.Warning24 },
					TimeSpan.FromSeconds(3));
			}

			return;
		}

		System.Diagnostics.Debug.WriteLine($"[EditorPageViewModel.RegisterSeriesAsync] 例外発生: {ex.Message}");
		this.showErrorSnackbar("エラー", $"保存に失敗しました: {ex.Message}");
	}

	/// <summary>
	/// ナビゲーション時にリソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// マネージド/アンマネージドリソースを解放します。
	/// </summary>
	/// <param name="disposing">マネージドリソースを解放するかどうかを示す値。</param>
	private void Dispose(bool disposing)
	{
		if (this.disposed)
			return;

		if (disposing)
		{
			this.ClearPastedThumbnail();
			this.selectedTagsView?.Dispose();
			this.selectedTagsView = null;
			this.materialSizeCancellationTokenSource?.Dispose();
			this.editorStore.MaterialFiles.CollectionChanged -= this.onMaterialFilesCollectionChanged;
			this.disposableBag.Dispose();
		}

		this.disposed = true;
	}
}

/// <summary>
/// EditorPageViewModel に関連するメソッド。
/// </summary>
public partial class EditorPageViewModel
{
	/// <summary>
	/// 素材一覧から指定された素材を削除します。
	/// 削除可否の判定と削除、所持最大巻数の推定は MangaSeriesManager が行います。
	/// </summary>
	/// <param name="targetItem">削除対象の MaterialFileItemViewModel。</param>
	private void RemoveMaterialFile(MaterialFileItemViewModel? targetItem)
	{
		// 対象が null の場合は処理しない
		if (targetItem == null)
		{
			return;
		}

		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<MangaSeriesManager>();

		// CollectionChanged イベントでコレクション関連の表示は自動更新される
		var result = manager.RemoveMaterialFile(targetItem.FullPath);
		if (!result.Removed)
		{
			return;
		}

		// 登録/保存コマンドの有効状態を更新
		this.UpdateSaveWorkSeriesCommandCanExecute();

		// 自動推定された所持最大巻数を入力欄へ反映
		this.reflectOwnedMaxVolumeIfUpdated(result.OwnedMaxVolumeUpdated);
	}

	/// <summary>
	/// 編集対象の素材フォルダを開きます。
	/// 編集開始時に特定した実在する素材ソース（<see cref="EditorStore.MaterialSource"/>）を使用します。
	/// </summary>
	private async Task openMaterialFolderAsync()
	{
		if (this.editorStore.MaterialSource is not { } materialSource)
			return;

		// MaterialFolderOpener を取得してフォルダを開く
		using var scope = this.serviceScopeFactory.CreateScope();
		var opener = scope.ServiceProvider.GetRequiredService<MaterialFolderOpener>();
		await opener.OpenAsync(materialSource);
	}
}
