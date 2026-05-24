using MangaBinder.Binding;
using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Binding;

/// <summary>
/// TreeView の SelectedItem を ViewModel へ双方向バインドするための添付ビヘイビアです。
/// </summary>
public static class TreeViewSelectedItemBehavior
{
	/// <summary>SelectedItem 添付プロパティです。</summary>
	public static readonly DependencyProperty SelectedItemProperty =
		DependencyProperty.RegisterAttached(
			"SelectedItem",
			typeof(object),
			typeof(TreeViewSelectedItemBehavior),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

	/// <summary>SelectedItem 添付プロパティの値を取得します。</summary>
	/// <param name="obj">値を取得する対象の <see cref="DependencyObject"/>。</param>
	/// <returns>現在の選択アイテム。</returns>
	public static object? GetSelectedItem(DependencyObject obj)
		=> obj.GetValue(SelectedItemProperty);

	/// <summary>SelectedItem 添付プロパティの値を設定します。</summary>
	/// <param name="obj">値を設定する対象の <see cref="DependencyObject"/>。</param>
	/// <param name="value">設定する選択アイテム。</param>
	public static void SetSelectedItem(DependencyObject obj, object? value)
		=> obj.SetValue(SelectedItemProperty, value);

	/// <summary>
	/// SelectedItem 添付プロパティが変更されたときに呼び出されます。
	/// </summary>
	private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TreeView treeView)
			return;

		treeView.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
		treeView.SelectedItemChanged += OnTreeViewSelectedItemChanged;
	}

	/// <summary>
	/// TreeView の SelectedItem が変更されたときに添付プロパティへ反映します。
	/// </summary>
	private static void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	{
		if (sender is TreeView treeView)
			SetSelectedItem(treeView, e.NewValue);
	}
}
