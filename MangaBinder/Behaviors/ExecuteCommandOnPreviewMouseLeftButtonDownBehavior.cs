using System.Windows;
using System.Windows.Input;
using Reactive.Bindings;

namespace MangaBinder.Behaviors;

/// <summary>
/// <see cref="UIElement"/> にアタッチし、PreviewMouseLeftButtonDown イベント時に <see cref="ICommand"/> を実行する添付ビヘイビアです。
/// コードビハインドを使わずに、マウスの左ボタン押下時にコマンドを実行できます。
/// </summary>
public static class ExecuteCommandOnPreviewMouseLeftButtonDownBehavior
{
	/// <summary>PreviewMouseLeftButtonDown 時に実行するコマンドの添付プロパティです。</summary>
	public static readonly DependencyProperty CommandProperty =
		DependencyProperty.RegisterAttached(
			"Command",
			typeof(ICommand),
			typeof(ExecuteCommandOnPreviewMouseLeftButtonDownBehavior),
			new PropertyMetadata(null, OnCommandChanged));

	/// <summary><see cref="CommandProperty"/> の getter です。</summary>
	public static ICommand GetCommand(DependencyObject obj)
		=> (ICommand)obj.GetValue(CommandProperty);

	/// <summary><see cref="CommandProperty"/> の setter です。</summary>
	public static void SetCommand(DependencyObject obj, ICommand value)
		=> obj.SetValue(CommandProperty, value);

	private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not UIElement element)
			return;

		if (e.NewValue is not null)
			element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
		else
			element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
	}

	private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (sender is not UIElement element)
			return;

		var command = GetCommand(element);
		if (command is null)
			return;

		if (command.CanExecute(null))
		{
			command.Execute(null);
		}
	}
}
