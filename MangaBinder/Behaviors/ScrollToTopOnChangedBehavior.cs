using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Behaviors;

/// <summary>
/// バインドされた値が変化したとき、対象コントロールのスクロール位置を先頭へ戻す添付ビヘイビアです。
/// TextBox系とListViewに対応します。
/// </summary>
public static class ScrollToTopOnChangedBehavior
{
	/// <summary>
	/// スクロール位置リセットの要求値を保持する添付プロパティです。
	/// 値が変化するたびに対象コントロールのスクロール位置を先頭へ戻します。
	/// </summary>
	public static readonly DependencyProperty RequestProperty =
		DependencyProperty.RegisterAttached(
			"Request",
			typeof(int),
			typeof(ScrollToTopOnChangedBehavior),
			new PropertyMetadata(0, OnRequestChanged));

	/// <summary>Request 添付プロパティの値を取得します。</summary>
	/// <param name="obj">値を取得する対象の <see cref="DependencyObject"/>。</param>
	public static int GetRequest(DependencyObject obj)
		=> (int)obj.GetValue(RequestProperty);

	/// <summary>Request 添付プロパティの値を設定します。</summary>
	/// <param name="obj">値を設定する対象の <see cref="DependencyObject"/>。</param>
	/// <param name="value">設定する値。</param>
	public static void SetRequest(DependencyObject obj, int value)
		=> obj.SetValue(RequestProperty, value);

	/// <summary>
	/// Request 添付プロパティが変更されたときに呼び出されます。
	/// </summary>
	private static void OnRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		// TextBox系に対応
		if (d is TextBox textBox)
		{
			textBox.Dispatcher.BeginInvoke(
				System.Windows.Threading.DispatcherPriority.Loaded,
				new Action(() =>
				{
					textBox.ScrollToHome();
				}));
			return;
		}

		// ListViewに対応
		if (d is ListView listView)
		{
			listView.Dispatcher.BeginInvoke(
				System.Windows.Threading.DispatcherPriority.Loaded,
				new Action(() =>
				{
					// Itemsが存在する場合、先頭Itemまでスクロール
					if (listView.Items.Count > 0)
					{
						listView.ScrollIntoView(listView.Items[0]);
					}
				}));
			return;
		}
	}
}

