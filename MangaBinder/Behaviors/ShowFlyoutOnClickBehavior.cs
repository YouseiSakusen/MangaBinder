using System.Windows;
using System.Windows.Controls.Primitives;
using Wpf.Ui.Controls;

namespace MangaBinder.Behaviors;

/// <summary>
/// <see cref="ButtonBase"/> にアタッチし、クリック時に <see cref="Flyout"/> を開く添付ビヘイビアです。
/// XAML 定義した Flyout をコードビハインドを使わずに表示できます。
/// </summary>
public static class ShowFlyoutOnClickBehavior
{
	/// <summary>クリック時に表示する Flyout の添付プロパティです。</summary>
	public static readonly DependencyProperty FlyoutProperty =
		DependencyProperty.RegisterAttached(
			"Flyout",
			typeof(Flyout),
			typeof(ShowFlyoutOnClickBehavior),
			new PropertyMetadata(null, OnFlyoutChanged));

	/// <summary><see cref="FlyoutProperty"/> の getter です。</summary>
	public static Flyout GetFlyout(DependencyObject obj)
		=> (Flyout)obj.GetValue(FlyoutProperty);

	/// <summary><see cref="FlyoutProperty"/> の setter です。</summary>
	public static void SetFlyout(DependencyObject obj, Flyout value)
		=> obj.SetValue(FlyoutProperty, value);

	private static void OnFlyoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not ButtonBase button)
			return;

		if (e.NewValue is not null)
			button.Click += OnButtonClick;
		else
			button.Click -= OnButtonClick;
	}

	private static void OnButtonClick(object sender, RoutedEventArgs e)
	{
		if (sender is not ButtonBase button)
			return;

		var flyout = GetFlyout(button);
		if (flyout is null)
			return;
			
		// IsOpen を直接トグルする代わりに、Show / Hide メソッドを使用
		if (flyout.IsOpen)
		{
			flyout.Hide();
		}
		else
		{
			flyout.Show();
		}
	}
}
