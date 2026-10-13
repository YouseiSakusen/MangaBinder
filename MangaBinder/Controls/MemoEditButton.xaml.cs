using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MangaBinder.Series;
using Microsoft.Extensions.DependencyInjection;

namespace MangaBinder.Controls;

/// <summary>
/// 作品メモの編集ボタンです。
/// Series を Binding するだけで、Dialog 表示から保存までを完結します。
/// </summary>
public partial class MemoEditButton : UserControl
{
	/// <summary>編集対象の共有 MangaSeriesViewModel を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty SeriesViewModelProperty = DependencyProperty.Register(
		nameof(SeriesViewModel),
		typeof(MangaSeriesViewModel),
		typeof(MemoEditButton),
		new PropertyMetadata(null, onSeriesViewModelChanged));

	/// <summary>ViewModel を表す読み取り専用依存関係プロパティのキー。</summary>
	private static readonly DependencyPropertyKey viewModelPropertyKey = DependencyProperty.RegisterReadOnly(
		nameof(ViewModel),
		typeof(MemoEditButtonViewModel),
		typeof(MemoEditButton),
		new PropertyMetadata(null));

	/// <summary>ViewModel を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty ViewModelProperty = viewModelPropertyKey.DependencyProperty;

	/// <summary>
	/// 編集対象の共有 MangaSeriesViewModel を取得または設定します。
	/// </summary>
	public MangaSeriesViewModel? SeriesViewModel
	{
		get => (MangaSeriesViewModel?)this.GetValue(SeriesViewModelProperty);
		set => this.SetValue(SeriesViewModelProperty, value);
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
	/// SeriesViewModel 変更時に ViewModel へ反映します。
	/// </summary>
	private static void onSeriesViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var button = (MemoEditButton)d;
		button.ViewModel?.SetSeriesViewModel((MangaSeriesViewModel?)e.NewValue);
	}

	/// <summary>
	/// Loaded 時に ViewModel を生成します。
	/// </summary>
	private void onLoaded(object sender, RoutedEventArgs e)
	{
		if (DesignerProperties.GetIsInDesignMode(this))
			return;

		var viewModel = App.Services.GetRequiredService<MemoEditButtonViewModel>();
		viewModel.SetSeriesViewModel(this.SeriesViewModel);
		this.button.DataContext = viewModel;
		this.SetValue(viewModelPropertyKey, viewModel);
	}

	/// <summary>
	/// Unloaded 時に ViewModel を破棄します。
	/// </summary>
	private void onUnloaded(object sender, RoutedEventArgs e)
	{
		if (DesignerProperties.GetIsInDesignMode(this))
			return;

		this.button.DataContext = null;
		this.ViewModel?.Dispose();
		this.SetValue(viewModelPropertyKey, null);
	}
}
