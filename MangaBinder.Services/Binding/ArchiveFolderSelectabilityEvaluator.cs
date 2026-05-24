using SharpCompress.Archives;
using System.Text.RegularExpressions;

namespace MangaBinder.Binding;

/// <summary>
/// Archive 内フォルダが製本対象として選択可能かどうかを評価するクラスです。
/// </summary>
public static partial class ArchiveFolderSelectabilityEvaluator
{
	/// <summary>
	/// 指定した Archive 内フォルダが製本対象として選択可能かどうかを評価します。
	/// </summary>
	/// <param name="folderKey">Archive 内フォルダのエントリ接頭辞（例: "vol01" or "vol01/chapter01"）。</param>
	/// <param name="archivePath">Archive ファイルのパス。</param>
	/// <param name="archive">開済みの Archive インスタンス。</param>
	/// <returns>選択可能な場合は <see langword="true"/>、不可の場合は <see langword="false"/>。</returns>
	public static bool Evaluate(string folderKey, string archivePath, IArchive archive)
	{
		var folderName = folderKey.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
			.LastOrDefault() ?? string.Empty;

		if (IsMultiVolumeRange(folderName))
			return false;

		return true;
	}

	/// <summary>
	/// フォルダ名が複数巻範囲を表しているかどうかを判定します。
	/// </summary>
	/// <param name="folderName">判定対象のフォルダ名。</param>
	/// <returns>複数巻範囲を表している場合は <see langword="true"/>。</returns>
	public static bool IsMultiVolumeRange(string folderName)
		=> MultiVolumeRangePattern().IsMatch(folderName);

	/// <summary>
	/// 複数巻範囲を表すフォルダ名にマッチする正規表現です。
	/// 例: "07-09", "01-03", "1-3", "1～3", "1〜3", "第1-3巻", "vol 01-03", "v07-09s"
	/// </summary>
	[GeneratedRegex(
		@"(?:
			\d+\s*[-–—~～〜]\s*\d+   # 数値-数値 or 数値～数値
		)",
		RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase)]
	private static partial Regex MultiVolumeRangePattern();
}
