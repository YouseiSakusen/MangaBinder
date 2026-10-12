using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBinder.Controls;

/// <summary>
/// 作品メモの編集ボタンです。
/// Series を Binding するだけで、Dialog 表示から保存までを完結します。
/// </summary>
public partial class MemoEditButton : UserControl
{
	/// <summary>編集対象の作品（Store 正本インスタンス）を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
		nameof(Series),
		typeof(MangaSeries),
		typeof(MemoEditButton),
		new PropertyMetadata(null, onSeriesChanged));

	/// <summary>ViewModel を表す読み取り専用依存関係プロパティのキー。</summary>
	private static readonly DependencyPropertyKey viewModelPropertyKey = DependencyProperty.RegisterReadOnly(
		nameof(ViewModel),
		typeof(MemoEditButtonViewModel),
		typeof(MemoEditButton),
		new PropertyMetadata(null));

	/// <summary>ViewModel を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty ViewModelProperty = viewModelPropertyKey.DependencyProperty;

	/// <summary>
	/// 編集対象の作品を取得または設定します。
	/// </summary>
	public MangaSeries? Series
	{
		get => (MangaSeries?)this.GetValue(SeriesProperty);
		set => this.SetValue(SeriesProperty, value);
	}

	/// <summary>
	/// ボタンの ViewModel を取得します。Loaded 中のみ有効です。
	/// </summary>
	public MemoEditButtonViewModel? ViewModel => (MemoEditButtonViewModel?)this.GetValue(ViewModelProperty);

	/// <summary>
	/// <see cref="MemoEditButton"/> の新しいインスタンスを初期化します。
	/// </summary>
	public MemoEditButton()
	{
		this.InitializeComponent();
		this.Loaded += this.onLoaded;
		this.Unloaded += this.onUnloaded;
	}

	/// <summary>
	/// Series 変更時に ViewModel へ反映します。
	/// </summary>
	private static void onSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var button = (MemoEditButton)d;
		button.ViewModel?.SetSeries((MangaSeries?)e.NewValue);
	}

	/// <summary>
	/// Loaded 時に ViewModel を生成します。
	/// </summary>
	private void onLoaded(object sender, RoutedEventArgs e)
	{
		var viewModel = App.Services.GetRequiredService<MemoEditButtonViewModel>();
		viewModel.SetSeries(this.Series);
		this.button.DataContext = viewModel;
		this.SetValue(viewModelPropertyKey, viewModel);
	}

	/// <summary>
	/// Unloaded 時に ViewModel を破棄します。
	/// </summary>
	private void onUnloaded(object sender, RoutedEventArgs e)
	{
		this.button.DataContext = null;
		this.ViewModel?.Dispose();
		this.SetValue(viewModelPropertyKey, null);
	}
}
