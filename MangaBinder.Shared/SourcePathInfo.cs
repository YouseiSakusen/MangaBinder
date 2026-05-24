using MangaBinder.Settings;

namespace MangaBinder;

/// <summary>
/// 作品の所在情報を表すクラスです。
/// </summary>
public class SourcePathInfo
{
    /// <summary>親の <see cref="MangaSeries"/> との紐付け用IDです。</summary>
    public long SeriesId { get; init; }

    /// <summary>フォルダの役割です。</summary>
    public FolderRole Role { get; init; }

    /// <summary>物理フルパスです。</summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>パース前の元の名前です。</summary>
    public string RawName { get; init; } = string.Empty;
}
