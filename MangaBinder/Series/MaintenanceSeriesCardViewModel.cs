using MangaBinder.Controls;
using MangaBinder.Core.Series;
using R3;

namespace MangaBinder.Series;

/// <summary>
/// 作品管理画面と既存作品確認ダイアログで表示する作品情報の ViewModel です。
/// 巻情報表示のみに特化した最小設計で、タグ管理や製本対象選択など Home 専用の機能は保持しません。
/// 新 Reactive 系では共有の MangaSeriesViewModel を受け取り、その Series を参照します。
/// </summary>
public class MaintenanceSeriesCardViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// ラップする MangaSeries です。
	/// ReactiveProperty として、値の変更を監視できます。
	/// 新 Reactive 系では共有の MangaSeriesViewModel が保持する Series を参照します。
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
	/// <see cref="MaintenanceSeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// 新 Reactive 系では、共有の MangaSeriesViewModel を受け取ります。
	/// </summary>
	/// <param name="viewModel">共有の MangaSeriesViewModel。この Series プロパティを参照します。</param>
	public MaintenanceSeriesCardViewModel(MangaSeriesViewModel viewModel)
	{
		// 共有の Series を直接参照する（同一インスタンス）
		// MangaSeriesViewModel が所有し管理するため、このインスタンスでは Dispose しない
		this.Series = viewModel.Series;

		// 巻情報表示用の SeriesVolumeStatusViewModel を生成（1インスタンスのみ保持）
		var initialSeries = viewModel.Series.Value;
		var volumeStatus = SeriesVolumeStatusViewModel.FromSeries(initialSeries);
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(volumeStatus)
			.AddTo(ref this.disposableBag);

		volumeStatus.AddTo(ref this.disposableBag);

		// メモ表示用の SeriesMemoViewModel を生成（1インスタンスのみ保持）
		var memoStatus = SeriesMemoViewModel.FromSeries(initialSeries);
		this.MemoStatus = new BindableReactiveProperty<SeriesMemoViewModel>(memoStatus)
			.AddTo(ref this.disposableBag);

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
	}

	/// <summary>
	/// <see cref="MaintenanceSeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// 既存ダイアログ互換性維持用。MangaSeries を直接受け取ります。
	/// </summary>
	/// <param name="series">ラップする MangaSeries。</param>
	public MaintenanceSeriesCardViewModel(MangaSeries series)
	{
		// Series の初期化
		this.Series = new BindableReactiveProperty<MangaSeries>(series)
			.AddTo(ref this.disposableBag);

		// 巻情報表示用の SeriesVolumeStatusViewModel を生成（1インスタンスのみ保持）
		var volumeStatus = SeriesVolumeStatusViewModel.FromSeries(series);
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(volumeStatus)
			.AddTo(ref this.disposableBag);

		// volumeStatus は BindableReactiveProperty に保持されるため、ここでは管理不要だが
		// IDisposable であるため、Dispose 時に破棄されるよう disposableBag に登録
		volumeStatus.AddTo(ref this.disposableBag);

		// メモ表示用の SeriesMemoViewModel を生成（1インスタンスのみ保持）
		var memoStatus = SeriesMemoViewModel.FromSeries(series);
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
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
