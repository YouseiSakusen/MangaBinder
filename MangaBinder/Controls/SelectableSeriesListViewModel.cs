using MangaBinder.Series;
using ObservableCollections;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 作品一覧の共通 UserControl 用 ViewModel です。
/// 親ViewModel から MangaSeries の一覧を受け取り、MaintenanceSeriesCardViewModel を生成・管理します。
/// SelectableSeriesList に表示する作品カード、選択状態、操作ボタン表示の制御を担います。
/// </summary>
public class SelectableSeriesListViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 表示対象の MangaSeries コレクション。
	/// </summary>
	private readonly ObservableList<MangaSeries> series;

	/// <summary>
	/// 表示する作品カードの一覧。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<MaintenanceSeriesCardViewModel> Items { get; }

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

		this.Items = this.series
			.CreateView(series => new MaintenanceSeriesCardViewModel(series))
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
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
	}

	/// <summary>
	/// 表示対象の MangaSeries コレクションを設定します。
	/// 渡された一覧を内部の ObservableList へ反映します。
	/// 既存の MaintenanceSeriesCardViewModel は適切に Dispose されます。
	/// </summary>
	/// <param name="series">表示対象の MangaSeries コレクション。</param>
	public void SetSource(IReadOnlyList<MangaSeries> series)
	{
		// 現在の Items に存在するカードVMを退避
		var oldCards = this.Items.ToList();

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
	/// 現在保持している MaintenanceSeriesCardViewModel を再評価します。
	/// 既存の MangaSeries に対して ForceNotify() を実行し、UI の再表示を促します。
	/// </summary>
	public void RefreshAllItems()
	{
		foreach (var item in this.Items)
		{
			// Series の値が同一インスタンスの場合、ForceNotify() で再通知を促す
			item.Series.ForceNotify();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// 現在 Items に残っている全 MaintenanceSeriesCardViewModel を Dispose
		foreach (var card in this.Items)
		{
			card.Dispose();
		}

		// R3 の購読を Dispose
		this.disposableBag.Dispose();
	}
}
