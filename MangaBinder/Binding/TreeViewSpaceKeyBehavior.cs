using R3;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MangaBinder.Binding;

/// <summary>
/// TreeView の PreviewKeyDown を購読し、Space キーで選択中ノードのチェックを切り替える添付ビヘイビアです。
/// </summary>
public static class TreeViewSpaceKeyBehavior
{
	/// <summary>IsEnabled 添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(TreeViewSpaceKeyBehavior),
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
			treeView.PreviewKeyDown -= OnPreviewKeyDown;

		if (e.NewValue is true)
			treeView.PreviewKeyDown += OnPreviewKeyDown;
	}

	/// <summary>
	/// Space キーが押下されたとき、選択中ノードの IsChecked を反転します。
	/// </summary>
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Space)
			return;

		e.Handled = true;

		if (sender is not TreeView treeView)
			return;

		if (treeView.SelectedItem is not MaterialVolumeNode node)
			return;

		node.ToggleCheckedCommand.Execute(Unit.Default);
	}
}
