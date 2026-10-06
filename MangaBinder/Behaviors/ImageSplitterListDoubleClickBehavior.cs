using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MangaBinder.Behaviors;

/// <summary>
/// ImageSplitter の一覧 ListView で画像をダブルクリックした際に、
/// その画像を選択し、親 TabControl の「プレビュー」タブへ切り替える専用 Behavior です。
/// </summary>
public static class ImageSplitterListDoubleClickBehavior
{
	/// <summary>プレビュータブのインデックス。</summary>
	private const int PreviewTabIndex = 1;

	/// <summary>IsEnabled 添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(ImageSplitterListDoubleClickBehavior),
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
	/// IsEnabled 変更時に MouseDoubleClick の購読を登録・解除します。
	/// </summary>
	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not ListView listView)
		{
			return;
		}

		listView.MouseDoubleClick -= OnMouseDoubleClick;

		if (e.NewValue is true)
		{
			listView.MouseDoubleClick += OnMouseDoubleClick;
		}
	}

	/// <summary>
	/// ダブルクリックされた項目を選択し、プレビュータブへ切り替えます。CheckBox 上の操作は対象外です。
	/// </summary>
	private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		var listView = (ListView)sender;
		if (e.OriginalSource is not DependencyObject source)
		{
			return;
		}

		var container = findAncestor<ListViewItem>(source);
		if (container is null || findAncestor<ToggleButton>(source) is not null)
		{
			return;
		}

		listView.SelectedItem = container.DataContext;

		var tabControl = findAncestor<TabControl>(listView);
		if (tabControl is not null)
		{
			tabControl.SelectedIndex = PreviewTabIndex;
		}

		e.Handled = true;
	}

	/// <summary>
	/// ビジュアルツリーを辿り、指定型の最初の祖先を取得します。
	/// </summary>
	private static T? findAncestor<T>(DependencyObject start) where T : DependencyObject
	{
		var current = start;
		while (current is not null)
		{
			if (current is T match)
			{
				return match;
			}

			current = current is Visual or System.Windows.Media.Media3D.Visual3D
				? VisualTreeHelper.GetParent(current)
				: LogicalTreeHelper.GetParent(current);
		}

		return null;
	}
}
