using System.Windows;
using MangaBinder.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程共通レイアウト Templated Custom Control。
/// 対象作品のタイトルと、製本工程の内容（通常は PageLayout など）を表示します。
/// </summary>
public class BindingPageLayout : PageLayout
{
	/// <summary>
	/// 製本工程の左ペインに挿入する、画面固有のコンテンツを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty AdditionalLeftContentProperty = DependencyProperty.Register(
		nameof(AdditionalLeftContent),
		typeof(UIElement),
		typeof(BindingPageLayout),
		new PropertyMetadata(null));

	static BindingPageLayout()
	{
		DefaultStyleKeyProperty.OverrideMetadata(
			typeof(BindingPageLayout),
			new FrameworkPropertyMetadata(typeof(BindingPageLayout)));
	}

	/// <summary>
	/// 製本工程の左ペインに挿入する、画面固有のコンテンツを取得または設定します。
	/// </summary>
	public UIElement? AdditionalLeftContent
	{
		get => (UIElement?)this.GetValue(AdditionalLeftContentProperty);
		set => this.SetValue(AdditionalLeftContentProperty, value);
	}
}
