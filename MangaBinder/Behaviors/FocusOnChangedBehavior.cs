using System.Windows;
using System.Windows.Input;

namespace MangaBinder.Behaviors;

/// <summary>
/// バインドされた値が変化したとき、対象の <see cref="UIElement"/> にフォーカスを移す添付ビヘイビアです。
/// </summary>
public static class FocusOnChangedBehavior
{
	/// <summary>
	/// フォーカス要求カウンタの添付プロパティです。
	/// 値が変化するたびに対象要素へ <see cref="UIElement.Focus"/> を呼び出します。
	/// </summary>
	public static readonly DependencyProperty RequestProperty =
		DependencyProperty.RegisterAttached(
			"Request",
			typeof(int),
			typeof(FocusOnChangedBehavior),
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
	/// 初期値以外で変化があった場合、対象要素へフォーカスを設定します。
	/// </summary>
	private static void OnRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not UIElement element)
			return;

		// 初期値 (0) では発火させない
		if ((int)e.NewValue == 0)
			return;

		// UI レンダリング完了後にフォーカスを設定するため、複数段階で遅延実行
		element.Dispatcher.BeginInvoke(
			System.Windows.Threading.DispatcherPriority.Render,
			new Action(() =>
			{
				element.Dispatcher.BeginInvoke(
					System.Windows.Threading.DispatcherPriority.Input,
					new Action(() =>
					{
						element.Focus();
						Keyboard.Focus(element);
					}));
			}));
	}
}
