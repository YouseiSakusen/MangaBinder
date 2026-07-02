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
    /// ケース1：タイトル中の999を巻数扱いしない。
    /// 入力：その劣等騎士、レベル９９９の全角括弧数字パターン3ファイル
    /// 期待：OwnedMaxVolume = 3
    /// </summary>
    [Fact]
    public void Estimate_タイトル中の999を無視して括弧数字を優先する()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // ファイルを作成（空ファイル）
            File.Create(Path.Combine(tempDir, "その劣等騎士、レベル９９９ (1).epub")).Dispose();
            File.Create(Path.Combine(tempDir, "その劣等騎士、レベル９９９ (2).epub")).Dispose();
            File.Create(Path.Combine(tempDir, "その劣等騎士、レベル９９９ (3).epub")).Dispose();

            var estimator = new OwnedVolumeEstimator();

            // Act
            var result = estimator.Estimate(tempDir);

            // Assert
            Assert.Equal(3, result.OwnedMaxVolume);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// ケース2：ローマ字タイトル中の999を無視してv形式を採用。
    /// 入力：Sono Retto Kishi Reberu 999 v03～v08-09のファイル
    /// 期待：OwnedMaxVolume = 9
    /// </summary>
    [Fact]
    public void Estimate_ローマ字タイトル中の999を無視してv形式を優先する()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // ファイルを作成（空ファイル）
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v03.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v04.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v05.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v06.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v07 DL.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v08-09 DL.rar")).Dispose();

            var estimator = new OwnedVolumeEstimator();

            // Act
            var result = estimator.Estimate(tempDir);

            // Assert
            Assert.Equal(9, result.OwnedMaxVolume);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// ケース3：混在時も999ではなく最大巻を採用。
    /// 入力：vol形式、v形式、括弧形式が混在
    /// 期待：OwnedMaxVolume = 9
    /// </summary>
    [Fact]
    public void Estimate_混在時も999ではなく最大巻を採用する()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // ファイルを作成（空ファイル）
            File.Create(Path.Combine(tempDir, "DLRAW.NET-Sono Retto Kishi Reberu 999 vol 01-07.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v01-02ss.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v03.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v04.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v05.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v06.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v07 DL.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "Sono Retto Kishi Reberu 999 v08-09 DL.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "その劣等騎士、レベル９９９ (1).epub")).Dispose();
            File.Create(Path.Combine(tempDir, "その劣等騎士、レベル９９９ (2).epub")).Dispose();
            File.Create(Path.Combine(tempDir, "その劣等騎士、レベル９９９ (3).epub")).Dispose();

            var estimator = new OwnedVolumeEstimator();

            // Act
            var result = estimator.Estimate(tempDir);

            // Assert
            Assert.Equal(9, result.OwnedMaxVolume);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
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
