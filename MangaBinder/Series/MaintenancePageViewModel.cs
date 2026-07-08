using MangaBinder.Bindings;
using ObservableCollections;
using R3;
using System.Collections.Specialized;
using Wpf.Ui;

namespace MangaBinder.Series;

/// <summary>
/// 作品管理ページの ViewModel です。
/// </summary>
public class MaintenancePageViewModel : IDisposable, IDataInitializable
{
	private DisposableBag disposableBag;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>作品選択状態ストア。</summary>
	private readonly SeriesWorkspaceStore workspaceStore;

	/// <summary>MangaSeries の正本リストを管理するストア。</summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>作品の検索を担う Manager。</summary>
	private readonly MangaSeriesManager mangaSeriesManager;

	/// <summary>表示用作品一覧の内部バッファ。</summary>
	private readonly ObservableList<SeriesCardViewModel> displaySeriesSource = new();

	/// <summary>検索文字列を取得します。</summary>
	public BindableReactiveProperty<string> SearchQuery { get; }

	/// <summary>登録待ち作品件数を取得します。</summary>
	public BindableReactiveProperty<int> WorkSeriesCount { get; }

	/// <summary>登録待ち作品一覧を取得します。</summary>
	public IReadOnlyList<MangaSeries> WorkSeries
		=> this.mangaSeriesStore.GetWorkSeries();

	/// <summary>表示する作品一覧を取得します。通常表示時は WorkSeries、検索表示時は検索結果。</summary>
	public NotifyCollectionChangedSynchronizedViewList<SeriesCardViewModel> DisplaySeries { get; }

	/// <summary>検索結果が空であるかを取得します。</summary>
	public BindableReactiveProperty<bool> IsSearchResultsEmpty { get; }

	/// <summary>検索結果が存在するかを取得します（IsSearchResultsEmpty の反対）。</summary>
	public BindableReactiveProperty<bool> HasSearchResults { get; }

	/// <summary>検索結果を表示中であるかを取得します。</summary>
	public BindableReactiveProperty<bool> IsSearchResultsShown { get; }

	/// <summary>検索実行コマンドです。</summary>
	public ReactiveCommand<Unit> SearchCommand { get; private set; }

	/// <summary>登録待ち表示コマンドです。</summary>
	public ReactiveCommand<Unit> ShowWorkSeriesCommand { get; private set; }

	/// <summary>EditorPage を表示するコマンドです。</summary>
	public ReactiveCommand<Unit> ShowEditorCommand { get; private set; }

	/// <summary>既存作品を編集モードで EditorPage を表示するコマンドです。</summary>
	public ReactiveCommand<MangaSeries> EditSeriesCommand { get; private set; }

	/// <summary>
	/// <see cref="MaintenancePageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="workspaceStore">作品選択状態ストア。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="mangaSeriesManager">作品の検索を担う Manager。</param>
	public MaintenancePageViewModel(INavigationService navigationService, SeriesWorkspaceStore workspaceStore, MangaSeriesStore mangaSeriesStore, MangaSeriesManager mangaSeriesManager)
	{
		this.navigationService = navigationService;
		this.workspaceStore = workspaceStore;
		this.mangaSeriesStore = mangaSeriesStore;
		this.mangaSeriesManager = mangaSeriesManager;

		this.SearchQuery = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.WorkSeriesCount = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		// DisplaySeries: ObservableCollections ベースの表示用コレクション
		this.DisplaySeries = this.displaySeriesSource.ToNotifyCollectionChanged()
			.AddTo(ref this.disposableBag);

		this.IsSearchResultsEmpty = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		this.HasSearchResults = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.IsSearchResultsShown = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.SearchCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.ShowWorkSeriesCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.ShowEditorCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.EditSeriesCommand = new ReactiveCommand<MangaSeries>()
			.AddTo(ref this.disposableBag);

		// SearchCommand の実装
		this.SearchCommand.Subscribe(async _ => await this.executeSearchAsync());

		// ShowWorkSeriesCommand の実装
		this.ShowWorkSeriesCommand.Subscribe(async _ => await this.executeShowWorkSeriesAsync());

		// ShowEditorCommand の実装
		this.ShowEditorCommand.Subscribe(_ => this.showEditor());

		// EditSeriesCommand の実装
		this.EditSeriesCommand.Subscribe(series => this.editSeries(series));
	}

	/// <summary>
	/// 画面表示後の初期データ読み込みを非同期で実行します。
	/// 登録待ち作品件数を更新します。
	/// </summary>
	public async ValueTask InitializeDataAsync()
	{
		// Store から登録待ち作品一覧を取得（通常表示用）
		var workSeriesList = this.mangaSeriesStore.GetWorkSeries();

		// 登録待ち件数を更新（常に Store が保持している登録待ち作品数を表示）
		this.WorkSeriesCount.Value = workSeriesList.Count;

		// DisplaySeries の再構築：既存インスタンスと新規インスタンスを照合
		// 既存インスタンスが存在する場合は巻情報を更新、新規インスタンスの場合は追加
		this.updateDisplaySeriesWithRefresh(workSeriesList);

		// 検索結果表示フラグを false に初期化
		this.IsSearchResultsShown.Value = false;

		// エンプティステート状態を更新
		this.UpdateEmptyState();

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// エンプティステートを更新します。
	/// DisplaySeries の行数で判定します。
	/// </summary>
	private void UpdateEmptyState()
	{
		var isEmpty = this.DisplaySeries.Count == 0;
		this.IsSearchResultsEmpty.Value = isEmpty;
		this.HasSearchResults.Value = !isEmpty;
	}

	/// <summary>
	/// 検索を実行します。
	/// 検索文字列が空の場合は通常表示へ戻します。
	/// </summary>
	private async ValueTask executeSearchAsync()
	{
		var searchQuery = this.SearchQuery.Value?.Trim() ?? string.Empty;

		if (string.IsNullOrEmpty(searchQuery))
		{
			// 検索文字列が空 → 通常表示へ戻す（巻情報も再同期）
			this.updateDisplaySeriesWithRefresh(this.WorkSeries);
			this.IsSearchResultsShown.Value = false;
		}
		else
		{
			// 検索実行
			var results = this.mangaSeriesManager.Search(searchQuery);

			// DisplaySeries に検索結果を設定（巻情報も再同期）
			this.updateDisplaySeriesWithRefresh(results);

			// 検索結果表示フラグを設定
			this.IsSearchResultsShown.Value = true;

			// デバッグ出力
			System.Diagnostics.Debug.WriteLine($"[Search] Query: {searchQuery}, Results: {results.Count}");
		}

		// EmptyState を更新
		this.UpdateEmptyState();

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// 登録待ち一覧を表示します。
	/// 検索をクリアし、WorkSeries を表示します。
	/// </summary>
	private async ValueTask executeShowWorkSeriesAsync()
	{
		// SearchQuery をクリア
		this.SearchQuery.Value = string.Empty;

		// DisplaySeries を通常表示へ戻す（巻情報も再同期）
		this.updateDisplaySeriesWithRefresh(this.WorkSeries);

		// 検索結果表示フラグを false に設定
		this.IsSearchResultsShown.Value = false;

		// EmptyState を更新
		this.UpdateEmptyState();

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// EditorPage を表示します。新規作品として新しい MangaSeries を初期化します。
	/// </summary>
	private void showEditor()
	{
		// 編集対象を新規作品として初期化
		this.workspaceStore.EditTarget = new MangaSeries();

		// NavigationHierarchy を設定
		this.navigationService.NavigateWithHierarchy(typeof(EditorPage));
	}

	/// <summary>
	/// 指定された作品を編集モードで EditorPage を表示します。
	/// </summary>
	/// <param name="series">編集対象の作品。</param>
	private void editSeries(MangaSeries series)
	{
		// 編集対象を指定作品に設定
		this.workspaceStore.EditTarget = series;

		// NavigationHierarchy を設定
		this.navigationService.NavigateWithHierarchy(typeof(EditorPage));
	}

	/// <summary>
	/// DisplaySeries を指定された MangaSeries リストに基づいて更新します。
	/// 既存の SeriesCardViewModel が存在する場合は巻情報を再同期し、
	/// 新規の場合は新しい SeriesCardViewModel を作成します。
	/// </summary>
	/// <param name="workSeriesList">更新対象の MangaSeries リスト。</param>
	private void updateDisplaySeriesWithRefresh(IReadOnlyList<MangaSeries> workSeriesList)
	{
		// 新しい SeriesCardViewModel リストを構築
		var newDisplaySeries = new List<SeriesCardViewModel>();

		foreach (var series in workSeriesList)
		{
			// 既存の displaySeriesSource 内で同じ WorkId を持つ SeriesCardViewModel を検索
			var existingCard = this.displaySeriesSource.FirstOrDefault(x => x.Series.WorkId == series.WorkId);

			if (existingCard != null)
			{
				// 既存インスタンスが存在する場合は巻情報を再同期
				existingCard.RefreshVolumeStatus();
				newDisplaySeries.Add(existingCard);
			}
			else
			{
				// 新規インスタンスの場合は新しい SeriesCardViewModel を作成
				newDisplaySeries.Add(new SeriesCardViewModel(series));
			}
		}

		// displaySeriesSource を一括更新
		this.displaySeriesSource.Clear();
		this.displaySeriesSource.AddRange(newDisplaySeries);
	}

	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
