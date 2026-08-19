using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Behaviors;

/// <summary>
/// TextBoxのあらすじ欄へ文字列を貼り付けた際に、Webサイト等から取得した文章を読みやすく自動整形する添付ビヘイビアです。
/// 文末記号（。！？!?―）の後で改行を挿入し、各行の先頭・末尾の空白を除去します。
/// </summary>
public static class DescriptionPasteFormattingBehavior
{
	/// <summary>
	/// あらすじペースト時の自動整形機能を有効化するための添付プロパティです。
	/// </summary>
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(bool),
			typeof(DescriptionPasteFormattingBehavior),
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
	/// クリップボードから貼り付けられた文字列を整形してからTextBoxへ挿入します。
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

		// 貼り付け文字列を整形
		string formattedText = FormatDescription(pastedText);

		// TextBoxの現在のカーソル位置を取得
		int selectionStart = textBox.SelectionStart;

		// SelectedTextを利用して通常のTextBox編集として貼り付け
		// 選択範囲がある場合は置き換え、ない場合はカーソル位置に挿入
		textBox.SelectedText = formattedText;

		// カーソル位置を貼り付け文字列の直後へ設定
		textBox.CaretIndex = selectionStart + formattedText.Length;

		// 選択範囲をクリア
		textBox.SelectionLength = 0;

		// 元のPaste処理をキャンセル
		e.CancelCommand();
	}

	/// <summary>
	/// あらすじ文字列を整形します。
	/// 文末記号の後で改行を挿入し、各行の先頭・末尾の空白を除去します。
	/// </summary>
	/// <param name="text">整形対象の文字列。</param>
	/// <returns>整形後の文字列。</returns>
	private static string FormatDescription(string text)
	{
		if (string.IsNullOrEmpty(text))
			return text;

		// 対象となる文末記号
		const string endMarks = "。！？!?―";
		const string closingMarks = "」』）】》〉";

		// 既存の改行（CRLF または LF）を基準に行を分割
		// 先に CRLF を LF に統一してから分割
		string normalized = text.Replace("\r\n", "\n");
		string[] lines = normalized.Split('\n');

		var formattedLines = new System.Collections.Generic.List<string>();

		foreach (string line in lines)
		{
			// 各行の先頭・末尾の空白を除去
			string trimmedLine = line.Trim();

			if (string.IsNullOrEmpty(trimmedLine))
			{
				// 空行はそのまま保持
				formattedLines.Add(string.Empty);
				continue;
			}

			// 行内で文末記号の後に改行を挿入
			string processedLine = InsertLineBreaksAtEndMarks(trimmedLine, endMarks, closingMarks);

			// 複数行に分割された場合、各行を formattedLines に追加
			string[] processedLines = processedLine.Split('\n');
			foreach (string processedSubLine in processedLines)
			{
				// 分割後の各行も Trim してから追加
				formattedLines.Add(processedSubLine.Trim());
			}
		}

		// 末尾の空行を除去（貼り付け文字列の末尾に不要な改行を作らない）
		while (formattedLines.Count > 0 && string.IsNullOrEmpty(formattedLines[formattedLines.Count - 1]))
		{
			formattedLines.RemoveAt(formattedLines.Count - 1);
		}

		// 改行で結合して返す
		return string.Join("\n", formattedLines);
	}

	/// <summary>
	/// 文末記号の後に改行を挿入します。
	/// 連続する文末記号は最後の1つの後でのみ改行し、
	/// 既に改行の場合や閉じ記号直前の場合は改行を挿入しません。
	/// </summary>
	/// <param name="line">処理対象の行。</param>
	/// <param name="endMarks">対象となる文末記号。</param>
	/// <param name="closingMarks">改行を挿入しない閉じ記号。</param>
	/// <returns>処理後の行（内部に改行を含む可能性あり）。</returns>
	private static string InsertLineBreaksAtEndMarks(string line, string endMarks, string closingMarks)
	{
		var result = new System.Text.StringBuilder();
		int i = 0;

		while (i < line.Length)
		{
			char currentChar = line[i];
			result.Append(currentChar);

			// 現在の文字が文末記号であるかチェック
			if (endMarks.Contains(currentChar))
			{
				// 連続する文末記号の終了位置を探す
				int endMarkEndIndex = i + 1;
				while (endMarkEndIndex < line.Length && endMarks.Contains(line[endMarkEndIndex]))
				{
					result.Append(line[endMarkEndIndex]);
					endMarkEndIndex++;
				}

				// endMarkEndIndex が行の末尾でない場合、さらに処理を続ける必要がある
				if (endMarkEndIndex < line.Length)
				{
					// 次の文字が閉じ記号であるかチェック
					char nextChar = line[endMarkEndIndex];
					if (!closingMarks.Contains(nextChar))
					{
						// 次の文字が閉じ記号でない場合は改行を挿入
						result.Append('\n');
					}
				}

				// ループカウンターを更新（連続した文末記号を飛ばす）
				i = endMarkEndIndex;
			}
			else
			{
				i++;
			}
		}

		return result.ToString();
	}
}
