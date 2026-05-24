namespace MangaBinder.Settings;

/// <summary>
/// サムネイル生成に関する Options パターン用設定クラスです。
/// </summary>
public sealed class ThumbnailOptions
{
    /// <summary>サムネイルの幅（ピクセル）を取得または設定します。</summary>
    public int Width { get; set; }

    /// <summary>サムネイルの高さ（ピクセル）を取得または設定します。</summary>
    public int Height { get; set; }

    /// <summary>JPEG 品質（0〜100）を取得または設定します。</summary>
    public int JpegQuality { get; set; }

    /// <summary>サムネイルの背景色（HTML カラーコード等）を取得または設定します。</summary>
    public string BackgroundColor { get; set; } = string.Empty;
}
