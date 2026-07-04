using System.Diagnostics;
using System.Text;
using MangaBinder.Bindings;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Jobs.FolderScanners;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// <see cref="MaterialFolderScanner"/> の動作検証テストクラスです。
/// スタブリポジトリを使用して、実際の素材フォルダを走査しCSVへ出力します。
/// </summary>
public class MaterialFolderScannerTest
{
    private const string ThumbnailFolderPath = @"D:\GitBares\MangaBinder\MangaBinder.Worker.Tests\bin\Debug\net10.0-windows\thumbnails";

	/// <summary>
	/// スキャン結果をCSVに出力し、パスをコンソールへ表示します。
	/// </summary>
	[Fact]
    public async Task ExecuteAsync_素材フォルダをスキャンしてCSVに出力する()
    {
        var stub = new StubMaterialRepository();
        var context = new WorkerContext
        {
            ConnectionString = string.Empty,
            DatabasePath = string.Empty,
			SupportedExtensions =
			[
				new() { Extension = ".jpg",  FileType = (int)FileType.Image },
				new() { Extension = ".jpeg", FileType = (int)FileType.Image },
				new() { Extension = ".png",  FileType = (int)FileType.Image },
				new() { Extension = ".webp", FileType = (int)FileType.Image },
				new() { Extension = ".avif", FileType = (int)FileType.Image },
				new() { Extension = ".gif",  FileType = (int)FileType.Image },
				new() { Extension = ".zip",  FileType = (int)FileType.Archive },
				new() { Extension = ".rar",  FileType = (int)FileType.Archive },
				new() { Extension = ".cbz",  FileType = (int)FileType.Archive },
				new() { Extension = ".epub", FileType = (int)FileType.Epub },
			],
			ThumbnailExtractLimitFileSizeMB = 500,
				ThumbnailOptions = new() { Width = 160, Height = 224 },
			ThumbnailFolderPath = ThumbnailFolderPath,
		};
		SupportedExtensionHelper.Initialize(context.SupportedExtensions);

		var services = new ServiceCollection();
        services.AddSingleton<IFolderScannerRepository>(stub);
        services.AddScoped<MangaBinder.Bindings.SeriesExtractorFactory>();
        services.AddKeyedScoped<MangaBinder.Bindings.IThumbnailExtractor, MangaBinder.Bindings.ArchiveThumbnailExtractor>(FileType.Image);
        services.AddKeyedScoped<MangaBinder.Bindings.IThumbnailExtractor, MangaBinder.Bindings.ArchiveThumbnailExtractor>(FileType.Archive);
        services.AddKeyedScoped<MangaBinder.Bindings.IThumbnailExtractor, MangaBinder.Bindings.EpubThumbnailExtractor>(FileType.Epub);
        services.AddScoped<ThumbnailCreator>(sp => new ThumbnailCreator(
            context,
            sp.GetRequiredService<MangaBinder.Bindings.SeriesExtractorFactory>(),
            new ThumbnailImageProcessor(),
            NullLogger<ThumbnailCreator>.Instance));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var scanner = new MaterialFolderScanner(scopeFactory, context, NullLogger<MaterialFolderScanner>.Instance);

        var sw = Stopwatch.StartNew();
        await scanner.ExecuteAsync(CancellationToken.None);
        sw.Stop();
        await stub.WriteCsvAsync(CancellationToken.None);

        Console.WriteLine($"CSV出力先: {stub.OutputCsvPath}");
        Console.WriteLine($"実行時間（秒）: {sw.Elapsed.TotalSeconds:F3}");
    }
}

/// <summary>
/// テスト用の <see cref="IFolderScannerRepository"/> スタブです。
/// 素材フォルダのスキャン対象パスを返し、結果をCSVファイルに保存します。
/// </summary>
file class StubMaterialRepository : IFolderScannerRepository
{
    /// <summary>スキャン対象のルートフォルダパスです。検証したいパスに変更してください。</summary>
    private readonly string scanRootPath = @"D:\My Comic\!src";

    /// <summary>保存された作品の萁積リストです。</summary>
    private readonly List<MangaSeries> seriesList = [];

    /// <summary>採番用IDカウンターです。</summary>
    private int _id = 1;

    /// <summary>CSV出力先のフルパスです。</summary>
    public string OutputCsvPath { get; } =
        Path.Combine(
            @"D:\GitBares\MangaBinder\MangaBinder.Worker.Tests\bin\Debug\net10.0-windows\ScanResults",
            $"Material_Scan_Result_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

    /// <summary>
    /// 検証用のスキャン対象フォルダパスを返します。
    /// </summary>
    /// <param name="role">フォルダの役割（未使用）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>スキャン対象パスを1件含むコレクション。</returns>
    public ValueTask<IEnumerable<string>> GetSourceFoldersAsync(int role, CancellationToken ct)
        => ValueTask.FromResult<IEnumerable<string>>([this.scanRootPath]);

    /// <summary>
    /// 受け取った <see cref="MangaSeries"/> を萁積リストに追加します。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public ValueTask<MangaSeries> SaveMaterialSeriesAsync(MangaSeries series, CancellationToken ct)
    {
        series.SeriesId = _id++;
        this.seriesList.Add(series);
        return ValueTask.FromResult(series);
    }

    /// <summary>製本スキャンはこのテストでは使用しません。</summary>
    public ValueTask<MangaSeries> SaveBindingSeriesAsync(MangaSeries series, CancellationToken ct)
        => throw new NotImplementedException();

    /// <summary>このテストでは常に false を返します。</summary>
    public ValueTask<bool> HasLimitExceededAsync(CancellationToken ct)
        => ValueTask.FromResult(false);

    /// <summary>このテストでは空の辞書を返します。</summary>
    public ValueTask<Dictionary<long, MangaSource>> GetSourcesByFolderRoleAsync(int role, IEnumerable<string> sourceFolderPaths, CancellationToken ct)
        => ValueTask.FromResult(new Dictionary<long, MangaSource>());

    /// <summary>このテストでは削除は行いません。</summary>
    public ValueTask DeleteSourcesByIdAsync(IEnumerable<long> sourceIds, CancellationToken ct)
        => ValueTask.CompletedTask;

    /// <summary>サムネイル更新はこのテストでは記録しません。</summary>
    public ValueTask UpdateThumbnailAsync(long seriesId, string thumbnailFileName, ThumbnailStatus thumbnailStatus, CancellationToken ct)
        => ValueTask.CompletedTask;

    /// <summary>
    /// 萁積した <see cref="MangaSeries"/> リストをCSVファイルに書き出します。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async ValueTask WriteCsvAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(this.OutputCsvPath)!);

        var sb = new StringBuilder();
        sb.AppendLine("Title,ShortTitle,NormalizedTitleInternal,EndVolume,SeriesCompleted,IsOwnedCompleted,CandidatePath,CandidateSizeMB,CandidateExt");

        foreach (var s in this.seriesList)
        {
            var candidatePath = s.ThumbnailFileName;
            var exists = File.Exists(candidatePath);
            var sizeMb = exists
                ? (new FileInfo(candidatePath).Length / 1024.0 / 1024.0).ToString("F2")
                : "0";
            var ext = exists ? Path.GetExtension(candidatePath) : string.Empty;

            sb.AppendLine(string.Join(",",
                Escape(s.Title),
                Escape(s.ShortTitle),
                Escape(s.NormalizedTitleInternal),
                s.EndVolume,
                s.SeriesCompleted,
                s.IsOwnedCompleted,
                Escape(candidatePath),
                sizeMb,
                Escape(ext)
                // s.ThumbnailProcessingTimeMs
                ));
        }

        await File.WriteAllTextAsync(this.OutputCsvPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
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

/// <summary>
/// テスト用の <see cref="MangaBinder.Bindings.IThumbnailExtractor"/> スタブです。
/// サムネイル抽出は行わず、常に Success を返します。
/// </summary>
file class StubSeriesExtractor : MangaBinder.Bindings.IThumbnailExtractor
{
    public ValueTask<MangaBinder.Bindings.ThumbnailExtractionResult> GetThumbnailImageAsync(string path, CancellationToken ct)
        => ValueTask.FromResult(new MangaBinder.Bindings.ThumbnailExtractionResult
        {
            Status = ExtractionStatus.Success,
            ImageStream = new MemoryStream(),
        });
}

/// <summary>
/// テスト用の <see cref="MangaBinder.IThumbnailImageProcessor"/> スタブです。
/// 画像処理は行わず、空のストリームを返します。
/// </summary>
file class StubImageProcessor : IThumbnailImageProcessor
{
    public ValueTask<Stream> ProcessThumbnailAsync(Stream input, MangaBinder.Settings.ThumbnailOptions options, CancellationToken ct)
        => ValueTask.FromResult<Stream>(new MemoryStream());
}
