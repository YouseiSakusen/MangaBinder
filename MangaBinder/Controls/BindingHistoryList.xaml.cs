using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MangaBinder.Series;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBinder.Controls;

/// <summary>
/// 製本履歴の一覧です。
/// SeriesViewModel を Binding するだけで、履歴表示と更新追従を完結します。
/// </summary>
public partial class BindingHistoryList : UserControl
{
	/// <summary>表示対象の共有 MangaSeriesViewModel を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty SeriesViewModelProperty = DependencyProperty.Register(
		nameof(SeriesViewModel),
		typeof(MangaSeriesViewModel),
		typeof(BindingHistoryList),
		new PropertyMetadata(null, onSeriesViewModelChanged));

	/// <summary>ViewModel を表す読み取り専用依存関係プロパティのキー。</summary>
	private static readonly DependencyPropertyKey viewModelPropertyKey = DependencyProperty.RegisterReadOnly(
		nameof(ViewModel),
		typeof(BindingHistoryListViewModel),
		typeof(BindingHistoryList),
		new PropertyMetadata(null));

	/// <summary>ViewModel を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty ViewModelProperty = viewModelPropertyKey.DependencyProperty;

	/// <summary>
	/// 表示対象の共有 MangaSeriesViewModel を取得または設定します。
	/// </summary>
	public MangaSeriesViewModel? SeriesViewModel
	{
		get => (MangaSeriesViewModel?)this.GetValue(SeriesViewModelProperty);
		set => this.SetValue(SeriesViewModelProperty, value);
	}

	/// <summary>
	/// ViewModel を取得します。Loaded 中のみ有効です。
	/// </summary>
	public BindingHistoryListViewModel? ViewModel => (BindingHistoryListViewModel?)this.GetValue(ViewModelProperty);

	/// <summary>
	/// <see cref="BindingHistoryList"/> の新しいインスタンスを初期化します。
	/// </summary>
	public BindingHistoryList()
	{
		this.InitializeComponent();
		this.Loaded += this.onLoaded;
		this.Unloaded += this.onUnloaded;
	}

	/// <summary>
	/// 履歴一覧のスクロール位置を先頭へ戻します。
	/// </summary>
	public void ResetScrollPosition()
	{
		this.HistoryListView.ApplyTemplate();
		var scrollViewer = findDescendant<ScrollViewer>(this.HistoryListView);
		scrollViewer?.ScrollToVerticalOffset(0);
		scrollViewer?.ScrollToHorizontalOffset(0);
	}

	/// <summary>
	/// SeriesViewModel 変更時に ViewModel へ反映します。
	/// </summary>
	private static void onSeriesViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var list = (BindingHistoryList)d;
		list.ViewModel?.SetSeriesViewModel((MangaSeriesViewModel?)e.NewValue);
	}

	/// <summary>
	/// Loaded 時に ViewModel を生成します。
	/// </summary>
	private void onLoaded(object sender, RoutedEventArgs e)
	{
		if (DesignerProperties.GetIsInDesignMode(this))
			return;

		var viewModel = App.Services.GetRequiredService<BindingHistoryListViewModel>();
		viewModel.SetSeriesViewModel(this.SeriesViewModel);
		this.HistoryCard.DataContext = viewModel;
		this.SetValue(viewModelPropertyKey, viewModel);
	}

	/// <summary>
	/// Unloaded 時に ViewModel を破棄します。
	/// </summary>
	private void onUnloaded(object sender, RoutedEventArgs e)
	{
		if (DesignerProperties.GetIsInDesignMode(this))
			return;

		this.HistoryCard.DataContext = null;
		this.ViewModel?.Dispose();
		this.SetValue(viewModelPropertyKey, null);
	}

	/// <summary>
	/// Visual Tree から最初に見つかった指定型の子孫を取得します。
	/// </summary>
	private static T? findDescendant<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T match)
				return match;

			var nested = findDescendant<T>(child);
			if (nested is not null)
				return nested;
		}

		return null;
	}
}
