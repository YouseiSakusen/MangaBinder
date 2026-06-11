using MangaBinder.Settings;

namespace MangaBinder;

/// <summary>
/// DBの MangaSources テーブルと対応する作品の所在情報を表すクラスです。
/// </summary>
public class MangaSource
{
    /// <summary>MangaSources テーブルの主キーです。</summary>
    public long SourceId { get; init; }

    /// <summary>親の <see cref="MangaSeries"/> との紐付け用IDです。</summary>
    public long SeriesId { get; init; }

    /// <summary>フォルダの物理フルパスです。</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>フォルダの役割を表す値です。</summary>
    public FolderRole Role { get; init; }
}
