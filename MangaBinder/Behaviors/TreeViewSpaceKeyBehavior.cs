using R3;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MangaBinder.Behaviors;

/// <summary>
/// TreeView の PreviewKeyDown を購読し、Space キーで指定されたコマンドを実行する添付ビヘイビアです。
/// 入力検出のみを担当し、ViewModel のコマンドに委譲します。
/// </summary>
public static class TreeViewSpaceKeyBehavior
{
	/// <summary>IsEnabled 添付プロパティです。</summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(TreeViewSpaceKeyBehavior),
			new PropertyMetadata(false, OnIsEnabledChanged));

	/// <summary>Command 添付プロパティです。</summary>
	public static readonly DependencyProperty CommandProperty =
		DependencyProperty.RegisterAttached(
			"Command",
			typeof(ICommand),
			typeof(TreeViewSpaceKeyBehavior),
			new PropertyMetadata(null));

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

	/// <summary>Command 添付プロパティの値を取得します。</summary>
	/// <param name="obj">値を取得する対象の <see cref="DependencyObject"/>。</param>
	/// <returns>現在のコマンド。</returns>
	public static ICommand? GetCommand(DependencyObject obj)
		=> (ICommand?)obj.GetValue(CommandProperty);

	/// <summary>Command 添付プロパティの値を設定します。</summary>
	/// <param name="obj">値を設定する対象の <see cref="DependencyObject"/>。</param>
	/// <param name="value">設定するコマンド。</param>
	public static void SetCommand(DependencyObject obj, ICommand? value)
		=> obj.SetValue(CommandProperty, value);

	/// <summary>
	/// IsEnabled 添付プロパティが変更されたときに呼び出されます。
	/// </summary>
	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TreeView treeView)
			return;

		if (e.OldValue is true)
			treeView.PreviewKeyDown -= OnPreviewKeyDown;

		if (e.NewValue is true)
			treeView.PreviewKeyDown += OnPreviewKeyDown;
	}

	/// <summary>
	/// Space キーが押下されたとき、指定されたコマンドを実行します。
	/// </summary>
	private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Space)
			return;

		if (sender is not TreeView treeView)
			return;

		var command = GetCommand(treeView);
		if (command == null || !command.CanExecute(null))
			return;

		e.Handled = true;
		command.Execute(null);
	}
}
