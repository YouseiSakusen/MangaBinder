using MangaBinder.Bindings;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MangaBinder.Behaviors;

/// <summary>
/// ListView の行クリックおよび Space キーで、MaterialItem のチェック状態を反転する添付ビヘイビアです。
/// CheckBox 自体のクリックは CheckBox 本来の動作に任せ、二重反転しません。
/// </summary>
public static class ListViewItemCheckToggleBehavior
{
	/// <summary>IsEnabled 添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(ListViewItemCheckToggleBehavior),
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

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not ListView listView)
			return;

		if (e.OldValue is true)
		{
			listView.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
			listView.PreviewKeyDown -= OnPreviewKeyDown;
		}

		if (e.NewValue is true)
		{
			listView.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
			listView.PreviewKeyDown += OnPreviewKeyDown;
		}
	}

	/// <summary>
	/// 行クリック時にチェック状態を反転します。CheckBox 上のクリックは処理しません。
	/// </summary>
	private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource is not DependencyObject clicked)
			return;

		for (var current = clicked; current != null; current = getParent(current))
		{
			if (current is CheckBox)
				return;

			if (current is ListViewItem item)
			{
				if (item.DataContext is MaterialItem material)
				{
					material.IsChecked.Value = !material.IsChecked.Value;
					e.Handled = true;
				}

				return;
			}
		}
	}

	/// <summary>
	/// Space キー押下時に、選択中の行のチェック状態を反転します。IME 有効時も考慮します。
	/// </summary>
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		var key = e.Key == Key.ImeProcessed
			? e.ImeProcessedKey
			: e.Key;

		if (key != Key.Space)
			return;

		if (sender is not ListView listView || listView.SelectedItem is not MaterialItem material)
			return;

		material.IsChecked.Value = !material.IsChecked.Value;
		e.Handled = true;
	}

	private static DependencyObject? getParent(DependencyObject element)
		=> element is Visual or System.Windows.Media.Media3D.Visual3D
			? VisualTreeHelper.GetParent(element)
			: LogicalTreeHelper.GetParent(element);
}
