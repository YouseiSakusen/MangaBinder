using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MangaBinder.Behaviors;

/// <summary>
/// ListView / ItemsControl にアタッチし、マウスホイール1ノッチで
/// 現在のViewportに表示可能な範囲分（1ページ）スクロールする添付ビヘイビアです。
/// 論理スクロール（CanContentScroll="True"）を使用するコントロール向けです。
/// </summary>
public static class ViewportPageScrollBehavior
{
	/// <summary>ビヘイビアの有効化を制御する添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(ViewportPageScrollBehavior),
			new PropertyMetadata(false, onIsEnabledChanged));

	/// <summary>内部で保持する ScrollViewer の添付プロパティキーです。</summary>
	private static readonly DependencyProperty scrollViewerKey =
		DependencyProperty.RegisterAttached(
			"ScrollViewer",
			typeof(ScrollViewer),
			typeof(ViewportPageScrollBehavior));

	/// <summary>高精度マウス用のDelta蓄積値を保持する添付プロパティキーです。</summary>
	private static readonly DependencyProperty accumulatedDeltaKey =
		DependencyProperty.RegisterAttached(
			"AccumulatedDelta",
			typeof(int),
			typeof(ViewportPageScrollBehavior),
			new PropertyMetadata(0));

	/// <summary>IsEnabled 添付プロパティの値を取得します。</summary>
	public static bool GetIsEnabled(DependencyObject obj)
		=> (bool)obj.GetValue(IsEnabledProperty);

	/// <summary>IsEnabled 添付プロパティの値を設定します。</summary>
	public static void SetIsEnabled(DependencyObject obj, bool value)
		=> obj.SetValue(IsEnabledProperty, value);

	/// <summary>
	/// IsEnabled 添付プロパティが変更されたときに呼び出されます。
	/// </summary>
	private static void onIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element)
			return;

		element.PreviewMouseWheel -= onPreviewMouseWheel;
		element.Loaded -= onLoaded;
		element.Unloaded -= onUnloaded;

		if ((bool)e.NewValue)
		{
			element.PreviewMouseWheel += onPreviewMouseWheel;
			element.Loaded += onLoaded;
			element.Unloaded += onUnloaded;
		}
	}

	/// <summary>
	/// 要素がロードされたときに呼び出されます。
	/// 内部の ScrollViewer を探索して保持します。
	/// </summary>
	private static void onLoaded(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement element)
			return;

		// ScrollViewer が見つからない場合はスキップ
		var scrollViewer = findScrollViewer(element);
		element.SetValue(scrollViewerKey, scrollViewer);

		// 蓄積Deltaをリセット
		element.SetValue(accumulatedDeltaKey, 0);
	}

	/// <summary>
	/// 要素がアンロードされたときに呼び出されます。
	/// </summary>
	private static void onUnloaded(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement element)
			return;

		element.SetValue(scrollViewerKey, null);
		element.SetValue(accumulatedDeltaKey, 0);
	}

	/// <summary>
	/// マウスホイールイベントをハンドルします。
	/// ViewportPageScroll を実行し、イベントを処理済みにします。
	/// </summary>
	private static void onPreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is not FrameworkElement element)
			return;

		// 親ListView自身のScrollViewerを取得
		var parentScrollViewer = (ScrollViewer?)element.GetValue(scrollViewerKey);
		if (parentScrollViewer is null)
			return;

		// このイベントが来た時点での元のソース（カード内部のScrollViewerなど）を確認
		if (e.OriginalSource is not DependencyObject originalSource)
			return;

		// ネストされたScrollViewerを探索
		ScrollViewer? nestedScrollViewer = null;

		// OriginalSource がScrollViewer の場合、親ListViewのScrollViewerではないか確認
		if (originalSource is ScrollViewer sourceScrollViewer)
		{
			// 親ListViewのScrollViewer本体でない場合だけネスト扱い
			if (!ReferenceEquals(sourceScrollViewer, parentScrollViewer))
			{
				nestedScrollViewer = sourceScrollViewer;
			}
		}
		else
		{
			// OriginalSourceがScrollViewerでない場合、
			// VisualTreeを遡ってネストされたScrollViewerを探す
			nestedScrollViewer = findNestedScrollViewer(originalSource, parentScrollViewer);
		}

		// ネストされたScrollViewerが該当方向へスクロール可能な場合は処理しない
		if (nestedScrollViewer is not null && canScrollInDirection(nestedScrollViewer, e.Delta))
		{
			return;
		}

		// 高精度マウス対応：Deltaを蓄積する
		var currentDelta = (int)element.GetValue(accumulatedDeltaKey);
		currentDelta += e.Delta;
		element.SetValue(accumulatedDeltaKey, currentDelta);

		// 標準の1ノッチ相当（120）に達するまで蓄積
		if (Math.Abs(currentDelta) < 120)
		{
			e.Handled = true;
			return;
		}

		// 1ノッチ以上の蓄積ができたら、ノッチ数と方向を計算
		var notchCount = Math.Abs(currentDelta) / 120;
		var remainingDelta = currentDelta % 120;
		element.SetValue(accumulatedDeltaKey, remainingDelta);

		// ノッチ数を符号付きで計算（蓄積Deltaの符号から方向決定）
		var pageCount = currentDelta > 0 ? -notchCount : notchCount;

		// ページスクロール実行
		scrollViewportPage(parentScrollViewer, pageCount);

		e.Handled = true;
	}

	/// <summary>
	/// ScrollViewer を現在のViewport分スクロールします。
	/// 正の pageCount は下方向、負の pageCount は上方向です。
	/// </summary>
	private static void scrollViewportPage(ScrollViewer scrollViewer, int pageCount)
	{
		if (pageCount == 0)
			return;

		// 現在のViewportに表示可能なアイテム数を取得
		var viewportItemCount = (int)Math.Max(1, Math.Floor(scrollViewer.ViewportHeight));
		//var viewportItemCount = (int)Math.Max(1, Math.Floor(scrollViewer.ViewportHeight) -1);

		// スクロール移動量を計算（アイテム単位）
		var moveAmount = viewportItemCount * pageCount;

		// 新しいオフセットを計算
		var newOffset = scrollViewer.VerticalOffset + moveAmount;

		// 有効範囲内に収める
		newOffset = Math.Max(0, Math.Min(newOffset, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight));

		// スクロール実行
		scrollViewer.ScrollToVerticalOffset(newOffset);
	}

	/// <summary>
	/// ScrollViewer が指定された方向へスクロール可能かどうかを判定します。
	/// </summary>
	private static bool canScrollInDirection(ScrollViewer scrollViewer, int delta)
	{
		if (delta > 0)
		{
			// 上方向スクロール要求（delta > 0）
			// VerticalOffsetがまだ0より大きければスクロール可能
			return scrollViewer.VerticalOffset > 0;
		}
		else
		{
			// 下方向スクロール要求（delta < 0）
			// ExtentHeight - ViewportHeight - VerticalOffset > 0 なら下余裕あり
			return scrollViewer.VerticalOffset < scrollViewer.ExtentHeight - scrollViewer.ViewportHeight;
		}
	}

	/// <summary>
	/// VisualTree を上方向に探索してネストされたScrollViewerを取得します。
	/// 親ListViewのScrollViewer本体に到達する前に見つかったScrollViewerを返します。
	/// 親ListViewのScrollViewer本体は除外します。
	/// </summary>
	private static ScrollViewer? findNestedScrollViewer(DependencyObject source, ScrollViewer parentScrollViewer)
	{
		var current = source;
		while (current is not null)
		{
			current = VisualTreeHelper.GetParent(current);
			if (current is null)
				break;

			if (current is ScrollViewer scrollViewer)
			{
				// 親ListViewのScrollViewer本体でない場合だけネスト扱い
				if (!ReferenceEquals(scrollViewer, parentScrollViewer))
				{
					return scrollViewer;
				}
			}

			// ListView / ItemsControl に到達したら探索終了
			if (current is ListView or ItemsControl)
				break;
		}
		return null;
	}

	/// <summary>
	/// VisualTree を下方向に探索して最初のScrollViewerを取得します。
	/// </summary>
	private static ScrollViewer? findScrollViewer(DependencyObject parent)
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			if (child is ScrollViewer scrollViewer)
				return scrollViewer;

			var found = findScrollViewer(child);
			if (found is not null)
				return found;
		}
		return null;
	}
}
