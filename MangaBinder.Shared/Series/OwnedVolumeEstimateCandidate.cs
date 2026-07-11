namespace MangaBinder.Series;

/// <summary>
/// 巻数候補の抽出結果を表すモデルです。
/// </summary>
public class OwnedVolumeEstimateCandidate
{
    /// <summary>候補が見つかったファイル名またはフォルダ名です。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>抽出された巻数です。</summary>
    public int Volume { get; init; }

    /// <summary>マッチに使用したパターン名です。</summary>
    public string PatternName { get; init; } = string.Empty;

    /// <summary>候補の優先度です。値が大きいほど優先度が高いです。</summary>
    public int Priority { get; init; } = 0;
}
