using System.Collections.Generic;
using System.IO;
using MangaBinder.Helpers;

namespace MangaBinder.Series;

/// <summary>
/// 素材フォルダ内のファイル名・サブフォルダ名から手持ちの最大巻数を推定するクラスです。
/// VolumeNumberHelper を使用して素材ごとに巻情報を解析し、最大巻を決定します。
/// </summary>
public class OwnedVolumeEstimator
{
    /// <summary>
    /// 素材情報（名前と種別）の一覧から手持ちの最大巻数を推定します。
    /// </summary>
    /// <param name="materials">素材情報の一覧（名前と種別）。</param>
    /// <returns>推定結果。推定できない場合は <see cref="OwnedVolumeEstimateResult.OwnedMaxVolume"/> が 0。</returns>
    public OwnedVolumeEstimateResult Estimate(IEnumerable<(string Name, VolumeNumberSourceType SourceType)> materials)
    {
        var materialsList = materials.ToList();
        var allCandidates = new List<OwnedVolumeEstimateCandidate>();
        var decidedVolumes = new List<int>();

        // 各素材ごとに VolumeNumberHelper で解析
        foreach (var (name, sourceType) in materialsList)
        {
            var parseResult = VolumeNumberHelper.Parse(name, sourceType);

            // Single または Range の場合のみ候補とする
            int? volume = null;
            if (parseResult.Kind == VolumeNumberParseKind.Single && parseResult.SingleVolume.HasValue)
            {
                volume = (int)parseResult.SingleVolume.Value;
            }
            else if (parseResult.Kind == VolumeNumberParseKind.Range && parseResult.RangeEnd.HasValue)
            {
                // Range の場合は終了巻を候補とする
                volume = (int)parseResult.RangeEnd.Value;
            }

            // 正の整数のみを候補とする
            if (volume.HasValue && volume.Value > 0)
            {
                allCandidates.Add(new OwnedVolumeEstimateCandidate
                {
                    Name = name,
                    Volume = volume.Value,
                    PatternName = parseResult.MatchedPattern ?? string.Empty,
                });
                decidedVolumes.Add(volume.Value);
            }
        }

        // 各素材で確定した巻数の最大値を採用
        var maxVolume = decidedVolumes.Count > 0 ? decidedVolumes.Max() : 0;

        return new OwnedVolumeEstimateResult
        {
            OwnedMaxVolume = maxVolume,
            TargetCount = materialsList.Count,
            Candidates = allCandidates,
        };
    }

    /// <summary>
    /// 指定された作品フォルダの直下にあるファイル・フォルダから手持ちの最大巻数を推定します。
    /// ファイルシステム上の種別から VolumeNumberSourceType を判定して Estimate に渡します。
    /// </summary>
    /// <param name="seriesFolderPath">作品フォルダのフルパス。</param>
    /// <returns>推定結果。推定できない場合は <see cref="OwnedVolumeEstimateResult.OwnedMaxVolume"/> が 0。</returns>
    public OwnedVolumeEstimateResult Estimate(string seriesFolderPath)
    {
        var dir = new DirectoryInfo(seriesFolderPath);
        if (!dir.Exists)
            return new OwnedVolumeEstimateResult();

        var entries = dir.EnumerateFileSystemInfos().ToList();
        var materials = new List<(string Name, VolumeNumberSourceType SourceType)>();

        foreach (var entry in entries)
        {
            VolumeNumberSourceType sourceType;

            if (entry is DirectoryInfo)
            {
                sourceType = VolumeNumberSourceType.Folder;
            }
            else if (entry is FileInfo fileInfo && IsEpubFile(fileInfo.Name))
            {
                sourceType = VolumeNumberSourceType.Epub;
            }
            else if (entry is FileInfo && IsArchiveFile(entry.Name))
            {
                sourceType = VolumeNumberSourceType.Archive;
            }
            else
            {
                // 対応外ファイルは無視
                continue;
            }

            materials.Add((entry.Name, sourceType));
        }

        return this.Estimate(materials);
    }

    /// <summary>
    /// ファイルが EPUB 形式か判定します。
    /// </summary>
    private static bool IsEpubFile(string fileName)
    {
        return fileName.EndsWith(".epub", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ファイルがアーカイブ形式か判定します。
    /// </summary>
    private static bool IsArchiveFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".rar" or ".zip" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz";
    }
}
