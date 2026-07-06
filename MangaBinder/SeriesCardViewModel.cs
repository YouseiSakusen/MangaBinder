using MangaBinder.Controls;

namespace MangaBinder;

/// <summary>
/// Home 画面の ListView アイテム表示用 ViewModel です。MangaSeries をラップしています。
/// </summary>
public class SeriesCardViewModel
{
	/// <summary>
	/// 基になった MangaSeries です。
	/// </summary>
	public MangaSeries Series { get; }

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// </summary>
	public SeriesVolumeStatusViewModel VolumeStatus { get; }

	/// <summary>
	/// <see cref="SeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="series">ラップする MangaSeries。</param>
	public SeriesCardViewModel(MangaSeries series)
	{
		this.Series = series;
		this.VolumeStatus = SeriesVolumeStatusViewModel.FromSeries(series);
	}
}
