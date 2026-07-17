namespace MangaBinder.Series;

/// <summary>
/// 新規作品のHome即時反映問題を追跡するためのTrace制御クラスです。
/// 正式登録した新規作品1件だけを追跡対象とし、起動時の全件読込では調査ログを出力しません。
/// </summary>
public static class NewSeriesHomeSyncTrace
{
	/// <summary>調査ログの有効無効フラグ。</summary>
	private const bool IsEnabled = true;

	/// <summary>現在追跡中のSeriesId。1件だけ保持します。</summary>
	private static long? trackingSeriesId;

	/// <summary>
	/// 新規作品の追跡を開始します。
	/// </summary>
	/// <param name="seriesId">追跡対象のSeriesId。</param>
	public static void Begin(long seriesId)
	{
		if (!IsEnabled)
			return;

		if (seriesId <= 0)
			return;

		trackingSeriesId = seriesId;
	}

	/// <summary>
	/// 新規作品の追跡を終了します。
	/// </summary>
	/// <param name="seriesId">追跡対象のSeriesId。</param>
	public static void End(long seriesId)
	{
		if (!IsEnabled)
			return;

		if (trackingSeriesId == seriesId)
		{
			trackingSeriesId = null;
		}
	}

	/// <summary>
	/// 指定したSeriesIdが追跡対象であるかどうかを判定します。
	/// </summary>
	/// <param name="seriesId">判定対象のSeriesId。</param>
	/// <returns>追跡対象の場合true、それ以外はfalse。</returns>
	public static bool IsTracking(long seriesId)
	{
		if (!IsEnabled)
			return false;

		if (seriesId <= 0)
			return false;

		return trackingSeriesId == seriesId;
	}
}
