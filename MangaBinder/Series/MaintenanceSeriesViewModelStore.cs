using ObservableCollections;
using R3;
using System.Collections.Specialized;

namespace MangaBinder.Series;

/// <summary>
/// 作品管理画面専用の状態と派生 View を保持する Store です。
/// MangaSeriesStore.MaintenanceDisplaySeries の Reactive な変更を監視し、
/// MaintenanceSeriesCardViewModel 一覧へ自動的に変換します。
/// </summary>
public class MaintenanceSeriesViewModelStore : IDisposable
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
	private readonly ISynchronizedView<MangaSeriesNotifier, MaintenanceSeriesCardViewModel> displayCardsView;

	/// <summary>
	/// WPF バインド用の作品管理画面表示カード一覧（同期ビュー）。
	/// MangaSeriesStore.MaintenanceDisplaySeries の変更を自動的に反映します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel> DisplayCards { get; }

	/// <summary>現在の表示件数（検索結果表示中は検索結果件数）を取得します。</summary>
	public BindableReactiveProperty<int> DisplayCount { get; }

	/// <summary>表示一覧が空であるかを取得します。</summary>
	public BindableReactiveProperty<bool> IsEmpty { get; }

	/// <summary>表示一覧に項目があるかを取得します。</summary>
	public BindableReactiveProperty<bool> HasItems { get; }

	/// <summary>検索結果件数を表示するかを取得します。MangaSeriesStore の状態を参照します。</summary>
	public BindableReactiveProperty<bool> IsSearchResultsShown => this.mangaSeriesStore.IsMaintenanceSearchResultsShown;

	/// <summary>
	/// <see cref="MaintenanceSeriesViewModelStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	public MaintenanceSeriesViewModelStore(MangaSeriesStore mangaSeriesStore)
	{
		this.mangaSeriesStore = mangaSeriesStore;

		// MangaSeriesNotifier から MaintenanceSeriesCardViewModel へ変換
		this.displayCardsView = this.mangaSeriesStore.MaintenanceDisplaySeries
			.CreateView(notifier => new MaintenanceSeriesCardViewModel(notifier));

		this.DisplayCards = this.displayCardsView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// 削除・置換・ソートなどの変更で、MaintenanceSeriesCardViewModel のライフサイクルを管理
		this.displayCardsView.ViewChanged += this.onDisplayCardsViewChanged;

		// 件数・空状態を表示対象リストから導出
		var initialCount = this.mangaSeriesStore.MaintenanceDisplaySeries.Count;

		this.DisplayCount = new BindableReactiveProperty<int>(initialCount)
			.AddTo(ref this.disposableBag);

		this.IsEmpty = new BindableReactiveProperty<bool>(initialCount == 0)
			.AddTo(ref this.disposableBag);

		this.HasItems = new BindableReactiveProperty<bool>(initialCount > 0)
			.AddTo(ref this.disposableBag);

		this.mangaSeriesStore.MaintenanceDisplaySeries.ObserveCountChanged()
			.Subscribe(count =>
			{
				this.DisplayCount.Value = count;
				this.IsEmpty.Value = count == 0;
				this.HasItems.Value = count > 0;
			})
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// ViewChanged イベント ハンドラ。
	/// ISynchronizedView での削除・置換・ソート時に MaintenanceSeriesCardViewModel を適切に Dispose する。
	/// 共有 Notifier とその Series は Dispose しない。
	/// </summary>
	private void onDisplayCardsViewChanged(in SynchronizedViewChangedEventArgs<MangaSeriesNotifier, MaintenanceSeriesCardViewModel> e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Remove:
			case NotifyCollectionChangedAction.Replace:
				// 削除・置換前の MaintenanceSeriesCardViewModel を Dispose
				if (e.IsSingleItem)
				{
					e.OldItem.View?.Dispose();
				}
				else
				{
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				break;

			case NotifyCollectionChangedAction.Reset:
				// Clear の場合だけ旧 MaintenanceSeriesCardViewModel を Dispose
				// Sort / Reverse の場合は現在有効な MaintenanceSeriesCardViewModel を Dispose しない
				if (e.SortOperation.IsClear)
				{
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				break;
		}
	}

	/// <summary>
	/// このストアが保持するリソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.displayCardsView.ViewChanged -= this.onDisplayCardsViewChanged;

		// 現在 View に残っている MaintenanceSeriesCardViewModel をすべて Dispose
		foreach (var card in this.displayCardsView)
		{
			card?.Dispose();
		}

		// ISynchronizedView を Dispose（元 Collection からの購読解除に必要）
		this.displayCardsView.Dispose();

		// WPF バインド用 DisplayCards を Dispose
		this.DisplayCards.Dispose();

		// R3 購読を Dispose
		this.disposableBag.Dispose();
	}
}
