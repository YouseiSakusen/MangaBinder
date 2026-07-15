namespace MangaBinder.Series;

/// <summary>
/// 素材フォルダ名の生成処理を集約するHelperクラスです。
/// フォルダ名生成ルール変更時は、このクラスのみ修正してください。
/// </summary>
public static class MaterialFolderNameHelper
{
	/// <summary>
	/// 指定された作品から、フォルダ名生成ルールに従って素材フォルダ名を生成します。
	/// </summary>
	/// <remarks>
	/// フォルダ名生成ルール：
	/// - 連載中：タイトル
	/// - 完結済み且つ全巻所持：タイトル 全{EndVolume}巻
	/// - 完結済み且つ巻抜けあり：タイトル （全{EndVolume}巻）
	/// </remarks>
	/// <param name="series">フォルダ名を生成する作品。</param>
	/// <returns>フォルダ名生成ルールに従って生成されたフォルダ名。</returns>
	public static string Create(MangaSeries series)
	{
		if (series == null)
			throw new ArgumentNullException(nameof(series));

		// 連載中
		if (!series.SeriesCompleted)
		{
			return series.Title;
		}

		// 完結済み + 全巻所持
		if (series.IsOwnedCompleted)
		{
			return $"{series.Title} 全{series.EndVolume}巻";
		}

		// 完結済み + 巻抜けあり
		return $"{series.Title} （全{series.EndVolume}巻）";
	}
}
