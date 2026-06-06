using MangaBinder.Binding;
using R3;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace MangaBinder.Behaviors;

/// <summary>
/// TreeView のノードをマウスクリックしてチェック状態を切り替える添付ビヘイビアです。
/// CheckBox 自体のクリックや展開アイコンのクリックは処理対象外とします。
/// </summary>
public static class TreeViewNodeClickToggleCheckBehavior
{
	/// <summary>IsEnabled 添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(TreeViewNodeClickToggleCheckBehavior),
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
	/// </summary>
	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TreeView treeView)
			return;

		if (e.OldValue is true)
			treeView.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;

		if (e.NewValue is true)
			treeView.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
	}

	/// <summary>
	/// マウス左ボタン クリック時に選択中ノードのチェック状態を切り替えます。
	/// CheckBox 自体のクリックや展開アイコンのクリックは処理対象外とします。
	/// </summary>
	private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (sender is not TreeView treeView)
			return;

		// クリック元の視覚要素を取得
		var clickedElement = e.OriginalSource as DependencyObject;
		if (clickedElement == null)
			return;

		// クリック元が CheckBox またはその子要素の場合は処理しない
		if (IsCheckBoxOrChild(clickedElement))
			return;

		// クリック元が展開/折りたたみアイコンの場合は処理しない
		if (IsExpanderToggleButton(clickedElement))
			return;

		// 選択中ノードを取得
		if (treeView.SelectedItem is not MaterialVolumeNode node)
			return;

		// チェック可能なノードのみ処理
		if (!node.CanCheck.Value)
			return;

		// チェック状態を切り替え
		node.ToggleCheckedCommand.Execute(Unit.Default);
		e.Handled = true;
	}

	/// <summary>
	/// 指定された要素が CheckBox またはその子要素かどうかを判定します。
	/// </summary>
	private static bool IsCheckBoxOrChild(DependencyObject element)
	{
		var current = element;
		while (current != null)
		{
			if (current is CheckBox)
				return true;

			current = VisualTreeHelper.GetParent(current);
		}

		return false;
	}

	/// <summary>
	/// 指定された要素が TreeViewItem の展開/折りたたみ用トグルボタンかどうかを判定します。
	/// </summary>
	private static bool IsExpanderToggleButton(DependencyObject element)
	{
		var current = element;
		while (current != null)
		{
			// TreeViewItem の子孫から遡って、ToggleButton を探す
			// ただし、TreeViewItem 自体には到達しない（より上位の要素になるため）
			if (current is ToggleButton && IsDescendantOfTreeViewItem(current))
				return true;

			current = VisualTreeHelper.GetParent(current);
			// TreeViewItem の親に到達したら止める
			if (current is TreeViewItem)
				break;
		}

		return false;
	}

	/// <summary>
	/// 指定された要素が TreeViewItem の子孫かどうかを判定します。
	/// </summary>
	private static bool IsDescendantOfTreeViewItem(DependencyObject element)
	{
		var current = VisualTreeHelper.GetParent(element);
		while (current != null)
		{
			if (current is TreeViewItem)
				return true;

			current = VisualTreeHelper.GetParent(current);
		}

		return false;
	}
}
