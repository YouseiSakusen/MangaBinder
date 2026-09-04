using MangaBinder.Series;
using ObservableCollections;
using R3;
using System.Collections.Specialized;

namespace MangaBinder.Bindings;

/// <summary>
/// Home 画面専用の表示用派生 View を保持する Store です。
/// MangaSeriesStore.All の Reactive な変更を監視し、
/// SeriesCardViewModel 一覧へ自動的に変換します。
/// </summary>
public class HomeSeriesStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 作品ストア。
	/// </summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>
	/// 製本開始キュー ストア。SeriesCardViewModel の IsSelected 初期値決定に使用。
	/// </summary>
	private readonly BindingQueueStore bindingQueueStore;

	/// <summary>
	/// タグ変更追跡ストア。SeriesCardViewModel の TagSelector 用。
	/// </summary>
	private readonly SeriesTagStore seriesTagStore;

	/// <summary>
	/// CreateView で生成された ISynchronizedView。
	/// ViewChanged イベントを購読して、削除・置換時に SeriesCardViewModel を Dispose する。
	/// </summary>
	private readonly ISynchronizedView<MangaSeriesViewModel, HomeSeriesCardViewModel> homeCardsView;

	/// <summary>
	/// WPF バインド用の Home 用作品 ViewModel コレクション（同期ビュー）。
	/// MangaSeriesStore.All の変更を自動的に反映します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<HomeSeriesCardViewModel> HomeCards { get; }

	/// <summary>
	/// <see cref="HomeSeriesStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaSeriesStore">作品ストア。</param>
	/// <param name="bindingQueueStore">製本開始キュー ストア。</param>
	/// <param name="seriesTagStore">タグ変更追跡ストア。</param>
	public HomeSeriesStore(MangaSeriesStore mangaSeriesStore, BindingQueueStore bindingQueueStore, SeriesTagStore seriesTagStore)
	{
		this.mangaSeriesStore = mangaSeriesStore;
		this.bindingQueueStore = bindingQueueStore;
		this.seriesTagStore = seriesTagStore;

		// Home 用作品 ViewModel コレクションを生成
		// MangaSeriesViewModel から SeriesCardViewModel へ変換
		this.homeCardsView = this.mangaSeriesStore.All
			.CreateView(seriesViewModel =>
			{
				var card = new HomeSeriesCardViewModel(seriesViewModel, this.mangaSeriesStore, this.seriesTagStore);
				// 生成時点で現在の BindingQueueStore.Queue を確認して IsSelected を初期化
				var isInQueue = this.bindingQueueStore.Contains(seriesViewModel.Series.Value.SeriesId);
				card.SetIsSelected(isInQueue);
				return card;
			});

		this.HomeCards = this.homeCardsView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// ISynchronizedView の ViewChanged イベントを購読
		// 削除・置換・ソートなどの変更で、SeriesCardViewModel のライフサイクルを管理
		this.homeCardsView.ViewChanged += this.onHomeCardsViewChanged;

		// BindingQueueStore.Queue の変更を監視して IsSelected を更新する
		// ObservableList<T>.IObservableCollection<T>.CollectionChanged を直接購読
		// Add / Remove / Replace / Reset に対応
		this.bindingQueueStore.Queue.CollectionChanged += this.onBindingQueueCollectionChanged;

		// 初期状態でQueue内の全カードを正しく初期化
		this.initializeIsSelectedFromQueue();
	}

	/// <summary>
	/// ViewChanged イベント ハンドラ。
	/// ISynchronizedView での削除・置換・ソート時に SeriesCardViewModel を適切に Dispose する。
	/// </summary>
	private void onHomeCardsViewChanged(in SynchronizedViewChangedEventArgs<MangaSeriesViewModel, HomeSeriesCardViewModel> e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Remove:
				// 削除された SeriesCardViewModel を Dispose
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
				// 置換前の SeriesCardViewModel を Dispose
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
				// IsClear で判定し、Clear の場合だけ旧 SeriesCardViewModel を Dispose
				if (e.SortOperation.IsClear)
				{
					// Clear で削除された SeriesCardViewModel を Dispose
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				// Sort / Reverse の場合は現在有効な SeriesCardViewModel を Dispose しない
				break;
		}
	}

	/// <summary>
	/// BindingQueueStore.Queue の CollectionChanged イベント ハンドラ。
	/// Queue への Add / Remove / Replace / Clear により、対応する SeriesCardViewModel.IsSelected を更新する。
	/// </summary>
	private void onBindingQueueCollectionChanged(in NotifyCollectionChangedEventArgs<BindingSeries> e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				// 追加されたカードを IsSelected = true にする
				if (e.IsSingleItem)
				{
					// 単一要素追加
					var card = this.findCardBySeriesId(e.NewItem.Series.SeriesId);
					if (card != null)
					{
						card.SetIsSelected(true);
					}
				}
				else
				{
					// 複数要素追加
					foreach (var bindingSeries in e.NewItems)
					{
						var card = this.findCardBySeriesId(bindingSeries.Series.SeriesId);
						if (card != null)
						{
							card.SetIsSelected(true);
						}
					}
				}
				break;

			case NotifyCollectionChangedAction.Remove:
				// 削除されたカードを IsSelected = false にする
				if (e.IsSingleItem)
				{
					// 単一要素削除
					var card = this.findCardBySeriesId(e.OldItem.Series.SeriesId);
					if (card != null)
					{
						card.SetIsSelected(false);
					}
				}
				else
				{
					// 複数要素削除
					foreach (var bindingSeries in e.OldItems)
					{
						var card = this.findCardBySeriesId(bindingSeries.Series.SeriesId);
						if (card != null)
						{
							card.SetIsSelected(false);
						}
					}
				}
				break;

			case NotifyCollectionChangedAction.Replace:
				// 置換時も対応するカードの状態を更新
				if (e.IsSingleItem)
				{
					// 単一要素置換
					var oldCard = this.findCardBySeriesId(e.OldItem.Series.SeriesId);
					if (oldCard != null)
					{
						oldCard.SetIsSelected(false);
					}
					var newCard = this.findCardBySeriesId(e.NewItem.Series.SeriesId);
					if (newCard != null)
					{
						newCard.SetIsSelected(true);
					}
				}
				else
				{
					// 複数要素置換
					foreach (var bindingSeries in e.OldItems)
					{
						var card = this.findCardBySeriesId(bindingSeries.Series.SeriesId);
						if (card != null)
						{
							card.SetIsSelected(false);
						}
					}
					foreach (var bindingSeries in e.NewItems)
					{
						var card = this.findCardBySeriesId(bindingSeries.Series.SeriesId);
						if (card != null)
						{
							card.SetIsSelected(true);
						}
					}
				}
				break;

			case NotifyCollectionChangedAction.Reset:
				// Reset (Clear / ReplaceAll) の場合は全カードを再照合
				// Queue の内容を確認して全カードの IsSelected を再初期化
				this.initializeIsSelectedFromQueue();
				break;
		}
	}

	/// <summary>
	/// 現在の BindingQueueStore.Queue 状態から、すべての SeriesCardViewModel.IsSelected を初期化する。
	/// </summary>
	private void initializeIsSelectedFromQueue()
	{
		foreach (var card in this.HomeCards)
		{
			var isInQueue = this.bindingQueueStore.Queue.Any(bs => bs.Series.SeriesId == card.Series.Value.SeriesId);
			card.SetIsSelected(isInQueue);
		}
	}

	/// <summary>
	/// SeriesId から対応する SeriesCardViewModel を検索する。
	/// </summary>
	private HomeSeriesCardViewModel? findCardBySeriesId(long seriesId)
	{
		return this.HomeCards.FirstOrDefault(card => card.Series.Value.SeriesId == seriesId);
	}

	/// <summary>
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		// HomeCards の ViewChanged イベント購読を解除
		this.homeCardsView.ViewChanged -= this.onHomeCardsViewChanged;

		// Queue の CollectionChanged イベント購読を解除
		this.bindingQueueStore.Queue.CollectionChanged -= this.onBindingQueueCollectionChanged;

		// 現在 View に残っている SeriesCardViewModel をすべて Dispose
		foreach (var card in this.homeCardsView)
		{
			card?.Dispose();
		}

		// ISynchronizedView を Dispose（元 Collection からの購読解除に必要）
		this.homeCardsView.Dispose();

		// WPF バインド用 HomeCards を Dispose
		this.HomeCards.Dispose();

		// R3 購読を Dispose
		this.disposableBag.Dispose();
	}
}
