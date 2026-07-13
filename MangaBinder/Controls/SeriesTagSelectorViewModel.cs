using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
	private MangaSeries? targetSeries;
	private Action<MangaSeries>? onTagsChangedCallback;

	/// <summary>
	/// Popup用のタグ選択項目一覧を取得します。
	/// </summary>
	public ObservableCollection<SeriesTagSelectionItem> SelectableTagItems { get; }

	/// <summary>
	/// 選択済みタグ一覧を取得します。
	/// </summary>
	public ObservableCollection<MangaTag> SelectedTags { get; }

	/// <summary>
	/// Home等で利用する省略表示文字列を取得します。
	/// </summary>
	public BindableReactiveProperty<string> CompactDisplayText { get; }

	/// <summary>
	/// Popup の列数を取得します。
	/// </summary>
	public BindableReactiveProperty<int> Columns { get; }

	/// <summary>
	/// Popup の行数を取得します。
	/// </summary>
	public BindableReactiveProperty<int> Rows { get; }

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

		this.SelectableTagItems = new ObservableCollection<SeriesTagSelectionItem>();
		this.SelectedTags = new ObservableCollection<MangaTag>();

		this.CompactDisplayText = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.Columns = new BindableReactiveProperty<int>(2)
			.AddTo(ref this.disposableBag);

		this.Rows = new BindableReactiveProperty<int>(0)
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
		this.targetSeries = series ?? throw new ArgumentNullException(nameof(series));
		this.onTagsChangedCallback = onTagsChanged;

		// SelectedTags と CompactDisplayText を更新
		this.updateSelectedTagsAndDisplay();
	}

	/// <summary>
	/// 対象 MangaSeries をクリアします。
	/// </summary>
	public void ClearTarget()
	{
		this.targetSeries = null;
		this.onTagsChangedCallback = null;
		this.SelectedTags.Clear();
		this.CompactDisplayText.Value = string.Empty;
	}

	/// <summary>
	/// リソースを解放します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}

	/// <summary>
	/// 現在の対象 MangaSeries を元に、選択済みタグと表示テキストを更新します。
	/// </summary>
	/// <remarks>
	/// DB 更新や Dirty 管理は行わず、表示だけを更新する責務を持ちます。
	/// </remarks>
	public void Refresh()
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
		this.SelectableTagItems.Clear();

		var tags = this.mangaSeriesStore.GetTags()
			.OrderByDescending(t => t.DisplayOrder)
			.ThenByDescending(t => t.TagId)
			.ToList();

		// プレースホルダーセルを計算
		var tagCount = tags.Count;
		var columns = this.Columns.Value;
		var placeholderCount = (columns - (tagCount % columns)) % columns;

		// プレースホルダーを先頭に追加
		for (var i = 0; i < placeholderCount; i++)
		{
			var placeholderItem = new SeriesTagSelectionItem(null!, false)
			{
				IsPlaceholder = true
			};
			this.SelectableTagItems.Add(placeholderItem);
		}

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

			this.SelectableTagItems.Add(item);
		}

		// 行数を計算して通知
		this.Rows.Value = (tagCount + placeholderCount + columns - 1) / columns;
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
			this.SelectedTags.Clear();
			this.CompactDisplayText.Value = string.Empty;
			return;
		}

		// SelectedTags を更新
		this.SelectedTags.Clear();
		foreach (var tag in this.targetSeries.Tags)
		{
			this.SelectedTags.Add(tag);
		}

		// CompactDisplayText を更新（既存フォーマッタを利用）
		this.CompactDisplayText.Value = SeriesTagDisplayFormatter.Format(this.targetSeries.Tags);
	}
}
