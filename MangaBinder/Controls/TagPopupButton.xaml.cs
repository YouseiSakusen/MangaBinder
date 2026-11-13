using System.Windows;
using System.Windows.Controls;
using MangaBinder.Core.Formatters;
using MangaBinder.Series;
using MangaBinder.Tags;
using ObservableCollections;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 作品のタグ表示ボタンです。
/// 対象作品の共有 <see cref="MangaSeriesNotifier"/> のタグを表示し、クリック時に共通の <see cref="SeriesTagPicker"/> へ Popup 表示を依頼します。
/// タグマスタの管理や DB 保存は行いません。
/// </summary>
public partial class TagPopupButton : UserControl
{
	/// <summary>対象作品の共有 Notifier を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
		nameof(Series),
		typeof(MangaSeriesNotifier),
		typeof(TagPopupButton),
		new PropertyMetadata(null, onSeriesChanged));

	/// <summary>Popup 表示を依頼する共通 <see cref="SeriesTagPicker"/> を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty PickerProperty = DependencyProperty.Register(
		nameof(Picker),
		typeof(SeriesTagPicker),
		typeof(TagPopupButton),
		new PropertyMetadata(null, onPickerChanged));

	/// <summary>タグの内容によらず常に表示する固定文字列を表す依存関係プロパティ。</summary>
	public static readonly DependencyProperty FixedTextProperty = DependencyProperty.Register(
		nameof(FixedText),
		typeof(string),
		typeof(TagPopupButton),
		new PropertyMetadata(null, onFixedTextChanged));

	private MangaSeriesNotifier? trackedSeries;
	private SeriesTagPicker? subscribedPicker;
	private IDisposable? firstTagSubscription;

	/// <summary>
	/// 対象作品の共有 Notifier を取得または設定します。
	/// </summary>
	public MangaSeriesNotifier? Series
	{
		get => (MangaSeriesNotifier?)this.GetValue(SeriesProperty);
		set => this.SetValue(SeriesProperty, value);
	}

	/// <summary>
	/// Popup 表示を依頼する共通 <see cref="SeriesTagPicker"/> を取得または設定します。
	/// </summary>
	public SeriesTagPicker? Picker
	{
		get => (SeriesTagPicker?)this.GetValue(PickerProperty);
		set => this.SetValue(PickerProperty, value);
	}

	/// <summary>
	/// 固定表示文字列を取得または設定します。
	/// null の場合は <see cref="SeriesTagDisplayFormatter.Format"/> による、対象作品のタグに応じた表示になります。
	/// </summary>
	public string? FixedText
	{
		get => (string?)this.GetValue(FixedTextProperty);
		set => this.SetValue(FixedTextProperty, value);
	}

	/// <summary>
	/// <see cref="TagPopupButton"/> の新しいインスタンスを初期化します。
	/// </summary>
	public TagPopupButton()
	{
		this.InitializeComponent();
		this.Loaded += this.onLoaded;
		this.Unloaded += this.onUnloaded;
	}

	private static void onSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var button = (TagPopupButton)d;

		if (button.IsLoaded)
		{
			button.detachSeries();
			button.attachSeries();
		}
	}

	private static void onPickerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var button = (TagPopupButton)d;

		if (button.IsLoaded)
		{
			button.detachPicker();
			button.attachPicker();
		}
	}

	private static void onFixedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((TagPopupButton)d).updateDisplay();
	}

	/// <summary>
	/// Loaded 時に、対象作品と Picker の購読を開始します。
	/// </summary>
	private void onLoaded(object sender, RoutedEventArgs e)
	{
		this.attachSeries();
		this.attachPicker();
	}

	/// <summary>
	/// Unloaded 時に、表示中の Popup を閉じて購読を解除します。
	/// </summary>
	private void onUnloaded(object sender, RoutedEventArgs e)
	{
		// Unchecked により Picker へ閉鎖が依頼される。Picker の購読を解除する前に行う
		this.TagToggleButton.IsChecked = false;

		this.detachPicker();
		this.detachSeries();
	}

	/// <summary>
	/// ToggleButton が ON になったとき、共通 Picker へ Popup 表示を依頼します。
	/// </summary>
	private void onTagToggleButtonChecked(object sender, RoutedEventArgs e)
	{
		var picker = this.Picker
			?? throw new InvalidOperationException("TagPopupButton.Picker が設定されていません。");
		var series = this.trackedSeries
			?? throw new InvalidOperationException("TagPopupButton.Series が設定されていません。");

		picker.Open(series, this.TagToggleButton);
	}

	/// <summary>
	/// ToggleButton が OFF になったとき、共通 Picker へ Popup を閉じるよう依頼します。
	/// </summary>
	private void onTagToggleButtonUnchecked(object sender, RoutedEventArgs e)
	{
		this.Picker?.Close(this.TagToggleButton);
	}

	/// <summary>
	/// 共通 Picker の Popup が閉じられたとき、自身が基準だった場合に ToggleButton を OFF に戻します。
	/// </summary>
	private void onPickerPopupClosed(FrameworkElement placementTarget)
	{
		if (ReferenceEquals(placementTarget, this.TagToggleButton))
		{
			this.TagToggleButton.IsChecked = false;
		}
	}

	/// <summary>
	/// 現在の <see cref="Picker"/> の PopupClosed を購読します。
	/// </summary>
	private void attachPicker()
	{
		this.subscribedPicker = this.Picker;

		if (this.subscribedPicker is not null)
		{
			this.subscribedPicker.PopupClosed += this.onPickerPopupClosed;
		}
	}

	/// <summary>
	/// 購読中の Picker の PopupClosed の購読を解除します。
	/// </summary>
	private void detachPicker()
	{
		if (this.subscribedPicker is not null)
		{
			this.subscribedPicker.PopupClosed -= this.onPickerPopupClosed;
			this.subscribedPicker = null;
		}
	}

	/// <summary>
	/// 現在の <see cref="Series"/> のタグ変更を購読し、表示を更新します。
	/// </summary>
	private void attachSeries()
	{
		this.trackedSeries = this.Series;

		if (this.trackedSeries is not null)
		{
			this.trackedSeries.Tags.CollectionChanged += this.onSeriesTagsChanged;
		}

		this.updateDisplay();
	}

	/// <summary>
	/// 購読中の作品のタグ変更の購読を解除します。共有 Notifier は破棄しません。
	/// </summary>
	private void detachSeries()
	{
		this.disposeFirstTagSubscription();

		if (this.trackedSeries is not null)
		{
			this.trackedSeries.Tags.CollectionChanged -= this.onSeriesTagsChanged;
			this.trackedSeries = null;
		}
	}

	/// <summary>
	/// 対象作品のタグの追加・削除時に表示を更新します。
	/// </summary>
	private void onSeriesTagsChanged(in NotifyCollectionChangedEventArgs<TagNotifier> e)
	{
		this.updateDisplay();
	}

	/// <summary>
	/// 表示文字列とボタンの有効状態を更新し、先頭タグの名称変更の購読を張り直します。
	/// </summary>
	private void updateDisplay()
	{
		this.disposeFirstTagSubscription();

		this.TagToggleButton.IsEnabled = this.trackedSeries is not null;

		if (this.FixedText is { } fixedText)
		{
			this.TagToggleButton.Content = fixedText;
			return;
		}

		if (this.trackedSeries is null)
		{
			this.TagToggleButton.Content = string.Empty;
			return;
		}

		this.TagToggleButton.Content = SeriesTagDisplayFormatter.Format(this.trackedSeries.Tags.Select(t => t.Tag.Value));

		// 表示に使う名称は先頭タグだけのため、先頭タグの名称変更だけを購読する
		if (this.trackedSeries.Tags.Count > 0)
		{
			this.firstTagSubscription = this.trackedSeries.Tags[0].Tag
				.Skip(1)
				.Subscribe(this.onFirstTagChanged);
		}
	}

	/// <summary>
	/// 先頭タグの名称変更時に表示文字列を更新します。
	/// </summary>
	private void onFirstTagChanged(MangaTag tag)
	{
		this.updateDisplay();
	}

	/// <summary>
	/// 先頭タグの購読を解除します。
	/// </summary>
	private void disposeFirstTagSubscription()
	{
		this.firstTagSubscription?.Dispose();
		this.firstTagSubscription = null;
	}
}
