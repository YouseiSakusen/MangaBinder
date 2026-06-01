using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MangaBinder.Behaviors;

/// <summary>
/// <see cref="ButtonBase"/> にアタッチし、クリック時に <see cref="ContextMenu"/> を開く添付ビヘイビアです。
/// code-behind を使わずにボタンクリックでコンテキストメニューを表示できます。
/// </summary>
public static class OpenContextMenuOnClickBehavior
{
	/// <summary>クリック時にコンテキストメニューを開くかどうかを制御する添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(OpenContextMenuOnClickBehavior),
			new PropertyMetadata(false, OnIsEnabledChanged));

	/// <summary><see cref="IsEnabledProperty"/> の getter です。</summary>
	public static bool GetIsEnabled(DependencyObject obj)
		=> (bool)obj.GetValue(IsEnabledProperty);

	/// <summary><see cref="IsEnabledProperty"/> の setter です。</summary>
	public static void SetIsEnabled(DependencyObject obj, bool value)
		=> obj.SetValue(IsEnabledProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not ButtonBase button)
			return;

		if ((bool)e.NewValue)
			button.Click += OnButtonClick;
		else
			button.Click -= OnButtonClick;
	}

	private static void OnButtonClick(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement element)
			return;

		if (element.ContextMenu is null)
			return;

		element.ContextMenu.PlacementTarget = element;
		element.ContextMenu.Placement = PlacementMode.Bottom;
		element.ContextMenu.IsOpen = true;
	}
}
