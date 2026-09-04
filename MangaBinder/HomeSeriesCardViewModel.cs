using MangaBinder.Bindings;
using MangaBinder.Controls;
using MangaBinder.Series;
using R3;

namespace MangaBinder;

/// <summary>
/// Home 画面の ListView アイテム表示用 ViewModel です。MangaSeries をラップしています。
/// </summary>
public class HomeSeriesCardViewModel : IDisposable
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
	/// メモ表示用の ViewModel です。
	/// </summary>
	public BindableReactiveProperty<SeriesMemoViewModel> MemoStatus { get; }

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
	/// <see cref="HomeSeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="seriesViewModel">ラップする MangaSeriesViewModel。</param>
	/// <param name="mangaSeriesStore">MangaSeries ストア。タグマスタ取得用。</param>
	/// <param name="seriesTagStore">タグ変更追跡ストア。Dirty 管理用。</param>
	public HomeSeriesCardViewModel(MangaSeriesViewModel seriesViewModel, MangaSeriesStore? mangaSeriesStore = null, SeriesTagStore? seriesTagStore = null)
	{
		// 共有 MangaSeriesViewModel.Series をそのまま参照（新規生成しない）
		this.Series = seriesViewModel.Series;

		// 巻情報表示用の SeriesVolumeStatusViewModel を生成（1インスタンスのみ保持）
		var volumeStatus = SeriesVolumeStatusViewModel.FromSeries(this.Series.Value);
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(volumeStatus)
			.AddTo(ref this.disposableBag);

		// volumeStatus は BindableReactiveProperty に保持されるため、ここでは管理不要だが
		// IDisposable であるため、Dispose 時に破棄されるよう disposableBag に登録
		volumeStatus.AddTo(ref this.disposableBag);

		// メモ表示用の SeriesMemoViewModel を生成（1インスタンスのみ保持）
		var memoStatus = SeriesMemoViewModel.FromSeries(this.Series.Value);
		this.MemoStatus = new BindableReactiveProperty<SeriesMemoViewModel>(memoStatus)
			.AddTo(ref this.disposableBag);

		// memoStatus は BindableReactiveProperty に保持されるため、ここでは管理不要だが
		// IDisposable であるため、Dispose 時に破棄されるよう disposableBag に登録
		memoStatus.AddTo(ref this.disposableBag);

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

			// メモ表示用の通知も同じ形式で流す
			if (this.MemoStatus.Value.Series.Value != newSeries)
			{
				this.MemoStatus.Value.Series.Value = newSeries;
			}
			else if (this.MemoStatus.Value.Series.Value == newSeries)
			{
				// 同一インスタンスの場合は ForceNotify() で再通知させる
				this.MemoStatus.Value.Series.ForceNotify();
			}
		})
		.AddTo(ref this.disposableBag);

		// IsSelected は Home 画面固有の表示状態。
		// 初期値は false で、HomeSeriesStore が BindingQueueStore.Queue から状態を設定する
		this.IsSelected = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// TagSelector の初期化
		this.tagSelector = new SeriesTagSelectorViewModel(mangaSeriesStore ?? throw new ArgumentNullException(nameof(mangaSeriesStore)))
			.AddTo(ref this.disposableBag);

		// 対象作品を設定し、タグ変更時の Dirty 登録処理を接続
		var onTagsChanged = seriesTagStore != null
			? (Action<MangaSeries>)(s => seriesTagStore.MarkDirty(s))
			: null;
		this.tagSelector.SetTarget(this.Series.Value, onTagsChanged);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.tagSelector.Dispose();
		this.disposableBag.Dispose();
	}

	/// <summary>
	/// IsSelected の値を設定します。Home 画面の表示状態更新用。
	/// HomeSeriesStore が BindingQueueStore.Queue の変更を監視して呼び出します。
	/// </summary>
	/// <param name="isSelected">新しい IsSelected 値。</param>
	internal void SetIsSelected(bool isSelected)
	{
		this.IsSelected.Value = isSelected;
	}
}
