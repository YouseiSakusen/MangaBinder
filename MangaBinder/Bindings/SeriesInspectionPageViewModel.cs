using MangaBinder.Bindings.Inspection;
using MangaBinder.Bindings.Prepress;
using MangaBinder.Controls;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using Wpf.Ui;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本前確認画面の ViewModel です。
/// </summary>
public class SeriesInspectionPageViewModel : IDisposable, IDataInitializable
{
	/// <summary>作品選択状態ストア。</summary>
	private readonly SeriesWorkspaceStore workspaceStore;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>スコープファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>サムネイル画像ローダー。</summary>
	private readonly ThumbnailImageLoader thumbnailImageLoader;

	/// <summary>ローディングサービス。</summary>
	private readonly LoadingService loadingService;

	private DisposableBag disposableBag;

	/// <summary>内部保持する検査結果リスト。</summary>
	private readonly ObservableList<VolumeInspectionResult> inspectionResults;

	/// <summary>選択中の作品エンティティを取得します（サムネイル・巻数情報表示用）。</summary>
	public BindableReactiveProperty<MangaSeries?> SelectedSeries { get; }

	/// <summary>選択中の作品名を取得します。</summary>
	public BindableReactiveProperty<string> SeriesTitle { get; }

	/// <summary>選択巻数サマリ文字列を取得します。</summary>
	public BindableReactiveProperty<string> VolumeSummaryText { get; }

	/// <summary>アイキャッチカード用の選択巻数テキスト（「9巻」形式）を取得します。</summary>
	public BindableReactiveProperty<string> SelectedVolumeCountText { get; }

	/// <summary>
	/// 作品サムネイルカード用の ViewModel を取得します。
	/// ThumbnailSource と VolumeStatus を管理し、左側パネルのサムネイルカードに使用されます。
	/// </summary>
	public MangaSeriesCardViewModel MangaSeriesCard { get; private set; }

	/// <summary>製本前確認画面が所有する巻情報表示用ViewModel。</summary>
	private readonly SeriesVolumeStatusViewModel volumeStatusViewModel;

	/// <summary>ListView にバインドする検査結果一覧を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<VolumeInspectionResult> InspectionResults { get; }

	// zip 設定のプロパティ

	/// <summary>著者名（編集可能）を取得します。</summary>
	public BindableReactiveProperty<string> ZipAuthor { get; }

	/// <summary>タイトル（編集可能）を取得します。</summary>
	public BindableReactiveProperty<string> ZipTitle { get; }

	/// <summary>出力 zip ファイル名（編集可能）を取得します。</summary>
	public BindableReactiveProperty<string> ZipOutputFileName { get; }

	/// <summary>出力形式の選択インデックスを取得します（0: 作品単位・1: 巻ごと）。</summary>
	public BindableReactiveProperty<int> ZipOutputFormatIndex { get; }

	/// <summary>出力形式の選択肢を取得します。</summary>
	public IReadOnlyList<string> ZipOutputFormatItems { get; } =
		["作品単位でzip化", "巻ごとにzip化"];

	/// <summary>既存の製本済み zip を削除するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> DeleteExistingZip { get; }

	/// <summary>既存の製本済み zip が存在するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> ExistingZipExists { get; }

	/// <summary>iCloud からも削除するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> DeleteFromICloud { get; }

	// サブフォルダ展開方式選択肢

	/// <summary>サブフォルダ展開方式の選択肢を取得します。</summary>
	public IReadOnlyList<string> SubFolderModeItems { get; } =
		["サブフォルダを無視", "サブフォルダを含める", "サブフォルダを連番化"];

	// コマンド

	/// <summary>戻るコマンドを取得します。</summary>
	public ReactiveCommand GoBackCommand { get; }

	/// <summary>製本開始コマンドを取得します（現時点はダミー）。</summary>
	public ReactiveCommand StartBindingCommand { get; }

	/// <summary>横長画像詳細コマンドを取得します（未実装）。</summary>
	public ReactiveCommand<VolumeInspectionResult> LandscapeDetailCommand { get; }

	/// <summary>見開き分割画面を開くコマンドを取得します。</summary>
	public ReactiveCommand<VolumeInspectionResult> OpenVolumeThumbnailsCommand { get; }

	/// <summary>
	/// <see cref="SeriesInspectionPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="workspaceStore">作品選択状態ストア。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	/// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
	/// <param name="loadingService">ローディングサービス。</param>
	public SeriesInspectionPageViewModel(
		SeriesWorkspaceStore workspaceStore,
		INavigationService navigationService,
		IServiceScopeFactory serviceScopeFactory,
		ThumbnailImageLoader thumbnailImageLoader,
		LoadingService loadingService)
	{
		this.workspaceStore = workspaceStore;
		this.navigationService = navigationService;
		this.serviceScopeFactory = serviceScopeFactory;
		this.thumbnailImageLoader = thumbnailImageLoader;
		this.loadingService = loadingService;

		this.inspectionResults = new ObservableList<VolumeInspectionResult>();

		// 製本前確認画面が所有する巻情報表示用ViewModel を生成
		this.volumeStatusViewModel = new SeriesVolumeStatusViewModel()
			.AddTo(ref this.disposableBag);

		this.SelectedSeries = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);
		this.SeriesTitle = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.VolumeSummaryText = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.SelectedVolumeCountText = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.ZipAuthor = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.ZipTitle = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.ZipOutputFileName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.ZipOutputFormatIndex = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.DeleteExistingZip = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.ExistingZipExists = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.DeleteFromICloud = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.DeleteExistingZip
			.Where(v => !v)
			.Subscribe(_ => this.DeleteFromICloud.Value = false)
			.AddTo(ref this.disposableBag);

		this.InspectionResults = this.inspectionResults
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// MangaSeriesCard: 作品サムネイルカード用ViewModel
		this.MangaSeriesCard = new MangaSeriesCardViewModel()
			.AddTo(ref this.disposableBag);

		// SelectedSeries 変更時に同期
		this.SelectedSeries.Subscribe(series =>
		{
			if (series is not null)
			{
				// 製本前確認画面が所有する SeriesVolumeStatusViewModel に series を設定
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
			}
			else
			{
				// series が null の場合はクリア
				this.volumeStatusViewModel.Series.Value = null;
				this.MangaSeriesCard.ThumbnailSource.Value = null;
				this.MangaSeriesCard.VolumeStatus.Value = null;
				this.MangaSeriesCard.Series.Value = null;
			}
		}).AddTo(ref this.disposableBag);

		this.GoBackCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.GoBackCommand.Subscribe(_ => this.navigationService.GoBack())
			.AddTo(ref this.disposableBag);

		this.StartBindingCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.StartBindingCommand.Subscribe(_ =>
		{
			// TODO: 製本処理実装後に置き換える
		}).AddTo(ref this.disposableBag);

		this.LandscapeDetailCommand = new ReactiveCommand<VolumeInspectionResult>()
			.AddTo(ref this.disposableBag);
		this.LandscapeDetailCommand.Subscribe(_ =>
		{
			// TODO: 見開き分割画面遷移先を実装する
		}).AddTo(ref this.disposableBag);

		this.OpenVolumeThumbnailsCommand = new ReactiveCommand<VolumeInspectionResult>()
			.AddTo(ref this.disposableBag);
		this.OpenVolumeThumbnailsCommand.Subscribe(result =>
		{
			this.workspaceStore.SetCurrentPrepressVolume(result);
			this.navigationService.NavigateWithHierarchy(typeof(VolumeThumbnailsPage));
		}).AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public ValueTask InitializeDataAsync()
	{
		this.inspectionResults.Clear();

		var series = this.workspaceStore.BindingTarget;
		this.SelectedSeries.Value = series;
		this.SeriesTitle.Value = series?.Title ?? string.Empty;
		this.ZipTitle.Value = series?.Title ?? string.Empty;
		this.ZipAuthor.Value = series?.Author ?? string.Empty;

		this.ExistingZipExists.Value = true;
		this.VolumeSummaryText.Value = string.Empty;

		// アイキャッチカード用の選択巻数テキストを更新
		var selectedVolumeCount = this.workspaceStore.SelectedMaterialVolumes.Count;
		this.SelectedVolumeCountText.Value = $"{selectedVolumeCount}巻";

		if (series is null || this.workspaceStore.SelectedMaterialVolumes.Count == 0)
		{
			this.ZipOutputFileName.Value = string.Empty;
			return ValueTask.CompletedTask;
		}

		this.ZipOutputFileName.Value = this.buildZipOutputFileName(series);

		_ = this.loadInspectionResultsAsync(series);

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// 選択巻情報と作品情報から製本後 zip ファイル名の初期値を生成します。
	/// </summary>
	/// <param name="series">対象の作品エンティティ。</param>
	/// <returns>zip ファイル名文字列。</returns>
	private string buildZipOutputFileName(MangaSeries series)
	{
		var volumes = this.workspaceStore.SelectedMaterialVolumes;
		var startVolume = volumes.Min(v => v.VolumeNumber);
		var endVolume = volumes.Max(v => v.VolumeNumber);

		using var scope = this.serviceScopeFactory.CreateScope();
		var formatter = scope.ServiceProvider.GetRequiredService<BindingZipFileNameFormatter>();

		return formatter.Format(
			series.Author,
			series.Title,
			startVolume,
			endVolume,
			series.EndVolume,
			series.SeriesCompleted,
			series.IsOwnedCompleted);
	}

	/// <summary>
	/// バックグラウンドで製本前確認データを読み込み、完了後に UI へ反映します。
	/// </summary>
	/// <param name="series">対象の作品エンティティ。</param>
	private async Task loadInspectionResultsAsync(MangaSeries series)
	{
		using (this.loadingService.Begin("展開・変換・検査中..."))
		{
			try
			{
				using var scope = this.serviceScopeFactory.CreateScope();
				var builder = scope.ServiceProvider.GetRequiredService<WorkFolderBuilderOld>();

				var results = await Task.Run(
					() => builder.BuildAsync(
						series,
						this.workspaceStore.SelectedMaterialVolumes,
						this.workspaceStore.RecreateWorkFolder.Value).AsTask());

				foreach (var result in results)
					this.inspectionResults.Add(result);
			}
			catch
			{
				// 例外は Fail Fast で処理（DispatcherUnhandledException へ到達）
				throw;
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
