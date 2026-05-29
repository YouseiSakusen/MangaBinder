using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace MangaBinder.Binding;

/// <summary>
/// ListView にアタッチし、スクロール発生時に開いているタグ Popup 用 ToggleButton を閉じる添付ビヘイビアです。
/// </summary>
public static class CloseToggleOnScrollBehavior
{
	/// <summary>ビヘイビアの有効化を制御する添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(CloseToggleOnScrollBehavior),
			new PropertyMetadata(false, OnIsEnabledChanged));

	/// <summary>内部で保持する ScrollViewer の添付プロパティキーです。</summary>
	private static readonly DependencyProperty ScrollViewerKey =
		DependencyProperty.RegisterAttached(
			"ScrollViewer",
			typeof(ScrollViewer),
			typeof(CloseToggleOnScrollBehavior));

	/// <summary>IsEnabled 添付プロパティの値を取得します。</summary>
	public static bool GetIsEnabled(DependencyObject obj)
		=> (bool)obj.GetValue(IsEnabledProperty);

	/// <summary>IsEnabled 添付プロパティの値を設定します。</summary>
	public static void SetIsEnabled(DependencyObject obj, bool value)
		=> obj.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element)
			return;

		element.Loaded -= OnLoaded;
		element.Unloaded -= OnUnloaded;

		if ((bool)e.NewValue)
		{
			element.Loaded += OnLoaded;
			element.Unloaded += OnUnloaded;
		}
	}

	private static void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement element)
			return;

		// ListView の VisualTree 構築完了後に ScrollViewer を取得・購読する
		// （ScrollViewerOffsetBehavior と同じ ContextIdle タイミング）
		element.Dispatcher.InvokeAsync(() =>
		{
			var scrollViewer = FindScrollViewer(element);
			if (scrollViewer is null)
				return;

			// 既存の購読を解除してから再購読（Loaded が複数回来た場合の二重購読防止）
			var previous = (ScrollViewer?)element.GetValue(ScrollViewerKey);
			if (previous is not null)
				previous.ScrollChanged -= OnScrollChanged;

			scrollViewer.ScrollChanged += OnScrollChanged;
			element.SetValue(ScrollViewerKey, scrollViewer);
		}, DispatcherPriority.ContextIdle);
	}

	private static void OnUnloaded(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement element)
			return;

		var scrollViewer = (ScrollViewer?)element.GetValue(ScrollViewerKey);
		if (scrollViewer is not null)
			scrollViewer.ScrollChanged -= OnScrollChanged;

		element.SetValue(ScrollViewerKey, null);
	}

	private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (e.VerticalChange == 0 && e.HorizontalChange == 0)
			return;

		if (sender is not ScrollViewer scrollViewer)
			return;

		// ScrollViewer の親をたどって ListView を取得する
		var listView = FindAncestor<ListView>(scrollViewer);
		if (listView is null)
			return;

		// 仮想化で現在生成済みのコンテナのみ走査する
		foreach (var item in listView.Items)
		{
			var container = listView.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
			if (container is null)
				continue;

			var toggleButton = FindToggleButton(container);
			if (toggleButton is { IsChecked: true })
				toggleButton.IsChecked = false;
		}
	}

	/// <summary>VisualTree の子孫から最初の <see cref="ScrollViewer"/> を返します。</summary>
	private static ScrollViewer? FindScrollViewer(DependencyObject parent)
	{
		var count = VisualTreeHelper.GetChildrenCount(parent);
		for (var i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			if (child is ScrollViewer sv)
				return sv;
			var found = FindScrollViewer(child);
			if (found is not null)
				return found;
		}
		return null;
	}

	/// <summary>VisualTree の子孫から完全型一致で最初の <see cref="ToggleButton"/> を返します。</summary>
	/// <remarks>
	/// <see cref="CheckBox"/> は <see cref="ToggleButton"/> の派生クラスのため、
	/// <c>is ToggleButton</c> では誤検出されます。
	/// <c>GetType() == typeof(ToggleButton)</c> で完全一致のみを対象にします。
	/// </remarks>
	private static ToggleButton? FindToggleButton(DependencyObject parent)
	{
		var count = VisualTreeHelper.GetChildrenCount(parent);
		for (var i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			if (child.GetType() == typeof(ToggleButton))
				return (ToggleButton)child;
			var result = FindToggleButton(child);
			if (result is not null)
				return result;
		}
		return null;
	}

	/// <summary>VisualTree を遡り最初の <typeparamref name="T"/> 祖先を返します。</summary>
	private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
	{
		var current = VisualTreeHelper.GetParent(child);
		while (current is not null)
		{
			if (current is T found)
				return found;
			current = VisualTreeHelper.GetParent(current);
		}
		return null;
	}
}
