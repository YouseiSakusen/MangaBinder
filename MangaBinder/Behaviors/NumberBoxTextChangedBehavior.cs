using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace MangaBinder.Behaviors;

/// <summary>
/// <see cref="NumberBox"/> の TextChanged イベントを監視し、テキスト変更時にコマンドを実行する添付ビヘイビアです。
/// </summary>
public static class NumberBoxTextChangedBehavior
{
	/// <summary>TextChanged イベント発生時に実行する Command の添付プロパティです。</summary>
	public static readonly DependencyProperty CommandProperty =
		DependencyProperty.RegisterAttached(
			"Command",
			typeof(ICommand),
			typeof(NumberBoxTextChangedBehavior),
			new PropertyMetadata(null, OnCommandChanged));

	/// <summary><see cref="CommandProperty"/> の getter です。</summary>
	public static ICommand GetCommand(DependencyObject obj)
		=> (ICommand)obj.GetValue(CommandProperty);

	/// <summary><see cref="CommandProperty"/> の setter です。</summary>
	public static void SetCommand(DependencyObject obj, ICommand value)
		=> obj.SetValue(CommandProperty, value);

	private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not NumberBox numberBox)
			return;

		if (e.NewValue is not null)
		{
			numberBox.TextChanged += OnNumberBoxTextChanged;
		}
		else
		{
			numberBox.TextChanged -= OnNumberBoxTextChanged;
		}
	}

	private static void OnNumberBoxTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
	{
		if (sender is not NumberBox numberBox)
			return;

		var command = GetCommand(numberBox);
		if (command?.CanExecute(numberBox.Text) ?? false)
		{
			command.Execute(numberBox.Text);
		}
	}
}
