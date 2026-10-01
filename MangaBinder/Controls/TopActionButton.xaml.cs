using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace MangaBinder.Controls;

/// <summary>
/// 画面上部に配置する共通アクションボタンコントロール。
/// アイコン、主操作名、操作に関連する現在値・状態をコンパクトに表示します。
/// </summary>
public partial class TopActionButton : UserControl
{
	/// <summary>
	/// アイコンの Symbol を取得または設定します。
	/// </summary>
	public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
		nameof(Symbol),
		typeof(SymbolRegular),
		typeof(TopActionButton),
		new PropertyMetadata(SymbolRegular.Box24));

	/// <summary>
	/// 主操作名を取得または設定します。
	/// </summary>
	public static readonly DependencyProperty PrimaryTextProperty = DependencyProperty.Register(
		nameof(PrimaryText),
		typeof(string),
		typeof(TopActionButton),
		new PropertyMetadata(string.Empty));

	/// <summary>
	/// セカンダリ行のラベルを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty SecondaryLabelProperty = DependencyProperty.Register(
		nameof(SecondaryLabel),
		typeof(string),
		typeof(TopActionButton),
		new PropertyMetadata(string.Empty));

	/// <summary>
	/// セカンダリ行の値を取得または設定します。
	/// </summary>
	public static readonly DependencyProperty SecondaryValueProperty = DependencyProperty.Register(
		nameof(SecondaryValue),
		typeof(string),
		typeof(TopActionButton),
		new PropertyMetadata(string.Empty));

	/// <summary>
	/// SecondaryLabel と SecondaryValue の間にセパレータ（：）を表示するかどうかを取得または設定します。既定値は true。
	/// </summary>
	public static readonly DependencyProperty ShowValueSeparatorProperty = DependencyProperty.Register(
		nameof(ShowValueSeparator),
		typeof(bool),
		typeof(TopActionButton),
		new PropertyMetadata(true));

	/// <summary>
	/// ボタンクリック時に実行するコマンドを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
		nameof(Command),
		typeof(ICommand),
		typeof(TopActionButton),
		new PropertyMetadata(null));

	/// <summary>
	/// アイコンの Symbol を取得または設定します。
	/// </summary>
	public SymbolRegular Symbol
	{
		get => (SymbolRegular)this.GetValue(SymbolProperty);
		set => this.SetValue(SymbolProperty, value);
	}

	/// <summary>
	/// 主操作名を取得または設定します。
	/// </summary>
	public string PrimaryText
	{
		get => (string)this.GetValue(PrimaryTextProperty);
		set => this.SetValue(PrimaryTextProperty, value);
	}

	/// <summary>
	/// セカンダリ行のラベルを取得または設定します。
	/// </summary>
	public string SecondaryLabel
	{
		get => (string)this.GetValue(SecondaryLabelProperty);
		set => this.SetValue(SecondaryLabelProperty, value);
	}

	/// <summary>
	/// セカンダリ行の値を取得または設定します。
	/// </summary>
	public string SecondaryValue
	{
		get => (string)this.GetValue(SecondaryValueProperty);
		set => this.SetValue(SecondaryValueProperty, value);
	}

	/// <summary>
	/// SecondaryLabel と SecondaryValue の間にセパレータ（：）を表示するかどうかを取得または設定します。
	/// </summary>
	public bool ShowValueSeparator
	{
		get => (bool)this.GetValue(ShowValueSeparatorProperty);
		set => this.SetValue(ShowValueSeparatorProperty, value);
	}

	/// <summary>
	/// ボタンクリック時に実行するコマンドを取得または設定します。
	/// </summary>
	public ICommand Command
	{
		get => (ICommand)this.GetValue(CommandProperty);
		set => this.SetValue(CommandProperty, value);
	}

	public TopActionButton()
	{
		InitializeComponent();
	}
}
