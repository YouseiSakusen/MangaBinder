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
	private MangaSeries series = null!;
	private SeriesTagSelectorViewModel tagSelector = null!;

	/// <summary>
	/// 基になった MangaSeries です。
	/// </summary>
	public MangaSeries Series { get; }

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
		this.Series = series;
		this.series = series;

		// VolumeStatus の初期化
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(
			SeriesVolumeStatusViewModel.FromSeries(series))
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
	/// 現在の Series インスタンスから VolumeStatus を再生成します。
	/// </summary>
	public void RefreshVolumeStatus()
	{
		this.VolumeStatus.Value = SeriesVolumeStatusViewModel.FromSeries(this.Series);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.tagSelector.Dispose();
		this.disposableBag.Dispose();
	}
}
