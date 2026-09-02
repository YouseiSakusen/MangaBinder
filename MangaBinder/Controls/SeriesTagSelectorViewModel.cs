using System;
using System.Collections.Generic;
using System.Linq;
using MangaBinder.Core.Formatters;
using MangaBinder.Series;
using MangaBinder.Tags;
using ObservableCollections;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// SeriesTagSelector 用の ViewModel。
/// 1つの MangaSeries に対するタグ選択・表示状態を管理します。
/// </summary>
public class SeriesTagSelectorViewModel : IDisposable
{
	private readonly MangaSeriesStore mangaSeriesStore;
	private readonly DisposableBag disposableBag = new();
	private readonly ObservableList<SeriesTagSelectionItem> selectableTagItems;
	private readonly ObservableList<MangaTag> selectedTags;
	private MangaSeries? targetSeries;
	private Action<MangaSeries>? onTagsChangedCallback;
	private NotifyCollectionChangedEventHandler<MangaTag>? tagsCollectionChangedHandler;

	/// <summary>
	/// Popup用のタグ選択項目一覧を取得します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<SeriesTagSelectionItem> SelectableTagItems { get; }

	/// <summary>
	/// 選択済みタグ一覧を取得します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<MangaTag> SelectedTags { get; }

	/// <summary>
	/// Home等で利用する省略表示文字列を取得します。
	/// </summary>
	public BindableReactiveProperty<string> CompactDisplayText { get; }

	/// <summary>
	/// Popup 準備コマンドを取得します。
	/// </summary>
	public ReactiveCommand<Unit> PreparePopupCommand { get; }

	/// <summary>
	/// SeriesTagSelectorViewModel の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaSeriesStore">MangaSeries のストア。</param>
	public SeriesTagSelectorViewModel(MangaSeriesStore mangaSeriesStore)
	{
		this.mangaSeriesStore = mangaSeriesStore ?? throw new ArgumentNullException(nameof(mangaSeriesStore));

		this.selectableTagItems = new ObservableList<SeriesTagSelectionItem>();
		this.SelectableTagItems = this.selectableTagItems
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.selectedTags = new ObservableList<MangaTag>();
		this.SelectedTags = this.selectedTags
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.CompactDisplayText = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.PreparePopupCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.PreparePopupCommand.Subscribe(_ =>
		{
			this.preparePopup();
		});
	}

	/// <summary>
	/// 対象 MangaSeries を設定します。
	/// </summary>
	/// <param name="series">対象 MangaSeries。</param>
	/// <param name="onTagsChanged">タグ変更時のコールバック。</param>
	public void SetTarget(MangaSeries series, Action<MangaSeries>? onTagsChanged = null)
	{
		// 前の購読を解除
		if (this.targetSeries != null && this.tagsCollectionChangedHandler is not null)
		{
			this.targetSeries.Tags.CollectionChanged -= this.tagsCollectionChangedHandler;
		}

		this.targetSeries = series ?? throw new ArgumentNullException(nameof(series));
		this.onTagsChangedCallback = onTagsChanged;

		// 新しいMangaSeries.Tagsの変更を監視
		this.tagsCollectionChangedHandler = this.onTargetTagsCollectionChanged;
		this.targetSeries.Tags.CollectionChanged += this.tagsCollectionChangedHandler;

		// SelectedTags と CompactDisplayText を更新
		this.updateSelectedTagsAndDisplay();
	}

	/// <summary>
	/// 対象 MangaSeries をクリアします。
	/// </summary>
	public void ClearTarget()
	{
		// 購読を解除
		if (this.targetSeries != null && this.tagsCollectionChangedHandler is not null)
		{
			this.targetSeries.Tags.CollectionChanged -= this.tagsCollectionChangedHandler;
		}

		this.targetSeries = null;
		this.onTagsChangedCallback = null;
		this.tagsCollectionChangedHandler = null;
		this.selectedTags.Clear();
		this.CompactDisplayText.Value = string.Empty;
	}

	/// <summary>
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		// Tags の購読を解除
		if (this.targetSeries != null && this.tagsCollectionChangedHandler is not null)
		{
			this.targetSeries.Tags.CollectionChanged -= this.tagsCollectionChangedHandler;
		}

		this.disposableBag.Dispose();
	}

	/// <summary>
	/// 対象 MangaSeries.Tags の CollectionChanged イベントハンドラー。
	/// </summary>
	private void onTargetTagsCollectionChanged(in NotifyCollectionChangedEventArgs<MangaTag> e)
	{
		this.updateSelectedTagsAndDisplay();
	}

	/// <summary>
	/// Popup 用のタグ一覧を準備します。
	/// </summary>
	private void preparePopup()
	{
		if (this.targetSeries == null)
		{
			return;
		}

		// 既存のイベント購読を削除するためクリア
		this.selectableTagItems.Clear();

		var tags = this.mangaSeriesStore.GetTags()
			.OrderByDescending(t => t.DisplayOrder)
			.ThenByDescending(t => t.TagId)
			.ToList();

		// 実際のタグを追加
		foreach (var tag in tags)
		{
			var isChecked = this.targetSeries.Tags.Any(t => t.TagId == tag.TagId);
			var item = new SeriesTagSelectionItem(tag, isChecked);

			item.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName != nameof(SeriesTagSelectionItem.IsChecked))
				{
					return;
				}

				this.onTagSelectionChanged(tag, item.IsChecked);
			};

			this.selectableTagItems.Add(item);
		}
	}

	/// <summary>
	/// タグ選択状態が変更されました。
	/// </summary>
	private void onTagSelectionChanged(MangaTag tag, bool isChecked)
	{
		if (this.targetSeries == null)
		{
			return;
		}

		if (isChecked)
		{
			// タグを追加（重複チェック）
			if (!this.targetSeries.Tags.Any(t => t.TagId == tag.TagId))
			{
				this.targetSeries.Tags.Add(tag);
			}
		}
		else
		{
			// タグを削除
			var existingTag = this.targetSeries.Tags.FirstOrDefault(t => t.TagId == tag.TagId);
			if (existingTag != null)
			{
				this.targetSeries.Tags.Remove(existingTag);
			}
		}

		// SelectedTags と CompactDisplayText を更新
		this.updateSelectedTagsAndDisplay();

		// コールバックを実行
		this.onTagsChangedCallback?.Invoke(this.targetSeries);
	}

	/// <summary>
	/// SelectedTags と CompactDisplayText を更新します。
	/// </summary>
	private void updateSelectedTagsAndDisplay()
	{
		if (this.targetSeries == null)
		{
			this.selectedTags.Clear();
			this.CompactDisplayText.Value = string.Empty;
			return;
		}

		// SelectedTags を更新
		this.selectedTags.Clear();
		foreach (var tag in this.targetSeries.Tags)
		{
			this.selectedTags.Add(tag);
		}

		// CompactDisplayText を更新（既存フォーマッタを利用）
		this.CompactDisplayText.Value = SeriesTagDisplayFormatter.Format(this.targetSeries.Tags);
	}
}
