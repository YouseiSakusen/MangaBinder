using MangaBinder.Bindings;
using MangaBinder.Helpers;
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

	/// <summary>MangaSeriesStore.Merged から CreateView で生成した SynchronizedView。</summary>
	private readonly ISynchronizedView<MangaSeries, MaintenanceSeriesCardViewModel> displayView;

	/// <summary>検索文字列を取得します。</summary>
	public BindableReactiveProperty<string> SearchQuery { get; }

	/// <summary>登録待ち作品件数を取得します。</summary>
	public BindableReactiveProperty<int> WorkSeriesCount { get; }

	/// <summary>登録待ち作品一覧を取得します。</summary>
	public IReadOnlyList<MangaSeries> WorkSeries
		=> this.mangaSeriesStore.GetWorkSeries();

	/// <summary>表示する作品一覧を取得します。MangaSeriesStore.Merged を正本とした CreateView + Filter 構造。</summary>
	public NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel> DisplaySeries { get; }

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

		// MangaSeriesStore.Merged から CreateView で MaintenanceSeriesCardViewModel へ変換
		// 初期 Filter は登録待ち作品のみを表示
		this.displayView = this.mangaSeriesStore.Merged
			.CreateView(series => new MaintenanceSeriesCardViewModel(series))
			.AddTo(ref this.disposableBag);

		// 通常時の Filter を設定：登録待ち作品のみ
		this.applyWorkSeriesOnlyFilter();

		// Filter 適用後、WPF バインド用に ToNotifyCollectionChanged で公開
		this.DisplaySeries = this.displayView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// CollectionChanged イベントで削除・置換時のカード Dispose を管理
		((INotifyCollectionChanged)this.DisplaySeries).CollectionChanged += this.onDisplaySeriesCollectionChanged;

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

		// 現在表示されているカードへ Series.ForceNotify() を呼び出す
		// これにより、他画面で同じ MangaSeries 正本インスタンスの内容が更新された場合でも、
		// 現在表示されている登録待ちカードが最新内容を再評価できる
		foreach (var card in this.DisplaySeries)
		{
			card.Series.ForceNotify();
		}

		// 登録待ち件数を更新（常に Store が保持している登録待ち作品数を表示）
		var workSeriesList = this.mangaSeriesStore.GetWorkSeries();
		this.WorkSeriesCount.Value = workSeriesList.Count;

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
	/// 登録待ち作品のみを表示する Filter を適用します。
	/// </summary>
	private void applyWorkSeriesOnlyFilter()
	{
		var filter = new SynchronizedViewFilter(this.filterWorkSeriesOnly);
		this.displayView.AttachFilter(filter);
	}

	/// <summary>
	/// 検索条件に基づく Filter を適用します。
	/// </summary>
	/// <param name="searchQuery">検索文字列。</param>
	private void applySearchFilter(string searchQuery)
	{
		// 検索条件を一度だけ生成
		var matcher = new MangaSeriesSearchMatcher(searchQuery);

		// matcher を閉じた変数でキャプチャし、SynchronizedViewFilter 内で使用
		Func<MaintenanceSeriesCardViewModel, bool> predicate = card => matcher.IsMatch(card.Series.Value);
		var filter = new SynchronizedViewFilter(predicate);
		this.displayView.AttachFilter(filter);
	}

	/// <summary>
	/// ISynchronizedViewFilter<> を実装するヘルパークラス。
	/// </summary>
	private class SynchronizedViewFilter : ISynchronizedViewFilter<MangaSeries, MaintenanceSeriesCardViewModel>
	{
		private readonly Func<MaintenanceSeriesCardViewModel, bool> predicate;

		/// <summary>
		/// <see cref="SynchronizedViewFilter"/> の新しいインスタンスを初期化します。
		/// </summary>
		/// <param name="predicate">フィルター判定関数。</param>
		public SynchronizedViewFilter(Func<MaintenanceSeriesCardViewModel, bool> predicate)
		{
			this.predicate = predicate;
		}

		/// <summary>
		/// アイテムがフィルター条件に一致するかを判定します。
		/// </summary>
		/// <param name="key">ソース MangaSeries。</param>
		/// <param name="value">フィルター対象の MaintenanceSeriesCardViewModel。</param>
		/// <returns>true の場合は表示、false の場合は非表示。</returns>
		public bool IsMatch(MangaSeries key, MaintenanceSeriesCardViewModel value)
		{
			return this.predicate(value);
		}
	}

	/// <summary>
	/// 登録待ち作品のみを表示する Filter 関数。
	/// MangaSeries.IsWork プロパティで判定します。
	/// </summary>
	/// <param name="card">フィルター対象の MaintenanceSeriesCardViewModel。</param>
	/// <returns>true の場合は表示、false の場合は非表示。</returns>
	private bool filterWorkSeriesOnly(MaintenanceSeriesCardViewModel card)
	{
		return card.Series.Value.IsWork;
	}

	/// <summary>
	/// DisplaySeries コレクションの変化を処理します。
	/// 削除・置換されたカードの Dispose を行い、EmptyState を更新します。
	/// </summary>
	private void onDisplaySeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Remove:
				// 削除されたカードを処理
				if (e.OldItems != null)
				{
					foreach (var oldCard in e.OldItems.Cast<MaintenanceSeriesCardViewModel>())
					{
						oldCard.Dispose();
					}
				}
				break;

			case NotifyCollectionChangedAction.Replace:
				// 置換された古いカードを処理
				if (e.OldItems != null)
				{
					foreach (var oldCard in e.OldItems.Cast<MaintenanceSeriesCardViewModel>())
					{
						oldCard.Dispose();
					}
				}
				break;

			case NotifyCollectionChangedAction.Reset:
				// リセット時は、既存の全カードを Dispose（ただし、新しい要素は Filter で残される）
				break;
		}

		// EmptyState を更新
		this.UpdateEmptyState();
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		// CollectionChanged 購読を明示的に解除
		((INotifyCollectionChanged)this.DisplaySeries).CollectionChanged -= this.onDisplaySeriesCollectionChanged;

		// 残っているすべての MaintenanceSeriesCardViewModel をクリーンアップ
		foreach (var card in this.DisplaySeries)
		{
			card.Dispose();
		}

		this.disposableBag.Dispose();
	}
}

