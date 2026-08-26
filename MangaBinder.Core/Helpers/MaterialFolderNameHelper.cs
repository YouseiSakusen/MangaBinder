using HalationGhost.Utilities;

namespace MangaBinder.Helpers;

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
		return Create(series, false);
	}

	/// <summary>
	/// 指定された作品から、フォルダ名生成ルールに従って素材フォルダ名を生成します。
	/// Author プレフィックスの付加の有無を指定できます。
	/// </summary>
	/// <remarks>
	/// フォルダ名生成ルール：
	/// - 連載中：タイトル
	/// - 完結済み且つ全巻所持：タイトル 全{EndVolume}巻
	/// - 完結済み且つ巻抜けあり：タイトル （全{EndVolume}巻）
	/// 
	/// includeAuthorPrefix が true の場合、生成されたフォルダ名の先頭に [作者] を付加します。
	/// 形式：[作者] {従来のフォルダ名}
	/// 
	/// 生成されたフォルダ名全体はサニタイズされ、Windows ファイルシステムの禁則文字が全角文字に変換されます。
	/// </remarks>
	/// <param name="series">フォルダ名を生成する作品。</param>
	/// <param name="includeAuthorPrefix">Author プレフィックスを付加するか。</param>
	/// <returns>フォルダ名生成ルールに従って生成され、サニタイズされたフォルダ名。</returns>
	/// <exception cref="InvalidOperationException">includeAuthorPrefix が true であるにもかかわらず、series.Author が null、空文字、または空白のみの場合にスローされます。</exception>
	public static string Create(MangaSeries series, bool includeAuthorPrefix)
	{
		ArgumentNullException.ThrowIfNull(series);

		string folderName;

		if (includeAuthorPrefix)
		{
			// Author が空の場合は例外をthrow
			if (string.IsNullOrWhiteSpace(series.Author))
			{
				throw new InvalidOperationException("Author プレフィックスの付加が要求されましたが、Author が空です。");
			}

			// [作者] を先頭へ付加
			folderName = $"[{series.Author.Trim()}] {series.MaterialFolderName}";
		}
		else
		{
			folderName = series.MaterialFolderName;
		}

		return FileSystemCharSanitizer.Sanitize(folderName);
	}
}

