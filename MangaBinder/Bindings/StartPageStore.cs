using ObservableCollections;
using R3;
using System.Collections.Specialized;
using MangaBinder.Series;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本開始画面専用の状態と派生 View を保持する Store です。
/// BindingQueueStore.Queue の Reactive な変更を監視し、
/// StartPageSeriesCardViewModel 一覧へ自動的に変換します。
/// </summary>
public class StartPageStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 製本開始キュー ストア。
	/// </summary>
	private readonly BindingQueueStore bindingQueueStore;

	/// <summary>
	/// 作品ストア。共有 MangaSeriesViewModel を解決するために使用します。
	/// </summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>
	/// CreateView で生成された ISynchronizedView。
	/// ViewChanged イベントを購読して、削除・置換時に StartPageSeriesCardViewModel を Dispose する。
	/// </summary>
	private readonly ISynchronizedView<BindingSeries, StartPageSeriesCardViewModel> queueCardsView;

	/// <summary>
	/// WPF バインド用の製本開始キュー ViewModel コレクション（同期ビュー）。
	/// BindingQueueStore.Queue の変更を自動的に反映します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<StartPageSeriesCardViewModel> QueueCards { get; }

	/// <summary>
	/// <see cref="StartPageStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingQueueStore">製本開始キュー ストア。</param>
	/// <param name="mangaSeriesStore">作品ストア。</param>
	public StartPageStore(BindingQueueStore bindingQueueStore, MangaSeriesStore mangaSeriesStore)
	{
		this.bindingQueueStore = bindingQueueStore;
		this.mangaSeriesStore = mangaSeriesStore;

		// 製本開始キュー ViewModel コレクションを生成
		// BindingSeries から、共有 MangaSeriesViewModel を通じて StartPageSeriesCardViewModel へ変換
		this.queueCardsView = this.bindingQueueStore.Queue
			.CreateView(bindingSeries =>
			{
				// SeriesId から共有 MangaSeriesViewModel を解決
				var sharedViewModel = this.mangaSeriesStore.FindViewModelById(bindingSeries.Series.SeriesId);
				if (sharedViewModel is null)
				{
					throw new InvalidOperationException(
						$"BindingQueue に登録された SeriesId {bindingSeries.Series.SeriesId} が MangaSeriesStore.All に見つかりません。" +
						"BindingQueue と MangaSeriesStore.All の状態が不整合です。");
				}
				// 共有 ViewModel を渡してカードを生成
				return new StartPageSeriesCardViewModel(bindingSeries, sharedViewModel);
			});

		this.QueueCards = this.queueCardsView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// ISynchronizedView の ViewChanged イベントを購読
		// 削除・置換・ソートなどの変更で、StartPageSeriesCardViewModel のライフサイクルを管理
		this.queueCardsView.ViewChanged += this.onQueueCardsViewChanged;
	}

	/// <summary>
	/// ViewChanged イベント ハンドラ。
	/// ISynchronizedView での削除・置換・ソート時に StartPageSeriesCardViewModel を適切に Dispose する。
	/// </summary>
	private void onQueueCardsViewChanged(in SynchronizedViewChangedEventArgs<BindingSeries, StartPageSeriesCardViewModel> e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Remove:
				// 削除された StartPageSeriesCardViewModel を Dispose
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
				// 置換前の StartPageSeriesCardViewModel を Dispose
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
				// IsClear で判定し、Clear の場合だけ旧 StartPageSeriesCardViewModel を Dispose
				if (e.SortOperation.IsClear)
				{
					// Clear で削除された StartPageSeriesCardViewModel を Dispose
					foreach (var card in e.OldViews)
					{
						card?.Dispose();
					}
				}
				// Sort / Reverse の場合は現在有効な StartPageSeriesCardViewModel を Dispose しない
				break;
		}
	}

	/// <summary>
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		// QueueCards の ViewChanged イベント購読を解除
		this.queueCardsView.ViewChanged -= this.onQueueCardsViewChanged;

		// 現在 View に残っている StartPageSeriesCardViewModel をすべて Dispose
		foreach (var card in this.queueCardsView)
		{
			card?.Dispose();
		}

		// ISynchronizedView を Dispose（元 Collection からの購読解除に必要）
		this.queueCardsView.Dispose();

		// WPF バインド用 QueueCards を Dispose
		this.QueueCards.Dispose();

		// R3 購読を Dispose
		this.disposableBag.Dispose();
	}
}
