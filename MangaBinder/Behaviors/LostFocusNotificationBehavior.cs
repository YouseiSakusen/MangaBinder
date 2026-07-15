using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MangaBinder.Behaviors;

/// <summary>
/// TextBoxがフォーカスを失った際に、指定されたコマンドを実行する添付ビヘイビアです。
/// </summary>
public static class LostFocusNotificationBehavior
{
	/// <summary>
	/// フォーカス喪失時に実行するコマンドの添付プロパティです。
	/// </summary>
	public static readonly DependencyProperty CommandProperty =
		DependencyProperty.RegisterAttached(
			"Command",
			typeof(ICommand),
			typeof(LostFocusNotificationBehavior),
			new PropertyMetadata(null, OnCommandChanged));

	/// <summary>Command 添付プロパティの値を取得します。</summary>
	/// <param name="obj">値を取得する対象の <see cref="DependencyObject"/>。</param>
	public static ICommand GetCommand(DependencyObject obj)
		=> (ICommand)obj.GetValue(CommandProperty);

	/// <summary>Command 添付プロパティの値を設定します。</summary>
	/// <param name="obj">値を設定する対象の <see cref="DependencyObject"/>。</param>
	/// <param name="value">設定する値。</param>
	public static void SetCommand(DependencyObject obj, ICommand value)
		=> obj.SetValue(CommandProperty, value);

	/// <summary>
	/// Command 添付プロパティが変更されたときに呼び出されます。
	/// </summary>
	private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TextBox textBox)
			return;

		// 既存のイベントハンドラーを解除
		textBox.LostFocus -= OnTextBoxLostFocus;

		if (e.NewValue is not null)
		{
			// イベントハンドラーを登録
			textBox.LostFocus += OnTextBoxLostFocus;
		}
	}

	/// <summary>
	/// TextBox の LostFocus イベントハンドラーです。
	/// フォーカス喪失時にコマンドを実行します。
	/// </summary>
	private static void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
	{
		if (sender is not TextBox textBox)
			return;

		var command = GetCommand(textBox);
		if (command != null && command.CanExecute(null))
		{
			command.Execute(null);
		}
	}
}
