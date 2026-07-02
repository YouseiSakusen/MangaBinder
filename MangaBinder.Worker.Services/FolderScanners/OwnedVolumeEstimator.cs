namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 素材フォルダ内のファイル名・サブフォルダ名から手持ちの最大巻数を推定するクラスです。
/// </summary>
public class OwnedVolumeEstimator
{
    /// <summary>
    /// 指定された作品フォルダの直下にあるファイル名・フォルダ名から手持ちの最大巻数を推定します。
    /// </summary>
    /// <param name="seriesFolderPath">作品フォルダのフルパス。</param>
    /// <returns>推定結果。推定できない場合は <see cref="OwnedVolumeEstimateResult.OwnedMaxVolume"/> が 0。</returns>
    public OwnedVolumeEstimateResult Estimate(string seriesFolderPath)
    {
        var dir = new DirectoryInfo(seriesFolderPath);
        if (!dir.Exists)
            return new OwnedVolumeEstimateResult();

        var entries = dir.EnumerateFileSystemInfos().ToList();

        var allCandidates = new List<OwnedVolumeEstimateCandidate>();
        foreach (var entry in entries)
        {
            var candidates = OwnedVolumeCandidateExtractor.Extract(entry.Name);
            allCandidates.AddRange(candidates);
        }

        var maxVolume = 0;
        if (allCandidates.Count > 0)
        {
            // 最も優先度が高い候補グループのみ取得し、その中から最大巻を選択
            var maxPriority = allCandidates.Max(c => c.Priority);
            maxVolume = allCandidates
                .Where(c => c.Priority == maxPriority)
                .Max(c => c.Volume);
        }

        return new OwnedVolumeEstimateResult
        {
            OwnedMaxVolume = maxVolume,
            TargetCount = entries.Count,
            Candidates = allCandidates,
        };
    }
}
