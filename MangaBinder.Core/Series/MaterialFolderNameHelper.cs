using HalationGhost.Utilities;

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
	/// 
	/// 生成されたフォルダ名はサニタイズされ、Windows ファイルシステムの禁則文字が全角文字に変換されます。
	/// </remarks>
	/// <param name="series">フォルダ名を生成する作品。</param>
	/// <returns>フォルダ名生成ルールに従って生成され、サニタイズされたフォルダ名。</returns>
	public static string Create(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		return FileSystemCharSanitizer.Sanitize(series.MaterialFolderName);
	}
}
