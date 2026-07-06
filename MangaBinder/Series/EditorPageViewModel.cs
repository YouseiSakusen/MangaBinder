using MangaBinder.Bindings;
using MangaBinder.Controls;
using R3;
using Reactive.Bindings.R3;
using System.Collections.ObjectModel;
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
	/// EditorPageViewModel の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="seriesManager">作品管理マネージャー。</param>
	/// <param name="workspaceStore">作業領域ストア。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	public EditorPageViewModel(MangaSeriesManager seriesManager, SeriesWorkspaceStore workspaceStore, IContentDialogService contentDialogService, INavigationService navigationService)
	{
		this.seriesManager = seriesManager ?? throw new ArgumentNullException(nameof(seriesManager));
		this.workspaceStore = workspaceStore ?? throw new ArgumentNullException(nameof(workspaceStore));
		this.contentDialogService = contentDialogService ?? throw new ArgumentNullException(nameof(contentDialogService));
		this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

		this.EditingSeries = new BindableReactiveProperty<MangaSeries?>(null)
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

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
