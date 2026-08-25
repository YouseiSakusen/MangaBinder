using System.Collections.Generic;
using System.IO;

namespace MangaBinder.Series;

/// <summary>
/// 素材フォルダ内のファイル名・サブフォルダ名から手持ちの最大巻数を推定するクラスです。
/// </summary>
public class OwnedVolumeEstimator
{
    /// <summary>
    /// ファイル名・フォルダ名の一覧から手持ちの最大巻数を推定します。
    /// </summary>
    /// <param name="entryNames">ファイル名・フォルダ名の一覧。作品フォルダ直下のエントリ名を想定しています。</param>
    /// <returns>推定結果。推定できない場合は <see cref="OwnedVolumeEstimateResult.OwnedMaxVolume"/> が 0。</returns>
    public OwnedVolumeEstimateResult Estimate(IEnumerable<string> entryNames)
    {
        var entries = entryNames.ToList();

        var allCandidates = new List<OwnedVolumeEstimateCandidate>();
        var decidedVolumes = new List<int>();

        // 各素材ごとに候補から最適な1つを選別
        foreach (var name in entries)
        {
            var candidates = OwnedVolumeCandidateExtractor.Extract(name);
            allCandidates.AddRange(candidates);

            // この素材内で最高Priorityの候補を見つける
            if (candidates.Count > 0)
            {
                var maxPriority = candidates.Max(c => c.Priority);
                // 同じ最高Priorityの候補の中から最大Volumeを採用
                var decidedVolume = candidates
                    .Where(c => c.Priority == maxPriority)
                    .Max(c => c.Volume);
                decidedVolumes.Add(decidedVolume);
            }
        }

        // 各素材で確定した巻数の最大値を採用
        var maxVolume = decidedVolumes.Count > 0 ? decidedVolumes.Max() : 0;

        return new OwnedVolumeEstimateResult
        {
            OwnedMaxVolume = maxVolume,
            TargetCount = entries.Count,
            Candidates = allCandidates,
        };
    }

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
        var entryNames = entries.Select(e => e.Name).ToList();

        return this.Estimate(entryNames);
    }
}
