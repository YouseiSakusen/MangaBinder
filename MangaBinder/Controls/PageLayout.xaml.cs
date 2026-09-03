using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace MangaBinder.Controls;

/// <summary>
/// MangaBinder の標準ページレイアウト UserControl。
/// 3カラム（左・中央・右）× 3エリア（上部・本文・フッタ）の構成で、各エリアに任意のコンテンツを配置可能。
/// </summary>
public partial class PageLayout : UserControl
{
	/// <summary>
	/// 左カラムの幅を取得または設定します。既定値は 250。
	/// </summary>
	public static readonly DependencyProperty LeftColumnWidthProperty = DependencyProperty.Register(
		nameof(LeftColumnWidth),
		typeof(GridLength),
		typeof(PageLayout),
		new PropertyMetadata(new GridLength(250)));

	/// <summary>
	/// 左カラムの最小幅を取得または設定します。既定値は 250。
	/// </summary>
	public static readonly DependencyProperty LeftColumnMinWidthProperty = DependencyProperty.Register(
		nameof(LeftColumnMinWidth),
		typeof(double),
		typeof(PageLayout),
		new PropertyMetadata(250d));

	/// <summary>
	/// 右カラムの幅を取得または設定します。既定値は 250。
	/// </summary>
	public static readonly DependencyProperty RightColumnWidthProperty = DependencyProperty.Register(
		nameof(RightColumnWidth),
		typeof(GridLength),
		typeof(PageLayout),
		new PropertyMetadata(new GridLength(250)));

	/// <summary>
	/// 右カラムの最小幅を取得または設定します。既定値は 250。
	/// </summary>
	public static readonly DependencyProperty RightColumnMinWidthProperty = DependencyProperty.Register(
		nameof(RightColumnMinWidth),
		typeof(double),
		typeof(PageLayout),
		new PropertyMetadata(250d));

	/// <summary>
	/// 左カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(
		nameof(LeftContent),
		typeof(UIElement),
		typeof(PageLayout),
		new PropertyMetadata(null));

	/// <summary>
	/// 中央上部の追加操作領域に配置するコンテンツを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty TopContentProperty = DependencyProperty.Register(
		nameof(TopContent),
		typeof(UIElement),
		typeof(PageLayout),
		new PropertyMetadata(null));

	/// <summary>
	/// アイキャッチカードの Symbol を取得または設定します。
	/// Wpf.Ui SymbolIcon.Symbol に指定できるシンボルを指定します。
	/// </summary>
	public static readonly DependencyProperty EyecatchSymbolProperty = DependencyProperty.Register(
		nameof(EyecatchSymbol),
		typeof(SymbolRegular),
		typeof(PageLayout),
		new PropertyMetadata(SymbolRegular.Box24));

	/// <summary>
	/// アイキャッチカードの見出しテキストを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty EyecatchTitleProperty = DependencyProperty.Register(
		nameof(EyecatchTitle),
		typeof(string),
		typeof(PageLayout),
		new PropertyMetadata(string.Empty));

	/// <summary>
	/// アイキャッチカードの表示値テキストを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty EyecatchValueTextProperty = DependencyProperty.Register(
		nameof(EyecatchValueText),
		typeof(string),
		typeof(PageLayout),
		new PropertyMetadata(string.Empty));

	/// <summary>
	/// 中央カラムの本文領域に配置するメインコンテンツを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty MainContentProperty = DependencyProperty.Register(
		nameof(MainContent),
		typeof(UIElement),
		typeof(PageLayout),
		new PropertyMetadata(null));

	/// <summary>
	/// 右カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(
		nameof(RightContent),
		typeof(UIElement),
		typeof(PageLayout),
		new PropertyMetadata(null));

	/// <summary>
	/// フッタの左カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty FooterLeftContentProperty = DependencyProperty.Register(
		nameof(FooterLeftContent),
		typeof(UIElement),
		typeof(PageLayout),
		new PropertyMetadata(null));

	/// <summary>
	/// フッタの中央カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty FooterContentProperty = DependencyProperty.Register(
		nameof(FooterContent),
		typeof(UIElement),
		typeof(PageLayout),
		new PropertyMetadata(null));

	/// <summary>
	/// フッタの右カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty FooterRightContentProperty = DependencyProperty.Register(
		nameof(FooterRightContent),
		typeof(UIElement),
		typeof(PageLayout),
		new PropertyMetadata(null));

	/// <summary>
	/// 左カラムの幅を取得または設定します。
	/// </summary>
	public GridLength LeftColumnWidth
	{
		get => (GridLength)this.GetValue(LeftColumnWidthProperty);
		set => this.SetValue(LeftColumnWidthProperty, value);
	}

	/// <summary>
	/// 左カラムの最小幅を取得または設定します。
	/// </summary>
	public double LeftColumnMinWidth
	{
		get => (double)this.GetValue(LeftColumnMinWidthProperty);
		set => this.SetValue(LeftColumnMinWidthProperty, value);
	}

	/// <summary>
	/// 右カラムの幅を取得または設定します。
	/// </summary>
	public GridLength RightColumnWidth
	{
		get => (GridLength)this.GetValue(RightColumnWidthProperty);
		set => this.SetValue(RightColumnWidthProperty, value);
	}

	/// <summary>
	/// 右カラムの最小幅を取得または設定します。
	/// </summary>
	public double RightColumnMinWidth
	{
		get => (double)this.GetValue(RightColumnMinWidthProperty);
		set => this.SetValue(RightColumnMinWidthProperty, value);
	}

	/// <summary>
	/// 左カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public UIElement LeftContent
	{
		get => (UIElement)this.GetValue(LeftContentProperty);
		set => this.SetValue(LeftContentProperty, value);
	}

	/// <summary>
	/// 中央上部の追加操作領域に配置するコンテンツを取得または設定します。
	/// </summary>
	public UIElement TopContent
	{
		get => (UIElement)this.GetValue(TopContentProperty);
		set => this.SetValue(TopContentProperty, value);
	}

	/// <summary>
	/// アイキャッチカードの Symbol を取得または設定します。
	/// </summary>
	public SymbolRegular EyecatchSymbol
	{
		get => (SymbolRegular)this.GetValue(EyecatchSymbolProperty);
		set => this.SetValue(EyecatchSymbolProperty, value);
	}

	/// <summary>
	/// アイキャッチカードの見出しテキストを取得または設定します。
	/// </summary>
	public string EyecatchTitle
	{
		get => (string)this.GetValue(EyecatchTitleProperty);
		set => this.SetValue(EyecatchTitleProperty, value);
	}

	/// <summary>
	/// アイキャッチカードの表示値テキストを取得または設定します。
	/// </summary>
	public string EyecatchValueText
	{
		get => (string)this.GetValue(EyecatchValueTextProperty);
		set => this.SetValue(EyecatchValueTextProperty, value);
	}

	/// <summary>
	/// 中央カラムの本文領域に配置するメインコンテンツを取得または設定します。
	/// </summary>
	public UIElement MainContent
	{
		get => (UIElement)this.GetValue(MainContentProperty);
		set => this.SetValue(MainContentProperty, value);
	}

	/// <summary>
	/// 右カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public UIElement RightContent
	{
		get => (UIElement)this.GetValue(RightContentProperty);
		set => this.SetValue(RightContentProperty, value);
	}

	/// <summary>
	/// フッタの左カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public UIElement FooterLeftContent
	{
		get => (UIElement)this.GetValue(FooterLeftContentProperty);
		set => this.SetValue(FooterLeftContentProperty, value);
	}

	/// <summary>
	/// フッタの中央カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public UIElement FooterContent
	{
		get => (UIElement)this.GetValue(FooterContentProperty);
		set => this.SetValue(FooterContentProperty, value);
	}

	/// <summary>
	/// フッタの右カラムに配置するコンテンツを取得または設定します。
	/// </summary>
	public UIElement FooterRightContent
	{
		get => (UIElement)this.GetValue(FooterRightContentProperty);
		set => this.SetValue(FooterRightContentProperty, value);
	}

	public PageLayout()
	{
		InitializeComponent();
	}
}
