using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MangaBinder.Behaviors;

/// <summary>
/// TreeView の右クリックした TreeViewItem を選択状態にする Behavior です。
/// </summary>
public static class TreeViewRightClickSelectBehavior
{
	/// <summary>
	/// 右クリック選択の有効/無効を示す添付プロパティです。
	/// </summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(TreeViewRightClickSelectBehavior),
			new PropertyMetadata(false, OnIsEnabledChanged));

	/// <summary>
	/// IsEnabled 添付プロパティの値を取得します。
	/// </summary>
	/// <param name="obj">対象となる依存オブジェクト。</param>
	/// <returns>IsEnabled の値。</returns>
	public static bool GetIsEnabled(DependencyObject obj)
	{
		return (bool)obj.GetValue(IsEnabledProperty);
	}

	/// <summary>
	/// IsEnabled 添付プロパティの値を設定します。
	/// </summary>
	/// <param name="obj">対象となる依存オブジェクト。</param>
	/// <param name="value">設定する値。</param>
	public static void SetIsEnabled(DependencyObject obj, bool value)
	{
		obj.SetValue(IsEnabledProperty, value);
	}

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TreeView treeView)
			return;

		if ((bool)e.NewValue)
		{
			treeView.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
		}
		else
		{
			treeView.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;
		}
	}

	private static void OnPreviewMouseRightButtonDown(object sender, RoutedEventArgs e)
	{
		if (e.OriginalSource is not DependencyObject source)
			return;

		var treeViewItem = FindAncestorTreeViewItem(source);
		if (treeViewItem != null)
		{
			treeViewItem.IsSelected = true;
			treeViewItem.Focus();
		}
	}

	/// <summary>
	/// DependencyObject から祖先の TreeViewItem を検索します。
	/// </summary>
	/// <param name="obj">検索開始点。</param>
	/// <returns>見つかった TreeViewItem、または null。</returns>
	private static TreeViewItem? FindAncestorTreeViewItem(DependencyObject obj)
	{
		var current = obj;
		while (current != null)
		{
			if (current is TreeViewItem treeViewItem)
				return treeViewItem;

			current = VisualTreeHelper.GetParent(current);
		}

		return null;
	}
}
