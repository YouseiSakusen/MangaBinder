using MangaBinder.Bindings;
using MangaBinder.Controls;
using MangaBinder.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Series;

/// <summary>
/// 作品管理ページの ViewModel です。
/// 検索・登録待ちフィルタ・表示制御を担当します。
/// 表示対象の MangaSeries コレクションを SelectableSeriesListViewModel へ渡し、
/// MaintenanceSeriesCardViewModel の生成・管理責務は SelectableSeriesListViewModel に委譲します。
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

	/// <summary>作品管理画面の派生 View を管理するストア。</summary>
	private readonly MaintenanceSeriesStore maintenanceSeriesStore;

	/// <summary>DI スコープを作成するファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>検索文字列を取得します。</summary>
	public BindableReactiveProperty<string> SearchQuery { get; }

	/// <summary>登録待ち作品件数を取得します。</summary>
	public BindableReactiveProperty<int> WorkSeriesCount { get; }

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

	/// <summary>作品一覧表示用の共通 UserControl の ViewModel。</summary>
	public SelectableSeriesListViewModel SelectableSeriesListViewModel { get; }

	/// <summary>
	/// <see cref="MaintenancePageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="workspaceStore">作品選択状態ストア。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="maintenanceSeriesStore">作品管理画面の派生 View を管理するストア。</param>
	/// <param name="serviceScopeFactory">DI スコープを作成するファクトリー。</param>
	public MaintenancePageViewModel(INavigationService navigationService, SeriesWorkspaceStore workspaceStore, MangaSeriesStore mangaSeriesStore, MaintenanceSeriesStore maintenanceSeriesStore, IServiceScopeFactory serviceScopeFactory)
	{
		this.navigationService = navigationService;
		this.workspaceStore = workspaceStore;
		this.mangaSeriesStore = mangaSeriesStore;
		this.maintenanceSeriesStore = maintenanceSeriesStore;
		this.serviceScopeFactory = serviceScopeFactory;

		this.SearchQuery = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.WorkSeriesCount = new BindableReactiveProperty<int>(this.mangaSeriesStore.WorkSeries.Count)
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

		// Store.WorkSeries の Count 変更を監視して WorkSeriesCount を自動更新
		this.mangaSeriesStore.WorkSeries.ObserveCountChanged()
			.Subscribe(count => this.WorkSeriesCount.Value = count)
			.AddTo(ref this.disposableBag);

		// SelectableSeriesListViewModel を初期化
		this.SelectableSeriesListViewModel = new SelectableSeriesListViewModel()
			.AddTo(ref this.disposableBag);

		// CardSize を Compact に設定（作品管理画面用）
		this.SelectableSeriesListViewModel.CardSize.Value = SeriesCardSize.Compact;

		// ShowNavigateButton を有効化（MaintenancePage では右端の「▶」ボタンを表示）
		this.SelectableSeriesListViewModel.ShowNavigateButton.Value = true;

		// NavigateCommand を EditSeriesCommand に接続
		this.SelectableSeriesListViewModel.NavigateCommand = this.EditSeriesCommand;

		// 初回の登録待ち作品フィルタ適用
		// 依存先（IsSearchResultsEmpty、HasSearchResults、SelectableSeriesListViewModel）の初期化完了後に実行
		this.applyWorkSeriesOnlyFilter();
	}


	/// <summary>
	/// 画面表示後の初期データ読み込みを非同期で実行します。
	/// 登録待ち作品件数を更新し、検索状態をリセットします。
	/// </summary>
	public async ValueTask InitializeDataAsync()
	{
		// 検索文字列をクリア
		this.SearchQuery.Value = string.Empty;

		// Filter を通常状態（登録待ち作品のみ）に戻す
		this.applyWorkSeriesOnlyFilter();

		// 検索結果表示フラグを false に設定
		this.IsSearchResultsShown.Value = false;

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// 検索を実行します。
	/// 検索文字列が空の場合は通常表示へ戻します。
	/// ISynchronizedView の Filter を切り替えることで検索結果をリアルタイム更新します。
	/// </summary>
	private async ValueTask executeSearchAsync()
	{
		var searchQuery = this.SearchQuery.Value?.Trim() ?? string.Empty;

		if (string.IsNullOrEmpty(searchQuery))
		{
			// 検索文字列が空 → 通常表示へ戻す（登録待ち作品のみ Filter）
			this.applyWorkSeriesOnlyFilter();
			this.IsSearchResultsShown.Value = false;
		}
		else
		{
			// 検索条件に一致する作品のみを表示する Filter を設定
			this.applySearchFilter(searchQuery);
			this.IsSearchResultsShown.Value = true;
		}

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// 登録待ち一覧を表示します。
	/// 検索をクリアし、登録待ち作品のみを表示する Filter を設定します。
	/// </summary>
	private async ValueTask executeShowWorkSeriesAsync()
	{
		// SearchQuery をクリア
		this.SearchQuery.Value = string.Empty;

		// Filter を通常状態（登録待ち作品のみ）に戻す
		this.applyWorkSeriesOnlyFilter();

		// 検索結果表示フラグを false に設定
		this.IsSearchResultsShown.Value = false;

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// EditorPage を表示します。新規作品登録フローを開始します。
	/// </summary>
	private async void showEditor()
	{
		// Dialog 表示期間用の Scope を作成
		using var scope = this.serviceScopeFactory.CreateScope();

		// Scope から NewSeriesCoordinator を Resolve
		var coordinator = scope.ServiceProvider.GetRequiredService<NewSeriesCoordinator>();

		// 新規作品登録フローを開始
		await coordinator.StartAsync();
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
	/// 登録待ち作品のみを表示する Filter を適用します。
	/// 新 Reactive 系では、MaintenanceSeriesStore の WorkSeriesCards を直接表示します。
	/// </summary>
	private void applyWorkSeriesOnlyFilter()
	{
		// 新 Reactive 系：MaintenanceSeriesStore の WorkSeriesCards を外部参照として設定
		this.SelectableSeriesListViewModel.SetExternalSource(this.maintenanceSeriesStore.WorkSeriesCards);
	}

	/// <summary>
	/// 検索条件に基づく Filter を適用します。
	/// </summary>
	/// <param name="searchQuery">検索文字列。</param>
	private void applySearchFilter(string searchQuery)
	{
		// Scope を作成して MangaSeriesManager を Resolve
		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<MangaSeriesManager>();

		// Manager の検索メソッドを呼び出し
		// All + WorkSeries から一致した MangaSeriesViewModel のみを取得
		var searchResults = manager.SearchMangaSeries(searchQuery, MangaSeriesSearchTarget.AllAndWorkSeries);

		// 検索結果を MaintenanceSeriesStore へ渡す
		this.maintenanceSeriesStore.ReplaceSearchResults(searchResults);

		// 検索結果用カード Collection を外部参照として設定して表示
		this.SelectableSeriesListViewModel.SetExternalSource(this.maintenanceSeriesStore.SearchResultCards);
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}

