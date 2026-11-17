namespace MangaBinder.Series;

/// <summary>
/// NumberBox の入力値（<see cref="double"/>?）と、<see cref="MangaSeries"/> の巻数（<see cref="int"/>）を相互に変換します。
/// 型と未入力の表現の違いだけを吸収し、業務上の値の更新は行いません。
/// </summary>
public static class VolumeInputConverter
{
	/// <summary>
	/// NumberBox の入力値を <see cref="MangaSeries"/> の巻数に変換します。
	/// </summary>
	/// <param name="inputValue">NumberBox の入力値。未入力の場合は null。</param>
	/// <returns>対応する巻数。未入力の場合は 0。</returns>
	public static int ToSeriesVolume(double? inputValue)
		=> inputValue.HasValue ? (int)inputValue.Value : 0;

	/// <summary>
	/// <see cref="MangaSeries"/> の巻数を NumberBox の入力値に変換します。
	/// </summary>
	/// <param name="seriesVolume">作品の巻数。</param>
	/// <returns>対応する入力値。巻数が 0 以下の場合は未入力を表す null。</returns>
	public static double? ToInputValue(int seriesVolume)
		=> seriesVolume > 0 ? seriesVolume : null;
}
