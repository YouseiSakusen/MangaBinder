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
/// コマンドと Manager 呼び出し、Store の公開、画面固有の UI 処理（検索結果0件 InfoBar の表示時間管理）を担当します。
/// 検索・表示対象の更新は MangaSeriesManager、表示用カード VM の生成・破棄は MaintenanceSeriesViewModelStore が担当します。
/// </summary>
public class MaintenancePageViewModel : IDisposable, IDataInitializable, INavigationLeavingAware
{
	/// <summary>検索結果0件 InfoBar を表示する時間。</summary>
	private static readonly TimeSpan noResultInfoBarDuration = TimeSpan.FromSeconds(5);

	private DisposableBag disposableBag;

	/// <summary>InfoBar の自動非表示待機。再検索・画面離脱時に破棄して無効化します。</summary>
	private readonly SerialDisposable infoBarHideTimer = new();

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>編集対象を保持するストア。</summary>
	private readonly EditStore editStore;

	/// <summary>DI スコープを作成するファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>MangaSeries の正本リストと作品管理画面の検索状態を管理するストア。</summary>
	public MangaSeriesStore MangaSeriesStore { get; }

	/// <summary>作品管理画面の表示用カード VM を管理するストア。</summary>
	public MaintenanceSeriesViewModelStore MaintenanceSeriesViewModelStore { get; }

	/// <summary>検索結果0件 InfoBar を開いているかを取得します。</summary>
	public BindableReactiveProperty<bool> IsNoResultInfoBarOpen { get; }

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
	/// <param name="editStore">編集対象を保持するストア。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="maintenanceSeriesViewModelStore">作品管理画面の表示用カード VM を管理するストア。</param>
	/// <param name="serviceScopeFactory">DI スコープを作成するファクトリー。</param>
	public MaintenancePageViewModel(INavigationService navigationService, EditStore editStore, MangaSeriesStore mangaSeriesStore, MaintenanceSeriesViewModelStore maintenanceSeriesViewModelStore, IServiceScopeFactory serviceScopeFactory)
	{
		this.navigationService = navigationService;
		this.editStore = editStore;
		this.MangaSeriesStore = mangaSeriesStore;
		this.MaintenanceSeriesViewModelStore = maintenanceSeriesViewModelStore;
		this.serviceScopeFactory = serviceScopeFactory;

		this.infoBarHideTimer.AddTo(ref this.disposableBag);

		this.IsNoResultInfoBarOpen = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.SearchCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.ShowWorkSeriesCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.ShowEditorCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.EditSeriesCommand = new ReactiveCommand<MangaSeries>()
			.AddTo(ref this.disposableBag);

		// SearchCommand: Manager の検索を呼び出す
		this.SearchCommand
			.Subscribe(_ => this.callManager(manager => manager.SearchMaintenanceSeries()))
			.AddTo(ref this.disposableBag);

		// ShowWorkSeriesCommand: Manager の登録待ち表示を呼び出す
		this.ShowWorkSeriesCommand
			.Subscribe(_ => this.callManager(manager => manager.ShowMaintenanceWorkSeries()))
			.AddTo(ref this.disposableBag);

		// ShowEditorCommand の実装
		this.ShowEditorCommand.Subscribe(_ => this.showEditor());

		// EditSeriesCommand の実装
		this.EditSeriesCommand.Subscribe(series => this.editSeries(series));

		// Manager が Store へ反映した検索完了通知を起点に InfoBar を制御（検索のたびに発火する）
		this.MangaSeriesStore.MaintenanceSearchCompleted
			.Subscribe(resultCount => this.onSearchCompleted(resultCount))
			.AddTo(ref this.disposableBag);

		// 登録待ち表示へ戻った場合は InfoBar を閉じる
		this.MangaSeriesStore.IsMaintenanceSearchResultsShown
			.Where(isShown => !isShown)
			.Subscribe(_ => this.closeInfoBar())
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

		// Store の単一の表示用カード一覧を一度だけ接続（以降の切り替えは不要）
		this.SelectableSeriesListViewModel.SetExternalSource(this.MaintenanceSeriesViewModelStore.DisplayCards);
	}

	/// <summary>
	/// 画面表示後の初期化を非同期で実行します。
	/// Manager 経由で登録待ち表示を初期化します。
	/// </summary>
	public async ValueTask InitializeDataAsync()
	{
		this.callManager(manager => manager.ShowMaintenanceWorkSeries());

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// 画面から離れる際に InfoBar の表示・時間制御を終了します。
	/// </summary>
	/// <param name="request">遷移先から受け取った要求。</param>
	public ValueTask OnNavigatingFromAsync(NavigationLeavingRequest request)
	{
		this.closeInfoBar();

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Scope を作成して MangaSeriesManager を Resolve し、指定された処理を呼び出します。
	/// </summary>
	/// <param name="action">Manager に対する処理。</param>
	private void callManager(Action<MangaSeriesManager> action)
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<MangaSeriesManager>();

		action(manager);
	}

	/// <summary>
	/// 検索完了通知を受けて InfoBar を制御します。
	/// 0件の場合は表示して5秒後に閉じる待機を開始し、1件以上の場合は閉じます。
	/// </summary>
	/// <param name="resultCount">検索結果件数。</param>
	private void onSearchCompleted(int resultCount)
	{
		if (resultCount > 0)
		{
			this.closeInfoBar();
			return;
		}

		// 前回の非表示待機を破棄して新しい待機に置き換える
		this.IsNoResultInfoBarOpen.Value = true;
		this.infoBarHideTimer.Disposable = Observable
			.Timer(noResultInfoBarDuration)
			.ObserveOnCurrentSynchronizationContext()
			.Subscribe(_ => this.IsNoResultInfoBarOpen.Value = false);
	}

	/// <summary>
	/// InfoBar を閉じ、非表示待機を破棄します。
	/// </summary>
	private void closeInfoBar()
	{
		this.infoBarHideTimer.Disposable = Disposable.Empty;
		this.IsNoResultInfoBarOpen.Value = false;
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
		this.editStore.EditTarget = series;
		this.editStore.EditTargetViewModel =
			this.MangaSeriesStore.All.FirstOrDefault(vm => ReferenceEquals(vm.Series.Value, series))
			?? this.MangaSeriesStore.WorkSeries.FirstOrDefault(vm => ReferenceEquals(vm.Series.Value, series));

		// NavigationHierarchy を設定
		this.navigationService.NavigateWithHierarchy(typeof(EditorPage));
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
