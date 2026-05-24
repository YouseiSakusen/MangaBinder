namespace MangaBinder.Bindings;

/// <summary>
/// サムネイル作成結果を表します。
/// </summary>
/// <param name="Status">サムネイル処理の状態。</param>
/// <param name="ThumbnailFileName">作成されたサムネイルファイル名。</param>
public sealed record ThumbnailCreationResult(
    ThumbnailStatus Status,
    string ThumbnailFileName);
