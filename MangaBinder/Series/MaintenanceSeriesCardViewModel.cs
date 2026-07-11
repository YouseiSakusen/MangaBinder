using MangaBinder.Controls;
using R3;

namespace MangaBinder.Series;

/// <summary>
/// 作品管理画面と既存作品確認ダイアログで表示する作品情報の ViewModel です。
/// 巻情報表示のみに特化した最小設計で、タグ管理や製本対象選択など Home 専用の機能は保持しません。
/// </summary>
public class MaintenanceSeriesCardViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// ラップする MangaSeries です。
	/// </summary>
	public MangaSeries Series { get; }

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// </summary>
	public BindableReactiveProperty<SeriesVolumeStatusViewModel> VolumeStatus { get; }

	/// <summary>
	/// <see cref="MaintenanceSeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="series">ラップする MangaSeries。</param>
	public MaintenanceSeriesCardViewModel(MangaSeries series)
	{
		this.Series = series;

		// VolumeStatus の初期化
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(
			SeriesVolumeStatusViewModel.FromSeries(series))
			.AddTo(ref this.disposableBag);
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
		this.disposableBag.Dispose();
	}
}
