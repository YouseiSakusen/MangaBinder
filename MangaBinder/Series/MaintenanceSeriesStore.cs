using MangaBinder.Core.Series;
using ObservableCollections;
using R3;
using System.Collections.Specialized;

namespace MangaBinder.Series;

/// <summary>
/// 作品管理画面専用の状態と派生 View を保持する Store です。
/// MangaSeriesStore.WorkSeries の Reactive な変更を監視し、
/// MaintenanceSeriesCardViewModel 一覧へ自動的に変換します。
/// </summary>
public class MaintenanceSeriesStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// MangaSeries の正本リストを管理するストア。
	/// </summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>
	/// CreateView で生成された ISynchronizedView。
	/// ViewChanged イベントを購読して、削除・置換時に MaintenanceSeriesCardViewModel を Dispose する。
	/// </summary>
	private readonly ISynchronizedView<MangaSeriesViewModel, MaintenanceSeriesCardViewModel> workSeriesCardsView;

	/// <summary>
	/// WPF バインド用の登録待ち作品 ViewModel コレクション（同期ビュー）。
	/// MangaSeriesStore.WorkSeries の変更を自動的に反映します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel> WorkSeriesCards { get; }

	/// <summary>
	/// 検索結果用の Source コレクション。
	/// 検索実行時に、All + WorkSeries から一致した MangaSeriesViewModel のみを格納します。
	/// </summary>
	private readonly ObservableList<MangaSeriesViewModel> searchResultSource = new();

	/// <summary>
	/// CreateView で生成された検索結果用 ISynchronizedView。
	/// ViewChanged イベントを購読して、削除・置換時に MaintenanceSeriesCardViewModel を Dispose する。
	/// </summary>
	private readonly ISynchronizedView<MangaSeriesViewModel, MaintenanceSeriesCardViewModel> searchResultCardsView;

	/// <summary>
	/// WPF バインド用の検索結果 ViewModel コレクション（同期ビュー）。
	/// searchResultSource の変更を自動的に反映します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel> SearchResultCards { get; }

	/// <summary>
	/// <see cref="MaintenanceSeriesStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	public MaintenanceSeriesStore(MangaSeriesStore mangaSeriesStore)
	{
		this.mangaSeriesStore = mangaSeriesStore;

		// 登録待ち作品の ViewModel コレクションを生成
		// MangaSeriesViewModel から MaintenanceSeriesCardViewModel へ変換
		this.workSeriesCardsView = this.mangaSeriesStore.WorkSeries
			.CreateView(viewModel => new MaintenanceSeriesCardViewModel(viewModel));

		this.WorkSeriesCards = this.workSeriesCardsView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// ISynchronizedView の ViewChanged イベントを購読
		// 削除・置換・ソートなどの変更で、MaintenanceSeriesCardViewModel のライフサイクルを管理
		this.workSeriesCardsView.ViewChanged += this.onWorkSeriesCardsViewChanged;

		// 検索結果用 ViewModel コレクションを生成
		// MangaSeriesViewModel から MaintenanceSeriesCardViewModel へ変換
		this.searchResultCardsView = this.searchResultSource
			.CreateView(viewModel => new MaintenanceSeriesCardViewModel(viewModel));

		this.SearchResultCards = this.searchResultCardsView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// 検索結果用 ISynchronizedView の ViewChanged イベントを購読
		this.searchResultCardsView.ViewChanged += this.onSearchResultCardsViewChanged;
	}

	/// <summary>
	/// 検索結果用 Source を置き換えます。
	/// MangaSeriesManager から取得した検索結果 MangaSeriesViewModel を、
	/// 検索結果用 Source に格納します。
	/// </summary>
	/// <param name="searchResults">検索結果の MangaSeriesViewModel 一覧。</param>
	public void ReplaceSearchResults(IEnumerable<MangaSeriesViewModel> searchResults)
	{
		// 検索結果用 Source をクリア
		this.searchResultSource.Clear();

		// 引数で受け取った検索結果を追加
		foreach (var viewModel in searchResults)
		{
			this.searchResultSource.Add(viewModel);
		}
	}

	/// <summary>
	/// ViewChanged イベント ハンドラ。
	/// ISynchronizedView での削除・置換・ソート時に MaintenanceSeriesCardViewModel を適切に Dispose する。
	/// </summary>
	private void onWorkSeriesCardsViewChanged(in SynchronizedViewChangedEventArgs<MangaSeriesViewModel, MaintenanceSeriesCardViewModel> e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Remove:
				// 削除された MaintenanceSeriesCardViewModel を Dispose
				if (e.IsSingleItem)
				{
					// 単一要素削除
					e.OldItem.View?.Dispose();
				}
				else
				{
					// 複数要素削除
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				break;

			case NotifyCollectionChangedAction.Replace:
				// 置換前の MaintenanceSeriesCardViewModel を Dispose
				if (e.IsSingleItem)
				{
					// 単一要素置換
					e.OldItem.View?.Dispose();
				}
				else
				{
					// 複数要素置換
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				break;

			case NotifyCollectionChangedAction.Reset:
				// Reset は Sort / Reverse / Clear の場合が考えられる
				// IsClear で判定し、Clear の場合だけ旧 MaintenanceSeriesCardViewModel を Dispose
				if (e.SortOperation.IsClear)
				{
					// Clear で削除された MaintenanceSeriesCardViewModel を Dispose
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				// Sort / Reverse の場合は現在有効な MaintenanceSeriesCardViewModel を Dispose しない
				break;
		}
	}

	/// <summary>
	/// 検索結果用 ViewChanged イベント ハンドラ。
	/// ISynchronizedView での削除・置換・ソート時に MaintenanceSeriesCardViewModel を適切に Dispose する。
	/// WorkSeriesCards の ViewChanged と同じ考え方で実装。
	/// </summary>
	private void onSearchResultCardsViewChanged(in SynchronizedViewChangedEventArgs<MangaSeriesViewModel, MaintenanceSeriesCardViewModel> e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Remove:
				// 削除された MaintenanceSeriesCardViewModel を Dispose
				if (e.IsSingleItem)
				{
					// 単一要素削除
					e.OldItem.View?.Dispose();
				}
				else
				{
					// 複数要素削除
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				break;

			case NotifyCollectionChangedAction.Replace:
				// 置換前の MaintenanceSeriesCardViewModel を Dispose
				if (e.IsSingleItem)
				{
					// 単一要素置換
					e.OldItem.View?.Dispose();
				}
				else
				{
					// 複数要素置換
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				break;

			case NotifyCollectionChangedAction.Reset:
				// Reset は Sort / Reverse / Clear の場合が考えられる
				// IsClear で判定し、Clear の場合だけ旧 MaintenanceSeriesCardViewModel を Dispose
				if (e.SortOperation.IsClear)
				{
					// Clear で削除された MaintenanceSeriesCardViewModel を Dispose
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				// Sort / Reverse の場合は現在有効な MaintenanceSeriesCardViewModel を Dispose しない
				break;
		}
	}

	/// <summary>
	/// このストアが保持するリソースを解放します。
	/// </summary>
	public void Dispose()
	{
		// WorkSeriesCards の ViewChanged イベント購読を解除
		this.workSeriesCardsView.ViewChanged -= this.onWorkSeriesCardsViewChanged;

		// 現在 View に残っている MaintenanceSeriesCardViewModel をすべて Dispose
		foreach (var card in this.workSeriesCardsView)
		{
			card?.Dispose();
		}

		// ISynchronizedView を Dispose（元 Collection からの購読解除に必要）
		this.workSeriesCardsView.Dispose();

		// WPF バインド用 WorkSeriesCards を Dispose
		this.WorkSeriesCards.Dispose();

		// SearchResultCards の ViewChanged イベント購読を解除
		this.searchResultCardsView.ViewChanged -= this.onSearchResultCardsViewChanged;

		// 現在 View に残っている MaintenanceSeriesCardViewModel をすべて Dispose
		foreach (var card in this.searchResultCardsView)
		{
			card?.Dispose();
		}

		// 検索結果用 ISynchronizedView を Dispose
		this.searchResultCardsView.Dispose();

		// WPF バインド用 SearchResultCards を Dispose
		this.SearchResultCards.Dispose();

		// R3 購読を Dispose
		this.disposableBag.Dispose();
	}
}
