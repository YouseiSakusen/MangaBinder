using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MangaBinder.Behaviors;

/// <summary>
/// VolumeSelectionPage の Singleton View に残っているスクロール位置を初期化する専用 Behavior です。
/// TreeView / ListView が表示されるたびに、内部 ScrollViewer のスクロール位置を先頭へ戻します。
/// </summary>
public static class VolumeSelectionInitializeBehavior
{
	/// <summary>IsEnabled 添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(VolumeSelectionInitializeBehavior),
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
	/// IsEnabled 添付プロパティが変更されたときに呼び出されます。
	/// Loaded / Unloaded イベントを登録・解除します。
	/// </summary>
	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element)
			return;

		// Loaded イベントハンドラを削除（重複登録防止）
		element.Loaded -= OnLoaded;

		// IsEnabled が true になった場合は Loaded イベントを登録
		if (e.NewValue is true)
		{
			element.Loaded += OnLoaded;
		}
	}

	/// <summary>
	/// 要素がロードされたときに呼び出されます。
	/// 内部の ScrollViewer を探して、スクロール位置を先頭（VerticalOffset=0, HorizontalOffset=0）へリセットします。
	/// </summary>
	private static void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement element)
			return;

		// Dispatcher.InvokeAsync で ScrollViewer が利用可能になったタイミングで初期化を実行
		element.Dispatcher.InvokeAsync(() =>
		{
			var scrollViewer = FindScrollViewer(element);
			if (scrollViewer == null)
				return;

			// スクロール位置を先頭へリセット
			scrollViewer.ScrollToVerticalOffset(0);
			scrollViewer.ScrollToHorizontalOffset(0);
		}, DispatcherPriority.ContextIdle);
	}

	/// <summary>
	/// Visual Tree から内部の ScrollViewer を探します。
	/// </summary>
	/// <param name="element">検索対象となる要素。</param>
	/// <returns>見つかった ScrollViewer、見つからない場合は null。</returns>
	private static ScrollViewer? FindScrollViewer(DependencyObject element)
	{
		// 子要素をたどって ScrollViewer を探す
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
		{
			var child = VisualTreeHelper.GetChild(element, i);

			if (child is ScrollViewer scrollViewer)
			{
				return scrollViewer;
			}

			// 子要素を再帰的に探索
			var found = FindScrollViewer(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}
}
