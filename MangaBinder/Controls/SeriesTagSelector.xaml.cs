using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MangaBinder.Controls;

/// <summary>
/// SeriesTagSelector.xaml の相互作用ロジック
/// </summary>
public partial class SeriesTagSelector : UserControl
{
	/// <summary>
	/// ItemsSource DependencyProperty を取得または設定します。
	/// </summary>
	public IEnumerable ItemsSource
	{
		get => (IEnumerable)this.GetValue(ItemsSourceProperty);
		set => this.SetValue(ItemsSourceProperty, value);
	}

	/// <summary>
	/// ItemsSource DependencyProperty の定義。
	/// </summary>
	public static readonly DependencyProperty ItemsSourceProperty =
		DependencyProperty.Register(
			nameof(ItemsSource),
			typeof(IEnumerable),
			typeof(SeriesTagSelector),
			new PropertyMetadata(null));

	/// <summary>
	/// Columns DependencyProperty を取得または設定します。
	/// </summary>
	public int Columns
	{
		get => (int)this.GetValue(ColumnsProperty);
		set => this.SetValue(ColumnsProperty, value);
	}

	/// <summary>
	/// Columns DependencyProperty の定義。
	/// </summary>
	public static readonly DependencyProperty ColumnsProperty =
		DependencyProperty.Register(
			nameof(Columns),
			typeof(int),
			typeof(SeriesTagSelector),
			new PropertyMetadata(2));

	/// <summary>
	/// Rows DependencyProperty を取得または設定します。
	/// </summary>
	public int Rows
	{
		get => (int)this.GetValue(RowsProperty);
		set => this.SetValue(RowsProperty, value);
	}

	/// <summary>
	/// Rows DependencyProperty の定義。
	/// </summary>
	public static readonly DependencyProperty RowsProperty =
		DependencyProperty.Register(
			nameof(Rows),
			typeof(int),
			typeof(SeriesTagSelector),
			new PropertyMetadata(0));

	public SeriesTagSelector()
	{
		InitializeComponent();
	}
}
