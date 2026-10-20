using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Media;

namespace HalationGhost.Wpf.Ui.Controls;

/// <summary>
/// 表示幅に合わせて文字列を省略表示するTextBlockです。
/// 表示元の文字列は <see cref="SourceText"/> に設定します。
/// </summary>
public class ElfEllipsisTextBlock : TextBlock
{
	private const string ellipsis = "…";

	/// <summary>
	/// <see cref="SourceText"/> 依存関係プロパティ。
	/// </summary>
	public static readonly DependencyProperty SourceTextProperty = DependencyProperty.Register(
		nameof(SourceText),
		typeof(string),
		typeof(ElfEllipsisTextBlock),
		new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

	/// <summary>
	/// <see cref="EllipsisMode"/> 依存関係プロパティ。
	/// </summary>
	public static readonly DependencyProperty EllipsisModeProperty = DependencyProperty.Register(
		nameof(EllipsisMode),
		typeof(EllipsisMode),
		typeof(ElfEllipsisTextBlock),
		new PropertyMetadata(EllipsisMode.End, OnDisplayPropertyChanged));

	/// <summary>
	/// 表示元の文字列を取得または設定します。
	/// </summary>
	public string SourceText
	{
		get => (string)this.GetValue(SourceTextProperty);
		set => this.SetValue(SourceTextProperty, value);
	}

	/// <summary>
	/// 省略方向を取得または設定します。
	/// </summary>
	public EllipsisMode EllipsisMode
	{
		get => (EllipsisMode)this.GetValue(EllipsisModeProperty);
		set => this.SetValue(EllipsisModeProperty, value);
	}

	/// <summary>
	/// <see cref="ElfEllipsisTextBlock"/> の新しいインスタンスを初期化します。
	/// </summary>
	public ElfEllipsisTextBlock()
	{
		this.TextWrapping = TextWrapping.NoWrap;
		this.TextTrimming = TextTrimming.None;
		this.ClipToBounds = true;

		// 利用側がToolTipを明示設定した場合は、そちらが優先される
		this.SetBinding(ToolTipProperty, new Binding(nameof(this.SourceText)) { Source = this });

		this.SizeChanged += this.onSizeChanged;
	}

	/// <summary>
	/// 表示に影響するプロパティ変更時に再計算します。
	/// </summary>
	private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		=> ((ElfEllipsisTextBlock)d).updateText();

	/// <summary>
	/// 文字幅に影響するフォント設定の変更時に再計算するようメタデータを上書きします。
	/// </summary>
	static ElfEllipsisTextBlock()
	{
		var changed = new PropertyChangedCallback(OnDisplayPropertyChanged);
		FontFamilyProperty.OverrideMetadata(typeof(ElfEllipsisTextBlock), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.Inherits, changed));
		FontSizeProperty.OverrideMetadata(typeof(ElfEllipsisTextBlock), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, changed));
		FontStyleProperty.OverrideMetadata(typeof(ElfEllipsisTextBlock), new FrameworkPropertyMetadata(SystemFonts.MessageFontStyle, FrameworkPropertyMetadataOptions.Inherits, changed));
		FontWeightProperty.OverrideMetadata(typeof(ElfEllipsisTextBlock), new FrameworkPropertyMetadata(SystemFonts.MessageFontWeight, FrameworkPropertyMetadataOptions.Inherits, changed));
		FontStretchProperty.OverrideMetadata(typeof(ElfEllipsisTextBlock), new FrameworkPropertyMetadata(FontStretches.Normal, FrameworkPropertyMetadataOptions.Inherits, changed));
		PaddingProperty.OverrideMetadata(typeof(ElfEllipsisTextBlock), new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsMeasure, changed));
	}

	/// <summary>
	/// DPI変更時に再計算します。
	/// </summary>
	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
	{
		base.OnDpiChanged(oldDpi, newDpi);
		this.updateText();
	}

	/// <summary>
	/// 表示幅が変更された場合に再計算します。
	/// </summary>
	private void onSizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (e.WidthChanged)
		{
			this.updateText();
		}
	}

	/// <summary>
	/// 現在の幅とフォント設定から表示文字列を算出し、<see cref="TextBlock.Text"/> へ設定します。
	/// </summary>
	private void updateText()
	{
		var source = this.SourceText ?? string.Empty;

		var width = double.IsNaN(this.Width) ? this.ActualWidth : this.Width;
		var available = width - this.Padding.Left - this.Padding.Right;

		var result = width > 0
			? this.buildText(source, available)
			: source;

		if (this.Text != result)
		{
			this.SetCurrentValue(TextProperty, result);
		}
	}

	/// <summary>
	/// 指定幅に収まる表示文字列を生成します。
	/// </summary>
	private string buildText(string source, double available)
	{
		if (source.Length == 0 || this.measure(source) <= available)
		{
			return source;
		}

		if (available <= 0 || this.measure(ellipsis) > available)
		{
			return string.Empty;
		}

		// 収まる最大の残し文字数を二分探索する
		var low = 0;
		var high = source.Length - 1;
		while (low < high)
		{
			var mid = (low + high + 1) / 2;
			if (this.measure(this.compose(source, mid)) <= available)
			{
				low = mid;
			}
			else
			{
				high = mid - 1;
			}
		}

		return this.compose(source, low);
	}

	/// <summary>
	/// 残す文字数を指定して省略後の文字列を組み立てます。
	/// </summary>
	private string compose(string source, int keep)
	{
		switch (this.EllipsisMode)
		{
			case EllipsisMode.Start:
				return ellipsis + source.Substring(source.Length - keep);
			case EllipsisMode.Middle:
				var head = (keep + 1) / 2;
				var tail = keep / 2;
				return source.Substring(0, head) + ellipsis + source.Substring(source.Length - tail);
			default:
				return source.Substring(0, keep) + ellipsis;
		}
	}

	/// <summary>
	/// 現在のフォント設定とDPIで文字列の描画幅を計測します。
	/// </summary>
	private double measure(string text)
	{
		var typeface = new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch);
		var formatted = new FormattedText(
			text,
			CultureInfo.CurrentUICulture,
			this.FlowDirection,
			typeface,
			this.FontSize,
			Brushes.Black,
			VisualTreeHelper.GetDpi(this).PixelsPerDip);

		return formatted.WidthIncludingTrailingWhitespace;
	}
}
