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

	/// <summary>DI スコープを作成するファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>検索文字列を取得します。</summary>
	public BindableReactiveProperty<string> SearchQuery { get; }

	/// <summary>登録待ち作品件数を取得します。</summary>
	public BindableReactiveProperty<int> WorkSeriesCount { get; }

	/// <summary>登録待ち作品一覧を取得します。</summary>
	public IReadOnlyList<MangaSeries> WorkSeries
		=> this.mangaSeriesStore.GetWorkSeries();

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

	/// <summary>作品一覧表示用の共通 UserControl の ViewModel。</summary>
	public SelectableSeriesListViewModel SelectableSeriesListViewModel { get; }

	/// <summary>
	/// <see cref="MaintenancePageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="workspaceStore">作品選択状態ストア。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="serviceScopeFactory">DI スコープを作成するファクトリー。</param>
	public MaintenancePageViewModel(INavigationService navigationService, SeriesWorkspaceStore workspaceStore, MangaSeriesStore mangaSeriesStore, IServiceScopeFactory serviceScopeFactory)
	{
		this.navigationService = navigationService;
		this.workspaceStore = workspaceStore;
		this.mangaSeriesStore = mangaSeriesStore;
		this.serviceScopeFactory = serviceScopeFactory;

		this.SearchQuery = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.WorkSeriesCount = new BindableReactiveProperty<int>(0)
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

		// Store.WorkSeries の Count 変更を監視して WorkSeriesCount を自動更新
		this.mangaSeriesStore.WorkSeries.ObserveCountChanged()
			.Subscribe(count => this.WorkSeriesCount.Value = count)
			.AddTo(ref this.disposableBag);

		// SelectableSeriesListViewModel を初期化
		this.SelectableSeriesListViewModel = new SelectableSeriesListViewModel()
			.AddTo(ref this.disposableBag);

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

		// 登録待ち件数を更新（常に Store が保持している登録待ち作品数を表示）
		var workSeriesList = this.mangaSeriesStore.GetWorkSeries();
		this.WorkSeriesCount.Value = workSeriesList.Count;

		// エンプティステート状態を更新
		this.UpdateEmptyState();

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// エンプティステートを更新します。
	/// SelectableSeriesListViewModel.Items の行数で判定します。
	/// </summary>
	private void UpdateEmptyState()
	{
		var isEmpty = this.SelectableSeriesListViewModel.Items.Count == 0;
		this.IsSearchResultsEmpty.Value = isEmpty;
		this.HasSearchResults.Value = !isEmpty;
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
	/// </summary>
	private void applyWorkSeriesOnlyFilter()
	{
		var series = this.mangaSeriesStore.Merged
			.Where(series => series.IsWork)
			.ToList();

		this.SelectableSeriesListViewModel.SetSource(series);
		this.UpdateEmptyState();
	}

	/// <summary>
	/// 検索条件に基づく Filter を適用します。
	/// </summary>
	/// <param name="searchQuery">検索文字列。</param>
	private void applySearchFilter(string searchQuery)
	{
		var matcher = new MangaSeriesSearchMatcher(searchQuery);

		var series = this.mangaSeriesStore.Merged
			.Where(series => matcher.IsMatch(series))
			.ToList();

		this.SelectableSeriesListViewModel.SetSource(series);
		this.UpdateEmptyState();
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}

