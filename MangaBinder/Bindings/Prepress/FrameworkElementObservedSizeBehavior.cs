using System.Windows;

namespace MangaBinder.Bindings.Prepress;

/// <summary>
/// <see cref="FrameworkElement"/> の実際のサイズを ViewModel へ通知する添付ビヘイビアです。
/// </summary>
public static class FrameworkElementObservedSizeBehavior
{
	/// <summary>監視対象の ObservedWidth 添付プロパティです。</summary>
	public static readonly DependencyProperty ObservedWidthProperty =
		DependencyProperty.RegisterAttached(
			"ObservedWidth",
			typeof(double),
			typeof(FrameworkElementObservedSizeBehavior),
			new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	/// <summary>監視対象の ObservedHeight 添付プロパティです。</summary>
	public static readonly DependencyProperty ObservedHeightProperty =
		DependencyProperty.RegisterAttached(
			"ObservedHeight",
			typeof(double),
			typeof(FrameworkElementObservedSizeBehavior),
			new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	/// <summary>サイズ監視を有効にする Observe 添付プロパティです。</summary>
	public static readonly DependencyProperty ObserveProperty =
		DependencyProperty.RegisterAttached(
			"Observe",
			typeof(bool),
			typeof(FrameworkElementObservedSizeBehavior),
			new PropertyMetadata(false, OnObserveChanged));

	/// <summary>ObservedWidth の値を取得します。</summary>
	public static double GetObservedWidth(DependencyObject obj)
		=> (double)obj.GetValue(ObservedWidthProperty);

	/// <summary>ObservedWidth の値を設定します。</summary>
	public static void SetObservedWidth(DependencyObject obj, double value)
		=> obj.SetValue(ObservedWidthProperty, value);

	/// <summary>ObservedHeight の値を取得します。</summary>
	public static double GetObservedHeight(DependencyObject obj)
		=> (double)obj.GetValue(ObservedHeightProperty);

	/// <summary>ObservedHeight の値を設定します。</summary>
	public static void SetObservedHeight(DependencyObject obj, double value)
		=> obj.SetValue(ObservedHeightProperty, value);

	/// <summary>Observe の値を取得します。</summary>
	public static bool GetObserve(DependencyObject obj)
		=> (bool)obj.GetValue(ObserveProperty);

	/// <summary>Observe の値を設定します。</summary>
	public static void SetObserve(DependencyObject obj, bool value)
		=> obj.SetValue(ObserveProperty, value);

	private static void OnObserveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element)
			return;

		if ((bool)e.NewValue)
		{
			element.SizeChanged += OnSizeChanged;
			UpdateSize(element);
		}
		else
		{
			element.SizeChanged -= OnSizeChanged;
		}
	}

	private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is FrameworkElement element)
			UpdateSize(element);
	}

	private static void UpdateSize(FrameworkElement element)
	{
		SetObservedWidth(element, element.ActualWidth);
		SetObservedHeight(element, element.ActualHeight);
	}
}
