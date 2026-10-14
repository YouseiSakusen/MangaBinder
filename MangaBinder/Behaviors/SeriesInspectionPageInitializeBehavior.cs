using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Behaviors;

/// <summary>
/// SeriesInspectionPage の Singleton View に残っているスクロール位置を初期化する専用 Behavior です。
/// ViewModel / Store が持つ製本 Domain 状態は初期化しません。
/// </summary>
public static class SeriesInspectionPageInitializeBehavior
{
	/// <summary>IsEnabled 添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(SeriesInspectionPageInitializeBehavior),
			new PropertyMetadata(false, OnIsEnabledChanged));

	/// <summary>IsEnabled 添付プロパティの値を取得します。</summary>
	/// <param name="obj">値を取得する対象の <see cref="DependencyObject"/>。</param>
	/// <returns>現在の IsEnabled の値。</returns>
	public static bool GetIsEnabled(DependencyObject obj)
		=> (bool)obj.GetValue(IsEnabledProperty);

	/// <summary>IsEnabled 添付プロパティの値を設定します。</summary>
	/// <param name="obj">値を設定する対象の <see cref="DependencyObject"/>。</param>
	/// <param name="value">設定する値。</param>
	public static void SetIsEnabled(DependencyObject obj, bool value)
		=> obj.SetValue(IsEnabledProperty, value);

	/// <summary>
	/// IsEnabled 添付プロパティが変更されたときに Loaded イベントを登録・解除します。
	/// </summary>
	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element)
		{
			return;
		}

		element.Loaded -= OnLoaded;

		if (e.NewValue is true)
		{
			element.Loaded += OnLoaded;
		}
	}

	/// <summary>
	/// 画面が表示されるたびに、スクロール位置を先頭へ戻します。
	/// </summary>
	private static void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement page)
		{
			return;
		}

		if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(page))
		{
			return;
		}

		var listView = (ListView)page.FindName("VolumeCardsListView");
		var listScrollViewer = FindDescendant<ScrollViewer>(listView);
		listScrollViewer?.ScrollToVerticalOffset(0);
		listScrollViewer?.ScrollToHorizontalOffset(0);

		var scrollViewer = (ScrollViewer)page.FindName("SettingsScrollViewer");
		scrollViewer.ScrollToVerticalOffset(0);
		scrollViewer.ScrollToHorizontalOffset(0);
	}

	/// <summary>
	/// 指定要素の Visual Tree から最初に見つかった指定型の子孫を取得します。
	/// </summary>
	private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
	{
		for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
		{
			var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
			if (child is T match)
			{
				return match;
			}

			var nested = FindDescendant<T>(child);
			if (nested is not null)
			{
				return nested;
			}
		}

		return null;
	}
}
