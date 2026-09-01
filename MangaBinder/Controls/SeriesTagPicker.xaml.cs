using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Controls;

/// <summary>
/// SeriesTagPicker.xaml の相互作用ロジック
/// </summary>
public partial class SeriesTagPicker : UserControl
{
	private SeriesTagSelectorViewModel? currentViewModel;

	/// <summary>
	/// ButtonContent DependencyProperty を取得または設定します。
	/// </summary>
	public object ButtonContent
	{
		get => this.GetValue(ButtonContentProperty);
		set => this.SetValue(ButtonContentProperty, value);
	}

	/// <summary>
	/// ButtonContent DependencyProperty の定義。
	/// </summary>
	public static readonly DependencyProperty ButtonContentProperty =
		DependencyProperty.Register(
			nameof(ButtonContent),
			typeof(object),
			typeof(SeriesTagPicker),
			new PropertyMetadata(null));

	/// <summary>
	/// SeriesTagPicker の新しいインスタンスを初期化します。
	/// </summary>
	public SeriesTagPicker()
	{
		this.InitializeComponent();
		this.DataContextChanged += this.onDataContextChanged;
		this.Loaded += this.onLoaded;
		this.Unloaded += this.onUnloaded;
	}

	/// <summary>
	/// DataContext が変更されたときのハンドラー。
	/// </summary>
	private void onDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		// 前の ViewModel からの購読を解除
		this.unsubscribeSelectableTagItems();

		// 新しい ViewModel へ更新
		this.currentViewModel = this.DataContext as SeriesTagSelectorViewModel;

		// Loaded 状態の場合のみ新しい ViewModel を購読
		if (this.IsLoaded)
		{
			this.subscribeSelectableTagItems();
		}

		this.updateDisplayItems();
	}

	/// <summary>
	/// UserControl が Loaded されたときのハンドラー。
	/// </summary>
	private void onLoaded(object sender, RoutedEventArgs e)
	{
		this.subscribeSelectableTagItems();
		this.updateDisplayItems();
	}

	/// <summary>
	/// UserControl が Unloaded されたときのハンドラー。
	/// </summary>
	private void onUnloaded(object sender, RoutedEventArgs e)
	{
		this.unsubscribeSelectableTagItems();
	}

	/// <summary>
	/// SelectableTagItems の CollectionChanged イベントハンドラー。
	/// </summary>
	private void onSelectableTagItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		this.updateDisplayItems();
	}

	/// <summary>
	/// 現在の ViewModel の SelectableTagItems.CollectionChanged を購読します。
	/// </summary>
	private void subscribeSelectableTagItems()
	{
		// 二重購読を防ぐため、先に解除してから購読
		this.unsubscribeSelectableTagItems();

		if (this.currentViewModel?.SelectableTagItems is INotifyCollectionChanged collection)
		{
			collection.CollectionChanged += this.onSelectableTagItemsChanged;
		}
	}

	/// <summary>
	/// 現在の ViewModel の SelectableTagItems.CollectionChanged の購読を解除します。
	/// </summary>
	private void unsubscribeSelectableTagItems()
	{
		if (this.currentViewModel?.SelectableTagItems is INotifyCollectionChanged collection)
		{
			collection.CollectionChanged -= this.onSelectableTagItemsChanged;
		}
	}

	/// <summary>
	/// 表示用アイテム一覧を更新します。
	/// </summary>
	private void updateDisplayItems()
	{
		// CurrentViewModel が null の場合は空リストを設定
		if (this.currentViewModel?.SelectableTagItems == null)
		{
			this.TagItemsControl.ItemsSource = new List<SeriesTagPickerDisplayItem>();
			this.TagItemsControl.Tag = 2;
			return;
		}

		// SelectableTagItems から実タグを明示的にコピー
		// （NotifyCollectionChangedSynchronizedViewList<T> は ToList() に未対応）
		var realTags = new List<SeriesTagSelectionItem>();
		foreach (var item in this.currentViewModel.SelectableTagItems)
		{
			realTags.Add(item);
		}

		var tagCount = realTags.Count;
		var columns = 2;

		// 列数判定：実タグ数が24件以下なら2列、25件以上なら3列
		if (tagCount > 24)
		{
			columns = 3;
		}

		// 不足セル数を計算
		var placeholderCount = (columns - (tagCount % columns)) % columns;

		// 表示用リストを構成：プレースホルダーを先頭に、その後ろに実タグを配置
		var displayItems = new List<SeriesTagPickerDisplayItem>();

		// 不足セル（プレースホルダー）を追加
		for (var i = 0; i < placeholderCount; i++)
		{
			displayItems.Add(new SeriesTagPickerDisplayItem
			{
				IsPlaceholder = true,
				TagItem = null
			});
		}

		// 実タグを追加
		foreach (var tag in realTags)
		{
			displayItems.Add(new SeriesTagPickerDisplayItem
			{
				IsPlaceholder = false,
				TagItem = tag
			});
		}

		this.TagItemsControl.ItemsSource = displayItems;
		this.TagItemsControl.Tag = columns;
	}
}

/// <summary>
/// SeriesTagPicker 内部専用の表示用アイテム。
/// </summary>
internal sealed class SeriesTagPickerDisplayItem
{
	/// <summary>
	/// タグ選択項目を取得または設定します。プレースホルダーの場合は null。
	/// </summary>
	public SeriesTagSelectionItem? TagItem { get; init; }

	/// <summary>
	/// このアイテムがプレースホルダー（空セル）かどうかを取得または設定します。
	/// </summary>
	public bool IsPlaceholder { get; init; }
}

