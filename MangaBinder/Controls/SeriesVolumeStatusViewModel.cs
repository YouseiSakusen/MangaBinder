namespace MangaBinder.Controls;

/// <summary>
/// 巻情報表示用の ViewModel です。表示専用です。
/// </summary>
public class SeriesVolumeStatusViewModel
{
	/// <summary>全巻数テキストです。完結していない場合は "-" を返します。</summary>
	public string TotalVolumeText { get; init; } = string.Empty;

	/// <summary>所持推定巻数テキストです。</summary>
	public string OwnedEstimatedVolumeText { get; init; } = string.Empty;

	/// <summary>製本済み最終巻テキストです。</summary>
	public string BoundEndVolumeText { get; init; } = string.Empty;

	/// <summary>作品が完結しているかを示します。バッジ背景色判定用です。</summary>
	public bool SeriesCompleted { get; init; }

	/// <summary>全巻所持済みかを示します。バッジ背景色判定用です。</summary>
	public bool IsOwnedCompleted { get; init; }

	/// <summary>
	/// <see cref="MangaSeries"/> から <see cref="SeriesVolumeStatusViewModel"/> を生成します。
	/// </summary>
	/// <param name="series">変換元の MangaSeries。</param>
	/// <returns>生成された ViewModel。</returns>
	public static SeriesVolumeStatusViewModel FromSeries(MangaSeries series)
	{
		return new SeriesVolumeStatusViewModel
		{
			TotalVolumeText = series.SeriesCompleted
				? $"全{series.EndVolume}巻"
				: "-",
			OwnedEstimatedVolumeText = series.OwnedMaxVolume > 0
				? $"所持推定：{series.OwnedMaxVolume}"
				: "所持推定：-",
			BoundEndVolumeText = series.BoundEndVolume > 0
				? $"製本済み：{series.BoundEndVolume}"
				: "製本済み：-",
			SeriesCompleted = series.SeriesCompleted,
			IsOwnedCompleted = series.IsOwnedCompleted,
		};
	}
}
