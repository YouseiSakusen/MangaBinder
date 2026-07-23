namespace MangaBinder.Helpers;

/// <summary>
/// アーカイブエントリのフィルタリングを行う静的ヘルパークラスです。
/// </summary>
public static class ArchiveEntryHelper
{
    /// <summary>
    /// 指定されたエントリキーが除外対象かどうかを判定します。
    /// </summary>
    /// <param name="key">エントリキー（パス）。</param>
    /// <returns>除外対象の場合は <c>true</c>。</returns>
    public static bool IsIgnoredEntry(string? key)
    {
        if (key is null)
            return true;

        var normalized = key.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);

        // ① __MACOSX 配下
        if (normalized.Contains("__MACOSX/"))
            return true;

        // ② AppleDouble ファイル
        if (fileName.StartsWith("._"))
            return true;

        // ③ macOS メタファイル
        if (fileName == ".DS_Store")
            return true;

        // ④ Windows メタファイル
        if (fileName == "Thumbs.db" || fileName == "desktop.ini")
            return true;

        return false;
    }
}
