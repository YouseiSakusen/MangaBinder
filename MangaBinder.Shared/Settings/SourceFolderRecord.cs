namespace MangaBinder.Settings;

/// <summary>
/// SourceFolders テーブルの1行を表す不変レコードです。
/// </summary>
/// <param name="FolderPath">フォルダの絶対パス。</param>
/// <param name="DisplayName">フォルダの表示名。</param>
/// <param name="Role">フォルダの役割。</param>
public record SourceFolderRecord(string FolderPath, string DisplayName, FolderRole Role);
