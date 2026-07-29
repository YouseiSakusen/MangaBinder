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

	/// <summary>
	/// <see cref="CommandProperty"/> が変更された時に呼び出されます。
	/// イベント購読を再設定し、新しいコマンドが null でない場合のみ TextChanged イベントを購読します。
	/// </summary>
	/// <param name="d">DependencyObject。</param>
	/// <param name="e">プロパティ変更情報。</param>
	private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not NumberBox numberBox)
			return;

		// まず必ずイベントを解除
		numberBox.TextChanged -= OnNumberBoxTextChanged;

		// 新しいCommandがnullでない場合のみ再登録
		if (e.NewValue is not null)
		{
			numberBox.TextChanged += OnNumberBoxTextChanged;
		}
	}

	/// <summary>
	/// NumberBox の TextChanged イベントハンドラーです。
	/// ユーザーがキーボードでテキスト入力している場合のみコマンドを実行します。
	/// DataContext の変更や Binding 更新など、ユーザー操作以外による TextChanged では処理をスキップします。
	/// </summary>
	/// <param name="sender">イベント発行元の NumberBox。</param>
	/// <param name="e">TextChanged イベント引数。</param>
	private static void OnNumberBoxTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
	{
		if (sender is not NumberBox numberBox)
			return;

		// ユーザー入力以外による TextChanged を除外
		if (!numberBox.IsKeyboardFocusWithin)
			return;

		var command = GetCommand(numberBox);
		if (command?.CanExecute(numberBox.Text) ?? false)
		{
			command.Execute(numberBox.Text);
		}
	}
}
