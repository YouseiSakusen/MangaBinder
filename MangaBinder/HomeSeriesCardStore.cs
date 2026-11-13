using MangaBinder.Series;
using ObservableCollections;
using R3;
using System.Collections.Specialized;

namespace MangaBinder;

/// <summary>
/// Home 画面専用の表示用派生 View を保持する Store です。
/// MangaSeriesStore.All の Reactive な変更を監視し、
/// SeriesCardViewModel 一覧へ自動的に変換します。
/// </summary>
public class HomeSeriesCardStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 作品ストア。
	/// </summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>
	/// CreateView で生成された ISynchronizedView。
	/// ViewChanged イベントを購読して、削除・置換時に SeriesCardViewModel を Dispose する。
	/// </summary>
	private readonly ISynchronizedView<MangaSeriesNotifier, HomeSeriesCardViewModel> homeCardsView;

	/// <summary>
	/// WPF バインド用の Home 用作品 ViewModel コレクション（同期ビュー）。
	/// MangaSeriesStore.All の変更を自動的に反映します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<HomeSeriesCardViewModel> HomeCards { get; }

	/// <summary>
	/// <see cref="HomeSeriesCardStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaSeriesStore">作品ストア。</param>
	public HomeSeriesCardStore(MangaSeriesStore mangaSeriesStore)
	{
		this.mangaSeriesStore = mangaSeriesStore;

		// Home 用作品 ViewModel コレクションを生成
		// 共有 MangaSeriesNotifier から HomeSeriesCardViewModel へ変換
		// 製本待ちのチェック状態は共有 Notifier の IsBindingQueued が保持するため、ここでは扱わない
		this.homeCardsView = this.mangaSeriesStore.All
			.CreateView(seriesNotifier => new HomeSeriesCardViewModel(seriesNotifier));

		this.HomeCards = this.homeCardsView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// ISynchronizedView の ViewChanged イベントを購読
		// 削除・置換・ソートなどの変更で、SeriesCardViewModel のライフサイクルを管理
		this.homeCardsView.ViewChanged += this.onHomeCardsViewChanged;
	}

	/// <summary>
	/// ViewChanged イベント ハンドラ。
	/// ISynchronizedView での削除・置換・ソート時に SeriesCardViewModel を適切に Dispose する。
	/// </summary>
	private void onHomeCardsViewChanged(in SynchronizedViewChangedEventArgs<MangaSeriesNotifier, HomeSeriesCardViewModel> e)
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
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		// HomeCards の ViewChanged イベント購読を解除
		this.homeCardsView.ViewChanged -= this.onHomeCardsViewChanged;

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
