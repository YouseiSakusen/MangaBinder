using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程共通レイアウト Templated Custom Control。
/// 対象作品のタイトルと、製本工程の内容（通常は PageLayout など）を表示します。
/// </summary>
public class BindingPageLayout : ContentControl
{
	/// <summary>
	/// 製本対象作品のタイトルを取得または設定します。
	/// </summary>
	public static readonly DependencyProperty SeriesTitleProperty = DependencyProperty.Register(
		nameof(SeriesTitle),
		typeof(string),
		typeof(BindingPageLayout),
		new PropertyMetadata(null));

	static BindingPageLayout()
	{
		DefaultStyleKeyProperty.OverrideMetadata(
			typeof(BindingPageLayout),
			new FrameworkPropertyMetadata(typeof(BindingPageLayout)));
	}

	/// <summary>
	/// 製本対象作品のタイトルを取得または設定します。
	/// </summary>
	public string SeriesTitle
	{
		get => (string)this.GetValue(SeriesTitleProperty);
		set => this.SetValue(SeriesTitleProperty, value);
	}
}
