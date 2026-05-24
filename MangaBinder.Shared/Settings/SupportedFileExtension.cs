namespace MangaBinder.Settings;

/// <summary>
/// SupportedFileExtensions テーブルの1行を表す POCO クラスです。
/// </summary>
public class SupportedFileExtension
{
    /// <summary>ファイル拡張子を取得または設定します。</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>ファイル種別を取得または設定します。</summary>
    public int FileType { get; set; }

    /// <summary>製本前に既定フォーマットへの変換が必要かどうかを取得します。</summary>
    public bool RequiresConversion { get; set; }
}
