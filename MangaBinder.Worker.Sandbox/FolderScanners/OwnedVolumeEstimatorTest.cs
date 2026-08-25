using System.Diagnostics;
using System.Text;
using MangaBinder.Series;

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
    /// ケース4：高Priorityの既存素材より、新しい低Priority素材の巻数が大きい。
    /// 入力：v01-09.rar と [イトノコ×蔵人幸明] まりも兄弟の茶飯事 第10巻.rar
    /// 期待：OwnedMaxVolume = 10
    /// Priority の適用は素材内だけで、素材間では比較しないこと
    /// </summary>
    [Fact]
    public void Estimate_高Priorityの素材より新しい低Priority素材の巻数が大きい場合正しく採用する()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 既存素材：v01-09（Priority 高）
            File.Create(Path.Combine(tempDir, "Example v01-09.rar")).Dispose();
            // 新素材：第10巻（Priority 低いが、巻数が大きい）
            File.Create(Path.Combine(tempDir, "[イトノコ×蔵人幸明] まりも兄弟の茶飯事 第10巻.rar")).Dispose();

            var estimator = new OwnedVolumeEstimator();

            // Act
            var result = estimator.Estimate(tempDir);

            // Assert
            Assert.Equal(10, result.OwnedMaxVolume);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// ケース5：実例。v/vol系の高Priority素材と第n巻形式の低Priority素材を混在させても、
    /// 低Priority形式でより大きい巻数が見つかった場合は、それを採用すること。
    /// 入力：複数の第n巻形式ファイル
    /// 期待：OwnedMaxVolume = 28（第28巻が最大）
    /// </summary>
    [Fact]
    public void Estimate_実例の第n巻形式と既存形式の混在で正しく採用する()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 第n巻形式の素材（複数）
            File.Create(Path.Combine(tempDir, "[びび×五示正司] ひとりぼっちの異世界攻略 第28巻.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "[すかいふぁーむ×高幡隆盛] 俺だけ不遇スキルの異世界召喚叛逆記 第17巻.rar")).Dispose();
            File.Create(Path.Combine(tempDir, "[にことがめ] ヒト科のゆいか 第04巻.rar")).Dispose();

            var estimator = new OwnedVolumeEstimator();

            // Act
            var result = estimator.Estimate(tempDir);

            // Assert
            Assert.Equal(28, result.OwnedMaxVolume);
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
