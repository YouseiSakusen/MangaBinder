using System.Collections.Specialized;
using MangaBinder.Series;
using MangaBinder.Tags;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 共通のタグ選択 Popup 用 ViewModel です。
/// 画面ごとに1個だけ生成し、Popup を開くたびに <see cref="BeginSession"/> で対象作品の
/// <see cref="MangaSeriesNotifier"/> を切り替えます。
/// タグ一覧は <see cref="TagStore.Tags"/>、チェック状態は対象 <see cref="MangaSeriesNotifier.Tags"/> から導出し、
/// 独自の正本は持ちません。
/// </summary>
public class SeriesTagPickerViewModel : IDisposable
{
	private DisposableBag disposableBag;
	private readonly IServiceScopeFactory serviceScopeFactory;
	private readonly Action<MangaSeries>? onSessionChanged;
	private readonly ISynchronizedView<TagNotifier, SeriesTagSelectionItem> selectableTagItemsView;
	private MangaSeriesNotifier? targetSeries;
	private bool hasChanges;

	/// <summary>
	/// Popup 用のタグ選択項目一覧を取得します。
	/// <see cref="TagStore.Tags"/> と同期し、項目はタグマスタの変更に追従します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<SeriesTagSelectionItem> SelectableTagItems { get; }

	/// <summary>
	/// <see cref="SeriesTagPickerViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="tagStore">タグマスタの正本を保持するストア。</param>
	/// <param name="serviceScopeFactory">Scoped の <c>TagManager</c> を操作時に解決するためのファクトリ。</param>
	/// <param name="onSessionChanged">
	/// Popup の操作セッション終了時に、タグの追加・削除があった場合だけ1回呼び出す処理。対象作品が渡されます。
	/// </param>
	public SeriesTagPickerViewModel(
		TagStore tagStore,
		IServiceScopeFactory serviceScopeFactory,
		Action<MangaSeries>? onSessionChanged = null)
	{
		this.serviceScopeFactory = serviceScopeFactory;
		this.onSessionChanged = onSessionChanged;

		this.selectableTagItemsView = tagStore.Tags
			.CreateView(tagNotifier =>
			{
				var item = new SeriesTagSelectionItem(tagNotifier, this.onItemToggled);
				item.IsChecked.Value = this.isTargetTag(tagNotifier);
				return item;
			});

		this.SelectableTagItems = this.selectableTagItemsView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.selectableTagItemsView.ViewChanged += this.onSelectableTagItemsViewChanged;
	}

	/// <summary>
	/// 対象作品を切り替えて、新しい操作セッションを開始します。
	/// 前のセッションが残っている場合は先に終了し、前の作品の変更状態が混入しないようにします。
	/// </summary>
	/// <param name="series">対象作品の共有 Notifier。</param>
	public void BeginSession(MangaSeriesNotifier series)
	{
		this.EndSession();

		this.targetSeries = series;
		this.hasChanges = false;
		this.targetSeries.Tags.CollectionChanged += this.onTargetTagsChanged;
		this.refreshCheckStates();
	}

	/// <summary>
	/// 現在の操作セッションを終了します。
	/// セッション中にタグの追加・削除があった場合だけ、コールバックを1回呼び出します。
	/// DB 保存は行いません。
	/// </summary>
	public void EndSession()
	{
		if (this.targetSeries is null)
		{
			return;
		}

		var target = this.targetSeries;
		var changed = this.hasChanges;

		target.Tags.CollectionChanged -= this.onTargetTagsChanged;
		this.targetSeries = null;
		this.hasChanges = false;
		this.refreshCheckStates();

		if (changed)
		{
			this.onSessionChanged?.Invoke(target.Series.Value);
		}
	}

	/// <summary>
	/// リソースを解放します。共有 <see cref="TagNotifier"/> と対象 <see cref="MangaSeriesNotifier"/> は破棄しません。
	/// </summary>
	public void Dispose()
	{
		if (this.targetSeries is not null)
		{
			this.targetSeries.Tags.CollectionChanged -= this.onTargetTagsChanged;
			this.targetSeries = null;
		}

		this.selectableTagItemsView.ViewChanged -= this.onSelectableTagItemsViewChanged;

		foreach (var item in this.selectableTagItemsView)
		{
			item.Dispose();
		}

		this.selectableTagItemsView.Dispose();
		this.SelectableTagItems.Dispose();
		this.disposableBag.Dispose();
	}

	/// <summary>
	/// 指定タグが現在の対象作品に付与されているかどうかを判定します。
	/// </summary>
	private bool isTargetTag(TagNotifier tagNotifier)
	{
		return this.targetSeries is not null && this.targetSeries.Tags.Contains(tagNotifier);
	}

	/// <summary>
	/// 全項目のチェック状態を、現在の対象作品のタグから再導出します。
	/// </summary>
	private void refreshCheckStates()
	{
		foreach (var item in this.selectableTagItemsView)
		{
			item.IsChecked.Value = this.isTargetTag(item.TagNotifier);
		}
	}

	/// <summary>
	/// 対象作品のタグ変更時に、チェック状態を再導出します。
	/// </summary>
	private void onTargetTagsChanged(in NotifyCollectionChangedEventArgs<TagNotifier> e)
	{
		this.refreshCheckStates();
	}

	/// <summary>
	/// タグのチェック操作を、<c>TagManager</c> 経由で対象作品へ反映します。
	/// </summary>
	private void onItemToggled(TagNotifier tagNotifier)
	{
		var target = this.targetSeries
			?? throw new InvalidOperationException("Popup の操作セッションが開始されていません。");

		var series = target.Series.Value;
		var tagId = tagNotifier.Tag.Value.TagId;

		using var scope = this.serviceScopeFactory.CreateScope();
		var tagManager = scope.ServiceProvider.GetRequiredService<TagManager>();

		if (series.Tags.Any(t => t.TagId == tagId))
		{
			tagManager.RemoveTagFromSeries(series, tagId);
		}
		else
		{
			tagManager.AddTagToSeries(series, tagId);
		}

		this.hasChanges = true;
	}

	/// <summary>
	/// 選択項目 View の変更時に、除外された項目を Dispose します。
	/// </summary>
	private void onSelectableTagItemsViewChanged(in SynchronizedViewChangedEventArgs<TagNotifier, SeriesTagSelectionItem> e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Remove:
			case NotifyCollectionChangedAction.Replace:
				if (e.IsSingleItem)
				{
					e.OldItem.View?.Dispose();
				}
				else
				{
					foreach (var item in e.OldViews)
					{
						item?.Dispose();
					}
				}
				break;

			case NotifyCollectionChangedAction.Reset:
				// Sort / Reverse では有効な項目を Dispose しない。Clear の場合だけ旧項目を Dispose する
				if (e.SortOperation.IsClear)
				{
					foreach (var item in e.OldViews)
					{
						item?.Dispose();
					}
				}
				break;
		}
	}
}
