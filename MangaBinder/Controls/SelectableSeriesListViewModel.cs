using MangaBinder.Series;
using ObservableCollections;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 作品一覧の共通 UserControl 用 ViewModel です。
/// 内部 ObservableList から MaintenanceSeriesCardViewModel を生成する従来の方式と、
/// 外部から直接 NotifyCollectionChangedSynchronizedViewList を参照する新 Reactive 方式の両方に対応します。
/// SelectableSeriesList に表示する作品カード、選択状態、操作ボタン表示の制御を担います。
/// </summary>
public class SelectableSeriesListViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 内部用の MangaSeries コレクション。
	/// SetSource() により検索結果などを一時的に設定する場合に使用されます。
	/// </summary>
	private readonly ObservableList<MangaSeries> series;

	/// <summary>
	/// 内部用の Items（内部 ObservableList から生成）。
	/// 外部参照が設定されていない場合はこちらが Items.Value として公開されます。
	/// </summary>
	private readonly NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel> internalItems;

	/// <summary>
	/// 現在表示する作品カードの一覧。
	/// 内部か外部かを Reactive に切り替える。
	/// </summary>
	public BindableReactiveProperty<NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel>> Items { get; }

	/// <summary>
	/// 現在選択されている MaintenanceSeriesCardViewModel。
	/// </summary>
	public BindableReactiveProperty<MaintenanceSeriesCardViewModel?> SelectedItem { get; }

	/// <summary>
	/// 現在選択されている MangaSeries。
	/// </summary>
	public BindableReactiveProperty<MangaSeries?> SelectedSeries { get; }

	/// <summary>
	/// 「▶」ボタンを表示するかどうかを示す値。
	/// </summary>
	public BindableReactiveProperty<bool> ShowNavigateButton { get; }

	/// <summary>
	/// 作品カードの表示サイズ。
	/// </summary>
	public BindableReactiveProperty<SeriesCardSize> CardSize { get; }

	/// <summary>
	/// 「▶」ボタン押下時に実行される Command。
	/// CommandParameter として対象の MangaSeries が渡されます。
	/// </summary>
	public ReactiveCommand<MangaSeries>? NavigateCommand { get; set; }

	/// <summary>
	/// <see cref="SelectableSeriesListViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	public SelectableSeriesListViewModel()
	{
		// Items: 表示する MangaSeries から変換された MaintenanceSeriesCardViewModel の一覧
		this.series = new ObservableList<MangaSeries>();

		this.internalItems = this.series
			.CreateView(series => new MaintenanceSeriesCardViewModel(series))
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// Items: 初期値は internalItems、外部参照により切り替える
		this.Items = new BindableReactiveProperty<NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel>>(this.internalItems)
			.AddTo(ref this.disposableBag);

		// SelectedItem: 現在選択されているカード ViewModel
		this.SelectedItem = new BindableReactiveProperty<MaintenanceSeriesCardViewModel?>(null)
			.AddTo(ref this.disposableBag);

		// SelectedSeries: 現在選択されている MangaSeries
		this.SelectedSeries = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		// SelectedItem が変わったら SelectedSeries も同期
		this.SelectedItem
			.Subscribe(selectedItem =>
			{
				this.SelectedSeries.Value = selectedItem?.Series.Value;
			})
			.AddTo(ref this.disposableBag);

		// ShowNavigateButton: デフォルトは false
		this.ShowNavigateButton = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// CardSize: デフォルトは Compact
		this.CardSize = new BindableReactiveProperty<SeriesCardSize>(SeriesCardSize.Compact)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 外部参照の Items コレクションを設定します。
	/// Items.Value を外部参照に切り替え、WPF に通知します。
	/// 外部参照の所有権は呼び出し側にあります。
	/// </summary>
	/// <param name="externalItems">外部参照の Items コレクション。</param>
	public void SetExternalSource(NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel> externalItems)
	{
		this.Items.Value = externalItems;
		this.SelectedItem.Value = null;
	}

	/// <summary>
	/// 外部参照を解除し、内部 ObservableList を使用する状態に戻します。
	/// Items.Value を internalItems に戻し、WPF に通知します。
	/// </summary>
	public void ClearExternalSource()
	{
		this.Items.Value = this.internalItems;
		this.SelectedItem.Value = null;
	}

	/// <summary>
	/// 表示対象の MangaSeries コレクションを設定します。
	/// 渡された一覧を内部の ObservableList へ反映します。
	/// 外部参照がある場合は自動的に解除され、内部モードに戻ります。
	/// 既存の MaintenanceSeriesCardViewModel は適切に Dispose されます。
	/// </summary>
	/// <param name="series">表示対象の MangaSeries コレクション。</param>
	public void SetSource(IReadOnlyList<MangaSeries> series)
	{
		// 外部参照がある場合は解除
		this.ClearExternalSource();

		// 現在の Items に存在するカードVMを退避
		var oldCards = new List<MaintenanceSeriesCardViewModel>();

		foreach (var card in this.internalItems)
		{
			oldCards.Add(card);
		}

		// 内部 ObservableList をクリア
		this.series.Clear();

		// 退避した古いカードVMを Dispose
		foreach (var card in oldCards)
		{
			card.Dispose();
		}

		// 新しい MangaSeries を AddRange（View での生成は自動的に行われる）
		this.series.AddRange(series);

		// 選択状態をクリア
		this.SelectedItem.Value = null;
	}


	/// <summary>
	/// 現在表示している MaintenanceSeriesCardViewModel を再評価します。
	/// 既存の MangaSeries に対して ForceNotify() を実行し、UI の再表示を促します。
	/// </summary>
	public void RefreshAllItems()
	{
		foreach (var item in this.Items.Value)
		{
			// Series の値が同一インスタンスの場合、ForceNotify() で再通知を促す
			item.Series.ForceNotify();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// 外部参照がある場合も内部 Items も、両方の Dispose を呼び出す
		// 外部参照は呼び出し側が所有しているため、Dispose は呼び出さない
		// 内部の古い MaintenanceSeriesCardViewModel を Dispose
		foreach (var card in this.internalItems)
		{
			card.Dispose();
		}

		// R3 の購読を Dispose
		this.disposableBag.Dispose();
	}
}
