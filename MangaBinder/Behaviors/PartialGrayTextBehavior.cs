using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MangaBinder.Behaviors;

/// <summary>
/// TextBlock のテキストの一部をグレー表示するための添付ビヘイビアです。
/// 元テキストから除外文字列と完全一致する部分を検索し、
/// その部分だけグレー色で表示する Run を生成して Inlines に配置します。
/// </summary>
public static class PartialGrayTextBehavior
{
	/// <summary>
	/// 元のテキストを指定するための添付プロパティです。
	/// </summary>
	public static readonly DependencyProperty TextProperty =
		DependencyProperty.RegisterAttached(
			"Text",
			typeof(string),
			typeof(PartialGrayTextBehavior),
			new PropertyMetadata(string.Empty, OnTextChanged));

	/// <summary>
	/// グレー表示する（除外する）文字列を指定するための添付プロパティです。
	/// </summary>
	public static readonly DependencyProperty GrayTextProperty =
		DependencyProperty.RegisterAttached(
			"GrayText",
			typeof(string),
			typeof(PartialGrayTextBehavior),
			new PropertyMetadata(string.Empty, OnTextChanged));

	/// <summary>Text 添付プロパティの値を取得します。</summary>
	public static string GetText(DependencyObject obj)
		=> (string)obj.GetValue(TextProperty) ?? string.Empty;

	/// <summary>Text 添付プロパティの値を設定します。</summary>
	public static void SetText(DependencyObject obj, string value)
		=> obj.SetValue(TextProperty, value ?? string.Empty);

	/// <summary>GrayText 添付プロパティの値を取得します。</summary>
	public static string GetGrayText(DependencyObject obj)
		=> (string)obj.GetValue(GrayTextProperty) ?? string.Empty;

	/// <summary>GrayText 添付プロパティの値を設定します。</summary>
	public static void SetGrayText(DependencyObject obj, string value)
		=> obj.SetValue(GrayTextProperty, value ?? string.Empty);

	/// <summary>
	/// Text または GrayText 添付プロパティが変更されたときに呼び出されます。
	/// </summary>
	private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TextBlock textBlock)
			return;

		string text = GetText(d);
		string grayText = GetGrayText(d);

		// Inlines を再構築
		textBlock.Inlines.Clear();

		if (string.IsNullOrEmpty(text))
			return;

		if (string.IsNullOrEmpty(grayText))
		{
			// グレー対象がない場合は通常色のみで表示
			textBlock.Inlines.Add(new Run(text));
			return;
		}

		// テキストをグレー対象の部分で分割して Inlines を構築
		BuildInlines(textBlock, text, grayText);
	}

	/// <summary>
	/// 元テキストを除外文字列で分割し、適切な Run を Inlines に追加します。
	/// </summary>
	private static void BuildInlines(TextBlock textBlock, string text, string grayText)
	{
		int currentIndex = 0;

		while (currentIndex < text.Length)
		{
			// StringComparison.Ordinal で次の除外文字列の位置を検索
			int grayIndex = text.IndexOf(grayText, currentIndex, StringComparison.Ordinal);

			if (grayIndex == -1)
			{
				// 残りのテキストは通常色で追加
				if (currentIndex < text.Length)
				{
					textBlock.Inlines.Add(new Run(text.Substring(currentIndex)));
				}
				break;
			}

			// グレー対象の前までを通常色で追加
			if (grayIndex > currentIndex)
			{
				textBlock.Inlines.Add(new Run(text.Substring(currentIndex, grayIndex - currentIndex)));
			}

			// グレー対象をグレー色で追加
			var grayRun = new Run(grayText)
			{
				Foreground = (Brush?)textBlock.FindResource(
					"TextFillColorSecondaryBrush") ?? Brushes.Gray,
			};
			textBlock.Inlines.Add(grayRun);

			currentIndex = grayIndex + grayText.Length;
		}
	}
}

