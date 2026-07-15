using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MangaBinder.Behaviors;

/// <summary>
/// TextBoxがフォーカスを受けた際に、入力済み文字列を自動的に全選択する添付ビヘイビアです。
/// キーボード操作およびマウスクリックの両方に対応します。
/// </summary>
public static class SelectAllOnFocusBehavior
{
	/// <summary>
	/// フォーカス時全選択機能を有効化するための添付プロパティです。
	/// </summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(SelectAllOnFocusBehavior),
			new PropertyMetadata(false, OnIsEnabledChanged));

	/// <summary>IsEnabled 添付プロパティの値を取得します。</summary>
	/// <param name="obj">値を取得する対象の <see cref="DependencyObject"/>。</param>
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
		if (d is not TextBox textBox)
			return;

		bool isEnabled = (bool)e.NewValue;

		// 既存のイベントハンドラーを解除
		textBox.GotFocus -= OnTextBoxGotFocus;
		textBox.PreviewMouseLeftButtonDown -= OnTextBoxPreviewMouseLeftButtonDown;

		if (isEnabled)
		{
			// イベントハンドラーを登録
			textBox.GotFocus += OnTextBoxGotFocus;
			textBox.PreviewMouseLeftButtonDown += OnTextBoxPreviewMouseLeftButtonDown;
		}
	}

	/// <summary>
	/// TextBox の GotFocus イベントハンドラーです。
	/// キーボードやAPIでフォーカスを受けた場合に全選択を行います。
	/// 入力処理完了後に全選択を実行するため、Dispatcher を利用した遅延実行を行います。
	/// </summary>
	private static void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
	{
		if (sender is not TextBox textBox)
			return;

		// 入力処理完了後に全選択を実行（マウスクリック時と同じ考え方）
		textBox.Dispatcher.BeginInvoke(
			System.Windows.Threading.DispatcherPriority.Input,
			new Action(textBox.SelectAll));
	}

	/// <summary>
	/// TextBox の PreviewMouseLeftButtonDown イベントハンドラーです。
	/// マウスクリックでフォーカスを受ける場合に全選択を実行します。
	/// </summary>
	private static void OnTextBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (sender is not TextBox textBox)
			return;

		// フォーカスを持っていない場合のみ全選択を行う
		if (!textBox.IsFocused)
		{
			textBox.Focus();

			// 入力処理完了後に全選択を実行
			textBox.Dispatcher.BeginInvoke(
				System.Windows.Threading.DispatcherPriority.Input,
				new Action(textBox.SelectAll));

			e.Handled = true;
		}
	}
}
