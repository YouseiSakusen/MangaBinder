using MangaBinder.Bindings;
using MangaBinder.Controls;
using MangaBinder.Settings;
using MangaBinder.Tags;
using R3;
using Reactive.Bindings.R3;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Series;

/// <summary>
/// 編集ページの ViewModel です。
/// </summary>
public class EditorPageViewModel : IDataInitializable, IDisposable
{
	private readonly MangaSeriesManager seriesManager;
	private readonly SeriesWorkspaceStore workspaceStore;
	private readonly IContentDialogService contentDialogService;
	private readonly INavigationService navigationService;
	private readonly MangaSeriesStore mangaSeriesStore;
	private readonly ThumbnailPicker thumbnailPicker;
	private readonly ISnackbarService snackbarService;
	private DisposableBag disposableBag;

	/// <summary>編集対象の Series を取得します。</summary>
	public BindableReactiveProperty<MangaSeries?> EditingSeries { get; }

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

	/// <summary>完結巻入力中テキストを処理し、CanEditOwnedCompleted を制御するコマンドを取得します。</summary>
	public ReactiveCommand<string?> EndVolumeTextInputCommand { get; }

	/// <summary>巻情報編集用の ViewModel を取得します。</summary>
	public EditorSeriesVolumeStatusViewModel VolumeStatus { get; }

	/// <summary>
	/// Editor で選択可能なタグ一覧（ポップアップ用チェックボックスリスト）を取得します。
	/// </summary>
	public ObservableCollection<SeriesTagSelectionItem> SelectableTagsForPopup { get; } = new();

	/// <summary>
	/// タグ選択ポップアップの列数を取得します。
	/// </summary>
	public int TagSelectionColumns { get; private set; } = 2;

	/// <summary>
	/// タグ選択ポップアップの行数を取得します。
	/// </summary>
	public int TagSelectionRows { get; private set; }

	/// <summary>
	/// 編集中の作品に付与されているタグを取得します。
	/// </summary>
	public BindableReactiveProperty<ObservableCollection<MangaTag>> EditingTagsCollection { get; }

	/// <summary>
	/// タグポップアップを開く前に、対象作品のチェック状態を準備するコマンドです。
	/// </summary>
	public ReactiveCommand<Unit> PrepareTagPopupCommand { get; }

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
	/// EditorPageViewModel の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="seriesManager">作品管理マネージャー。</param>
	/// <param name="workspaceStore">作業領域ストア。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="mangaSeriesStore">作品タグストア。</param>
	/// <param name="thumbnailPicker">サムネイル操作ピッカー。</param>
	/// <param name="thumbnailImageProcessor">サムネイル画像処理。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="snackbarService">Snackbar サービス。</param>
	public EditorPageViewModel(
		MangaSeriesManager seriesManager,
		SeriesWorkspaceStore workspaceStore,
		IContentDialogService contentDialogService,
		INavigationService navigationService,
		MangaSeriesStore mangaSeriesStore,
		ThumbnailPicker thumbnailPicker,
		ISnackbarService snackbarService)
	{
		this.seriesManager = seriesManager ?? throw new ArgumentNullException(nameof(seriesManager));
		this.workspaceStore = workspaceStore ?? throw new ArgumentNullException(nameof(workspaceStore));
		this.contentDialogService = contentDialogService ?? throw new ArgumentNullException(nameof(contentDialogService));
		this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
		this.mangaSeriesStore = mangaSeriesStore ?? throw new ArgumentNullException(nameof(mangaSeriesStore));
		this.thumbnailPicker = thumbnailPicker ?? throw new ArgumentNullException(nameof(thumbnailPicker));
		this.snackbarService = snackbarService ?? throw new ArgumentNullException(nameof(snackbarService));

		this.EditingSeries = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		// 編集中タグコレクションの初期化
		this.EditingTagsCollection = new BindableReactiveProperty<ObservableCollection<MangaTag>>(new ObservableCollection<MangaTag>())
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

		this.MaterialFiles = new ObservableCollection<MaterialFileItemViewModel>();

		// MaterialFilesDisplay: MaterialFiles.Count に基づいて表示文字列を生成
		// 初期値を設定し、XAML で Count にバインドして表示内容を切り替えるアプローチに変更
		this.MaterialFilesDisplay = new BindableReactiveProperty<string>(this.getMaterialFilesDisplayText())
			.AddTo(ref this.disposableBag);

		// EndVolumeTextInputCommand: 完結巻入力中テキストを処理するコマンド
		this.EndVolumeTextInputCommand = new ReactiveCommand<string?>()
			.AddTo(ref this.disposableBag);
		this.EndVolumeTextInputCommand.Subscribe(text =>
		{
			this.HandleEndVolumeTextInput(text);
		});

		// タグポップアップコマンド
		this.PrepareTagPopupCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.PrepareTagPopupCommand.Subscribe(_ =>
		{
			this.PrepareTagPopup();
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

		// VolumeStatus: 巻情報編集用ViewModel
		this.VolumeStatus = new EditorSeriesVolumeStatusViewModel()
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 素材ファイル一覧のヘッダー表示文字列を取得します。
	/// </summary>
	/// <returns>0件の場合は "素材ファイル　無し"、1件以上の場合は "素材ファイル　{Count}件"。</returns>
	private string getMaterialFilesDisplayText()
	{
		return this.MaterialFiles.Count == 0
			? "素材ファイル　無し"
			: $"素材ファイル　{this.MaterialFiles.Count}件";
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

			// 編集対象作品のタグをコピー
			this.EditingTagsCollection.Value = new ObservableCollection<MangaTag>(editingSeries.Tags);

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

			// タイトル入力欄へのフォーカスを要求
			this.TitleFocusRequest.Value++;
		}
	}

	/// <summary>
	/// タイトルのバリデーション処理。
	/// タイトル重複チェックを行い、結果に応じてエラーメッセージまたは null を返す。
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
		return "同じタイトルの作品が複数見つかりました。";
	}

	/// <summary>
	/// ナビゲーション完了後に呼ばれる初期データ読み込み処理。
	/// workspaceStore.EditTarget から編集対象を取得し、StartEdit を実行します。
	/// </summary>
	public ValueTask InitializeDataAsync()
	{
		var editTarget = this.workspaceStore.EditTarget;

		if (editTarget is not null)
		{
			this.StartEdit(editTarget);
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
	/// 既存作品ダイアログを非同期で表示し、ユーザーの選択に応じた処理を実行します。
	/// </summary>
	/// <param name="duplicateSeries">既存の作品。</param>
	private async ValueTask showExistingSeriesDialogAsync(MangaSeries duplicateSeries)
	{
		// ダイアログ用に SeriesCardViewModel を生成
		var cardViewModel = new SeriesCardViewModel(duplicateSeries);

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
			PrimaryButtonText = "開く",
			CloseButtonText = "戻る",
		};

		// ダイアログを表示して結果を取得
		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);

		// ユーザーが「開く」を選択した場合
		if (result == ContentDialogResult.Primary)
		{
			// DuplicateSeriesFound を null にリセット（ダイアログループ防止）
			this.DuplicateSeriesFound.Value = null;
			// 既存作品を読み込み
			this.StartEdit(duplicateSeries);
		}
		else
		{
			// 「戻る」の場合、作品管理画面へ戻る
			// DuplicateSeriesFound を null にリセット
			this.DuplicateSeriesFound.Value = null;
			// ナビゲーションで MaintenancePage へ戻る
			this.navigationService.Navigate(typeof(MaintenancePage));
		}
	}

	/// <summary>
	/// タグポップアップ用のタグ一覧を準備します。
	/// </summary>
	private void PrepareTagPopup()
	{
		this.SelectableTagsForPopup.Clear();
		var tags = this.mangaSeriesStore.GetTags()
			.OrderByDescending(t => t.DisplayOrder)
			.ThenByDescending(t => t.TagId)
			.ToList();

		// プレースホルダーセルを計算
		var tagCount = tags.Count;
		var columns = this.TagSelectionColumns;
		var placeholderCount = (columns - (tagCount % columns)) % columns;

		// プレースホルダーを先頭に追加
		for (var i = 0; i < placeholderCount; i++)
		{
			var placeholderItem = new SeriesTagSelectionItem(null!, false)
			{
				IsPlaceholder = true
			};
			this.SelectableTagsForPopup.Add(placeholderItem);
		}

		// 実際のタグを追加
		foreach (var tag in tags)
		{
			var isChecked = this.EditingTagsCollection.Value.Any(t => t.TagId == tag.TagId);
			var item = new SeriesTagSelectionItem(tag, isChecked);
			item.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName != nameof(SeriesTagSelectionItem.IsChecked))
					return;
				this.ApplyTagToEditingSeries(tag, item.IsChecked);
			};
			this.SelectableTagsForPopup.Add(item);
		}

		// 行数を計算
		this.TagSelectionRows = (tagCount + placeholderCount + columns - 1) / columns;
	}

	/// <summary>
	/// タグの選択状態を編集中の作品に反映します。
	/// </summary>
	/// <param name="tag">対象タグ。</param>
	/// <param name="isChecked">チェック状態。</param>
	private void ApplyTagToEditingSeries(MangaTag tag, bool isChecked)
	{
		if (this.EditingSeries.Value == null)
			return;

		if (isChecked)
		{
			// タグを追加
			if (!this.EditingTagsCollection.Value.Any(t => t.TagId == tag.TagId))
			{
				this.EditingTagsCollection.Value.Add(tag);
				this.EditingSeries.Value.Tags.Add(tag);
			}
		}
		else
		{
			// タグを削除
			var tagToRemove = this.EditingTagsCollection.Value.FirstOrDefault(t => t.TagId == tag.TagId);
			if (tagToRemove != null)
			{
				this.EditingTagsCollection.Value.Remove(tagToRemove);
				this.EditingSeries.Value.Tags.Remove(tagToRemove);
			}
		}
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

	/// <inheritdoc/>
	public void Dispose()
	{
		this.ClearPastedThumbnail();
		this.disposableBag.Dispose();
	}
}
