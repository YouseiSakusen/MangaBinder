using MangaBinder.Bindings;
using MangaBinder.Controls;
using R3;

namespace MangaBinder;

/// <summary>
/// Home 画面の ListView アイテム表示用 ViewModel です。MangaSeries をラップしています。
/// </summary>
public class SeriesCardViewModel : IDisposable
{
	private DisposableBag disposableBag = new();
	private SeriesTagSelectorViewModel tagSelector = null!;

	/// <summary>
	/// 基になった MangaSeries です。
	/// ReactiveProperty として、値の変更を監視できます。
	/// </summary>
	public BindableReactiveProperty<MangaSeries> Series { get; }

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// </summary>
	public BindableReactiveProperty<SeriesVolumeStatusViewModel> VolumeStatus { get; }

	/// <summary>
	/// 製本対象として選択されているかどうかを示します。
	/// UI 状態のため、SeriesCardViewModel が保持します。
	/// </summary>
	public BindableReactiveProperty<bool> IsSelected { get; }

	/// <summary>
	/// タグ選択・表示状態を管理する ViewModel です。
	/// </summary>
	public SeriesTagSelectorViewModel TagSelector => this.tagSelector;

	/// <summary>
	/// <see cref="SeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="series">ラップする MangaSeries。</param>
	/// <param name="bindingQueueStore">製本開始キュー ストア。初期値決定用。</param>
	/// <param name="mangaSeriesStore">MangaSeries ストア。タグマスタ取得用。</param>
	/// <param name="seriesTagStore">タグ変更追跡ストア。Dirty 管理用。</param>
	public SeriesCardViewModel(MangaSeries series, BindingQueueStore? bindingQueueStore = null, MangaSeriesStore? mangaSeriesStore = null, SeriesTagStore? seriesTagStore = null)
	{
		this.Series = new BindableReactiveProperty<MangaSeries>(series)
			.AddTo(ref this.disposableBag);

		// 巻情報表示用の SeriesVolumeStatusViewModel を生成（1インスタンスのみ保持）
		var volumeStatus = SeriesVolumeStatusViewModel.FromSeries(series);
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(volumeStatus)
			.AddTo(ref this.disposableBag);

		// volumeStatus は BindableReactiveProperty に保持されるため、ここでは管理不要だが
		// IDisposable であるため、Dispose 時に破棄されるよう disposableBag に登録
		volumeStatus.AddTo(ref this.disposableBag);

		// Series 通知を SeriesVolumeStatusViewModel.Series へ流す
		// 同一インスタンスの ForceNotify() にも対応するため、通知が来たら内容をチェック
		this.Series.Subscribe(newSeries =>
		{
			// 新しい Series インスタンスが設定された場合は、VolumeStatus.Value.Series.Value に設定
			// 同一インスタンスの ForceNotify() 時も、ここに到達する
			if (this.VolumeStatus.Value.Series.Value != newSeries)
			{
				this.VolumeStatus.Value.Series.Value = newSeries;
			}
			else if (this.VolumeStatus.Value.Series.Value == newSeries)
			{
				// 同一インスタンスの場合は ForceNotify() で再通知させる
				this.VolumeStatus.Value.Series.ForceNotify();
			}
		})
		.AddTo(ref this.disposableBag);

		// IsSelected の初期値を BindingQueueStore から決定
		var isInQueue = bindingQueueStore?.Contains(series.SeriesId) ?? false;
		this.IsSelected = new BindableReactiveProperty<bool>(isInQueue)
			.AddTo(ref this.disposableBag);

		// TagSelector の初期化
		this.tagSelector = new SeriesTagSelectorViewModel(mangaSeriesStore ?? throw new ArgumentNullException(nameof(mangaSeriesStore)))
			.AddTo(ref this.disposableBag);

		// 対象作品を設定し、タグ変更時の Dirty 登録処理を接続
		var onTagsChanged = seriesTagStore != null
			? (Action<MangaSeries>)(s => seriesTagStore.MarkDirty(s))
			: null;
		this.tagSelector.SetTarget(series, onTagsChanged);
	}

	/// <summary>
	/// 現在の Series の情報から表示を更新します。
	/// タグ表示を最新の状態に反映させます。
	/// Series 情報と巻情報は Series.ForceNotify() の通知ラインで自動更新されます。
	/// </summary>
	public void RefreshDisplay()
	{
		this.Series.ForceNotify();
		this.tagSelector.Refresh();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.tagSelector.Dispose();
		this.disposableBag.Dispose();
	}
}
