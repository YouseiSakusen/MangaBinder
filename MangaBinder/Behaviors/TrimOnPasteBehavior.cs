using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Behaviors;

/// <summary>
/// TextBoxへの文字列貼り付けの際に、貼り付け文字列の先頭・末尾の空白を自動的に除去する添付ビヘイビアです。
/// クリップボードから貼り付けられた文字列に対してのみTrimを適用し、TextBoxに既に入力されている文字列全体へのTrimは行いません。
/// </summary>
public static class TrimOnPasteBehavior
{
	/// <summary>
	/// 貼り付け時の自動Trim機能を有効化するための添付プロパティです。
	/// </summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(TrimOnPasteBehavior),
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
		DataObject.RemovePastingHandler(textBox, OnPasting);

		if (isEnabled)
		{
			// イベントハンドラーを登録
			DataObject.AddPastingHandler(textBox, OnPasting);
		}
	}

	/// <summary>
	/// DataObject.Pasting イベントハンドラーです。
	/// クリップボードから貼り付けられた文字列に対してTrimを適用してからTextBoxへ挿入します。
	/// </summary>
	private static void OnPasting(object sender, DataObjectPastingEventArgs e)
	{
		if (sender is not TextBox textBox)
			return;

		// クリップボードからテキストデータを取得
		if (!e.DataObject.GetDataPresent(typeof(string)))
			return;

		string pastedText = (string)e.DataObject.GetData(typeof(string));
		if (pastedText == null)
			return;

		// 貼り付け文字列をTrim
		string trimmedText = pastedText.Trim();

		// TextBoxの現在のカーソル位置を取得
		int selectionStart = textBox.SelectionStart;

		// SelectedTextを利用して通常のTextBox編集として貼り付け
		// 選択範囲がある場合は置き換え、ない場合はカーソル位置に挿入
		textBox.SelectedText = trimmedText;

		// カーソル位置を貼り付け文字列の直後へ設定
		textBox.CaretIndex = selectionStart + trimmedText.Length;

		// 選択範囲をクリア
		textBox.SelectionLength = 0;

		// 元のPaste処理をキャンセル
		e.CancelCommand();
	}
}
