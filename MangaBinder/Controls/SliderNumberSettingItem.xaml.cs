using System.Windows;
using System.Windows.Controls;

namespace MangaBinder.Controls;

/// <summary>
/// Slider と NumberBox で整数値を設定する、設定項目1件分の共通コントロールです。
/// </summary>
public partial class SliderNumberSettingItem : UserControl
{
	public static readonly DependencyProperty LabelProperty =
		DependencyProperty.Register(nameof(Label), typeof(string), typeof(SliderNumberSettingItem), new PropertyMetadata(string.Empty));

	public static readonly DependencyProperty ValueProperty =
		DependencyProperty.Register(
			nameof(Value),
			typeof(int),
			typeof(SliderNumberSettingItem),
			new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	public static readonly DependencyProperty MinimumProperty =
		DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderNumberSettingItem), new PropertyMetadata(0d));

	public static readonly DependencyProperty MaximumProperty =
		DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderNumberSettingItem), new PropertyMetadata(100d));

	public static readonly DependencyProperty UnitProperty =
		DependencyProperty.Register(nameof(Unit), typeof(string), typeof(SliderNumberSettingItem), new PropertyMetadata("px"));

	public static readonly DependencyProperty LabelWidthProperty =
		DependencyProperty.Register(nameof(LabelWidth), typeof(GridLength), typeof(SliderNumberSettingItem), new PropertyMetadata(new GridLength(72)));

	public static readonly DependencyProperty NumberBoxWidthProperty =
		DependencyProperty.Register(nameof(NumberBoxWidth), typeof(GridLength), typeof(SliderNumberSettingItem), new PropertyMetadata(new GridLength(110)));

	/// <summary>項目名を取得または設定します。</summary>
	public string Label
	{
		get => (string)GetValue(LabelProperty);
		set => SetValue(LabelProperty, value);
	}

	/// <summary>設定値を取得または設定します。</summary>
	public int Value
	{
		get => (int)GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	/// <summary>最小値を取得または設定します。</summary>
	public double Minimum
	{
		get => (double)GetValue(MinimumProperty);
		set => SetValue(MinimumProperty, value);
	}

	/// <summary>最大値を取得または設定します。</summary>
	public double Maximum
	{
		get => (double)GetValue(MaximumProperty);
		set => SetValue(MaximumProperty, value);
	}

	/// <summary>単位表示を取得または設定します。</summary>
	public string Unit
	{
		get => (string)GetValue(UnitProperty);
		set => SetValue(UnitProperty, value);
	}

	/// <summary>ラベル列の幅を取得または設定します。</summary>
	public GridLength LabelWidth
	{
		get => (GridLength)GetValue(LabelWidthProperty);
		set => SetValue(LabelWidthProperty, value);
	}

	/// <summary>NumberBox列の幅を取得または設定します。</summary>
	public GridLength NumberBoxWidth
	{
		get => (GridLength)GetValue(NumberBoxWidthProperty);
		set => SetValue(NumberBoxWidthProperty, value);
	}

	public SliderNumberSettingItem()
	{
		InitializeComponent();
	}
}
