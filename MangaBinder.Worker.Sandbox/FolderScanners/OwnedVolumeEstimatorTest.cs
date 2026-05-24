using System.Diagnostics;
using System.Text;
using MangaBinder.Jobs.FolderScanners;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// <see cref="OwnedVolumeEstimator"/> の精度確認を行う Sandbox テストクラスです。
/// 実際の素材フォルダを走査して CSV へ出力します。
/// </summary>
public class OwnedVolumeEstimatorTest
{
    /// <summary>スキャン対象のルートフォルダパスです。</summary>
    private readonly string scanRootPath = @"D:\My Comic\!src";

    /// <summary>CSV出力先フォルダのパスです。</summary>
    private const string OutputFolder =
        @"D:\GitBares\MangaBinder\MangaBinder.Worker.Tests\bin\Debug\net10.0-windows\ScanResults";

    /// <summary>
    /// 素材フォルダ直下を走査し、手持ち最大巻数の推定結果を CSV に出力します。
    /// </summary>
    [Fact]
    public async Task Estimate_素材フォルダを走査してCSV出力する()
    {
        var outputCsvPath = Path.Combine(OutputFolder, $"OwnedVolumeEstimate_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        Directory.CreateDirectory(OutputFolder);

        var estimator = new OwnedVolumeEstimator();
        var folders = Directory.GetDirectories(scanRootPath);

        var sb = new StringBuilder();
        sb.AppendLine("FolderName,OwnedMaxVolume,TargetCount,Candidates");

        var sw = Stopwatch.StartNew();

        foreach (var folderPath in folders)
        {
            var folderName = Path.GetFileName(folderPath);
            var estimate = estimator.Estimate(folderPath);

            var candidatesText = estimate.Candidates.Count == 0
                ? string.Empty
                : string.Join(" | ", estimate.Candidates.Select(c => $"{c.PatternName}:{c.Volume}:{c.Name}"));

            sb.AppendLine(string.Join(",",
                Escape(folderName),
                estimate.OwnedMaxVolume,
                estimate.TargetCount,
                Escape(candidatesText)));
        }

        sw.Stop();

        await File.WriteAllTextAsync(outputCsvPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Console.WriteLine($"CSV出力先: {outputCsvPath}");
        Console.WriteLine($"対象フォルダ数: {folders.Length}");
        Console.WriteLine($"実行時間（秒）: {sw.Elapsed.TotalSeconds:F3}");
    }

    /// <summary>
    /// CSV用にフィールド値をエスケープします。カンマ・改行・ダブルクォートを含む場合はダブルクォートで囲みます。
    /// </summary>
    /// <param name="value">エスケープ対象の文字列。</param>
    /// <returns>エスケープ済みの文字列。</returns>
    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
