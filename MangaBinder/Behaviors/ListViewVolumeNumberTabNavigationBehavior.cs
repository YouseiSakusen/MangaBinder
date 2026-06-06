using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MangaBinder.Behaviors;

/// <summary>
/// ListView の DataTemplate 内にある巻番号 TextBox に対して、
/// Tab / Shift+Tab キーで行間を移動する添付ビヘイビアです。
/// </summary>
public static class ListViewVolumeNumberTabNavigationBehavior
{
	/// <summary>IsEnabled 添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(ListViewVolumeNumberTabNavigationBehavior),
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
		if (d is not ListView listView)
			return;

		if (e.OldValue is true)
			listView.PreviewKeyDown -= OnPreviewKeyDown;

		if (e.NewValue is true)
			listView.PreviewKeyDown += OnPreviewKeyDown;
	}

	/// <summary>
	/// Tab / Shift+Tab が押下されたとき、ListViewItem 内の次/前の巻番号 TextBox へフォーカスを移動します。
	/// </summary>
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Tab)
			return;

		var listView = sender as ListView;
		if (listView == null)
			return;

		// 現在フォーカスを持っているコントロールを取得
		var focusedElement = Keyboard.FocusedElement as DependencyObject;
		if (focusedElement == null)
			return;

		// フォーカスされた TextBox を探す（DataTemplate 内なのでビジュアルツリーを上へ遡る）
		var currentTextBox = focusedElement as TextBox;
		if (currentTextBox == null)
			return;

		// 現在の TextBox がある ListViewItem を探す
		var currentItem = GetContainingListViewItem(currentTextBox);
		if (currentItem == null)
			return;

		// 現在の TextBox の値をバインディングに反映（UpdateSourceTrigger=LostFocus のため）
		var binding = currentTextBox.GetBindingExpression(TextBox.TextProperty);
		binding?.UpdateSource();

		// 次または前の ListViewItem を取得
		var itemIndex = listView.Items.IndexOf(currentItem.Content);
		if (itemIndex < 0)
			return;

		var nextIndex = Keyboard.Modifiers == ModifierKeys.Shift
			? itemIndex - 1
			: itemIndex + 1;

		// 次のアイテムのインデックスが有効範囲内かチェック
		if (nextIndex < 0 || nextIndex >= listView.Items.Count)
			return;

		// 次のアイテムの TextBox にフォーカスを移動
		var nextItem = listView.ItemContainerGenerator.ContainerFromIndex(nextIndex) as ListViewItem;
		if (nextItem == null)
			return;

		var nextTextBox = FindVolumeNumberTextBox(nextItem);
		if (nextTextBox != null)
		{
			e.Handled = true;
			nextTextBox.Focus();
			nextTextBox.SelectAll();
		}
	}

	/// <summary>
	/// TextBox を含む ListViewItem を探します。
	/// </summary>
	private static ListViewItem? GetContainingListViewItem(DependencyObject element)
	{
		var current = element;
		while (current != null)
		{
			if (current is ListViewItem item)
				return item;

			current = System.Windows.Media.VisualTreeHelper.GetParent(current);
		}

		return null;
	}

	/// <summary>
	/// ListViewItem 内から巻番号 TextBox を探します。
	/// DataTemplate では Grid.Column="0" の TextBox が巻番号入力欄です。
	/// </summary>
	private static TextBox? FindVolumeNumberTextBox(ListViewItem item)
	{
		// ListViewItem の ContentPresenter から Grid（DataTemplate ルート）を取得
		var contentPresenter = GetVisualChild<ContentPresenter>(item);
		if (contentPresenter == null)
			return null;

		// ContentPresenter 内の Grid（行テンプレート）を取得
		var grid = GetVisualChild<Grid>(contentPresenter);
		if (grid == null)
			return null;

		// Grid 内から Column 0 に位置する TextBox を取得
		foreach (var child in grid.Children)
		{
			if (child is TextBox textBox && Grid.GetColumn(textBox) == 0)
				return textBox;
		}

		return null;
	}

	/// <summary>
	/// ビジュアルツリーから指定された型の最初の子要素を取得します。
	/// </summary>
	private static T? GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
	{
		if (parent == null)
			return null;

		for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
			if (child is T typedChild)
				return typedChild;

			var result = GetVisualChild<T>(child);
			if (result != null)
				return result;
		}

		return null;
	}
}
