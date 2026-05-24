namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 手持ち最大巻数の推定結果を表すモデルです。
/// </summary>
public class OwnedVolumeEstimateResult
{
    /// <summary>推定された手持ちの最大巻数です。推定できない場合は 0 です。</summary>
    public int OwnedMaxVolume { get; init; }

    /// <summary>列挙した直下のファイル・フォルダの数です。</summary>
    public int TargetCount { get; init; }

    /// <summary>抽出された巻数候補の一覧です。</summary>
    public IReadOnlyList<OwnedVolumeEstimateCandidate> Candidates { get; init; } = [];
}
