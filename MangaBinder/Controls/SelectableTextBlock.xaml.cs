using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Controls;

/// <summary>
/// TextBlock のように見えるが、文字列を選択してコピーできる読み取り専用テキスト表示コントロール。
/// 内部は標準 WPF の TextBox を使用し、TextBlock と同じような見た目で実装されています。
/// </summary>
public partial class SelectableTextBlock : UserControl
{
	/// <summary>
	/// 表示するテキストを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
		nameof(Text),
		typeof(string),
		typeof(SelectableTextBlock),
		new PropertyMetadata(string.Empty));

	/// <summary>
	/// 内部 TextBox のテキストラッピングを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
		nameof(TextWrapping),
		typeof(TextWrapping),
		typeof(SelectableTextBlock),
		new PropertyMetadata(TextWrapping.NoWrap));

	/// <summary>
	/// 表示するテキストを取得または設定します。
	/// </summary>
	public string Text
	{
		get => (string)this.GetValue(TextProperty);
		set => this.SetValue(TextProperty, value);
	}

	/// <summary>
	/// 内部 TextBox のテキストラッピングを取得または設定します。
	/// </summary>
	public TextWrapping TextWrapping
	{
		get => (TextWrapping)this.GetValue(TextWrappingProperty);
		set => this.SetValue(TextWrappingProperty, value);
	}

	public SelectableTextBlock()
	{
		InitializeComponent();
	}
}
