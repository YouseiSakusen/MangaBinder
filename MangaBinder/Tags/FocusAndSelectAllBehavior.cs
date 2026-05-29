using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MangaBinder.Tags;

/// <summary>
/// バインドされた値が変化したとき、対象の <see cref="TextBox"/> にフォーカスを移し、テキストを全選択する添付ビヘイビアです。
/// </summary>
public static class FocusAndSelectAllBehavior
{
	/// <summary>
	/// フォーカス＆全選択の要求カウンタの添付プロパティです。
	/// 値が変化するたびに対象 TextBox へフォーカスし、テキストを全選択します。
	/// </summary>
	public static readonly DependencyProperty RequestProperty =
		DependencyProperty.RegisterAttached(
			"Request",
			typeof(int),
			typeof(FocusAndSelectAllBehavior),
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

	private static void OnRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TextBox textBox)
			return;

		// 初期値 (0) では発火させない
		if ((int)e.NewValue == 0)
			return;

		textBox.Dispatcher.BeginInvoke(
			System.Windows.Threading.DispatcherPriority.Input,
			new Action(() =>
			{
				textBox.Focus();
				Keyboard.Focus(textBox);
				textBox.SelectAll();
			}));
	}
}
