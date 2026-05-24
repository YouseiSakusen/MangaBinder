namespace MangaBinder.Jobs;

/// <summary>
/// ジョブの実行インターフェースです。
/// </summary>
public interface IJob
{
    /// <summary>
    /// サムネイルサイズ制限をスキップするかどうか。
    /// </summary>
    bool SkipThumbnailSizeLimit { get; set; }

    /// <summary>
    /// ジョブを非同期で実行します。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    ValueTask ExecuteAsync(CancellationToken ct);
}
