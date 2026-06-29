using SharpCompress.Archives;

namespace MangaBinder.Bindings;

/// <summary>
/// Archive 内フォルダが製本対象として選択可能かどうかを評価するクラスです。
/// </summary>
public static partial class ArchiveFolderSelectabilityEvaluator
{
	/// <summary>
	/// 指定した Archive 内フォルダが製本対象として選択可能かどうかを評価します。
	/// 直下に画像ファイルが存在するか（fileCount > 0）で判定します。
	/// </summary>
	/// <param name="fileCount">フォルダ直下の画像ファイル数。</param>
	/// <returns>選択可能な場合は <see langword="true"/>、不可の場合は <see langword="false"/>。</returns>
	public static bool Evaluate(int fileCount)
	{
		return fileCount > 0;
	}
}
