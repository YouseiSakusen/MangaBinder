using System.Text;
using MangaBinder;
using MangaBinder.Bindings;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Jobs.FolderScanners;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// <see cref="BindingFolderScanner"/> の動作検証テストクラスです。
/// スタブリポジトリを使用して、実際のフォルダを走査しCSVへ出力します。
/// </summary>
public class BindingFolderScannerTest
{
    /// <summary>
    /// スキャン結果をCSVに出力し、パスをコンソールへ表示します。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_製本フォルダをスキャンしてCSVに出力する()
    {
        var stub = new StubFolderRepository();
        var context = new WorkerContext { ConnectionString = string.Empty, DatabasePath = string.Empty, SupportedExtensions = [], ThumbnailFolderPath = string.Empty, ThumbnailOptions = new() };
        SupportedExtensionHelper.Initialize(context.SupportedExtensions);

        var services = new ServiceCollection();
        services.AddSingleton<IFolderScannerRepository>(stub);
        services.AddScoped<MangaBinder.Bindings.SeriesExtractorFactory>();
        services.AddScoped<ThumbnailCreator>(sp => new ThumbnailCreator(
            context,
            sp.GetRequiredService<MangaBinder.Bindings.SeriesExtractorFactory>(),
            new StubImageProcessor(),
            NullLogger<ThumbnailCreator>.Instance));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var scanner = new BindingFolderScanner(scopeFactory, context, NullLogger<BindingFolderScanner>.Instance);

        await scanner.ExecuteAsync(CancellationToken.None);
        await stub.WriteCsvAsync(CancellationToken.None);

        Console.WriteLine($"CSV出力先: {stub.OutputCsvPath}");
    }
}

/// <summary>
/// テスト用の <see cref="IFolderScannerRepository"/> スタブです。
/// スキャン対象パスを返し、名寄せ後の結果をCSVファイルに保存します。
/// </summary>
file class StubFolderRepository : IFolderScannerRepository
{
    /// <summary>スキャン対象のルートフォルダパスです。検証したいパスに変更してください。</summary>
    private readonly string scanRootPath = @"D:\ZipComics";

    /// <summary>保存された作品の蓄積リストです。</summary>
    private readonly List<MangaSeries> seriesList = [];

    /// <summary>CSV出力先のフルパスです。</summary>
    public string OutputCsvPath { get; } =
        Path.Combine(
            @"D:\GitBares\MangaBinder\MangaBinder.Worker.Tests\bin\Debug\net10.0-windows\ScanResults",
            $"Binding_Scan_Result_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

    /// <summary>
    /// 検証用のスキャン対象フォルダパスを返します。
    /// </summary>
    /// <param name="role">フォルダの役割（未使用）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>スキャン対象パスを1件含むコレクション。</returns>
    public ValueTask<IEnumerable<string>> GetSourceFoldersAsync(int role, CancellationToken ct)
        => ValueTask.FromResult<IEnumerable<string>>([this.scanRootPath]);

    /// <summary>
    /// 受け取った <see cref="MangaSeries"/> を蓄積リストに追加します。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public ValueTask<MangaSeries> SaveBindingSeriesAsync(MangaSeries series, CancellationToken ct)
    {
        this.seriesList.Add(series);
        return ValueTask.FromResult(series);
    }

    /// <summary>素材スキャンはこのテストでは使用しません。</summary>
    public ValueTask<MangaSeries> SaveMaterialSeriesAsync(MangaSeries series, CancellationToken ct)
        => throw new NotImplementedException();

    /// <summary>サムネイル更新はこのテストでは記録しません。</summary>
    public ValueTask UpdateThumbnailAsync(long seriesId, string thumbnailFileName, ThumbnailStatus thumbnailStatus, CancellationToken ct)
        => ValueTask.CompletedTask;

    /// <summary>このテストでは常に false を返します。</summary>
    public ValueTask<bool> HasLimitExceededAsync(CancellationToken ct)
        => ValueTask.FromResult(false);

    /// <summary>
    /// 蓄積した <see cref="MangaSeries"/> リストをCSVファイルに書き出します。
    /// Sources 列には統合された全ファイルのフルパスをセミコロン連結で出力します。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async ValueTask WriteCsvAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(this.OutputCsvPath)!);

        var sb = new StringBuilder();
        sb.AppendLine("Title,ShortTitle,NormalizedTitleInternal,Author,StartVolume,EndVolume,BoundEndVolume,SeriesCompleted,IsOwnedCompleted,Sources");

        foreach (var s in this.seriesList)
        {
            var sources = string.Join(";", s.Sources.Select(src => src.Path));

            sb.AppendLine(string.Join(",",
                Escape(s.Title),
                Escape(s.ShortTitle),
                Escape(s.NormalizedTitleInternal),
                Escape(s.Author),
                s.StartVolume,
                s.EndVolume,
                s.BoundEndVolume,
                s.SeriesCompleted,
                s.IsOwnedCompleted,
                Escape(sources)));
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
/// テスト用の <see cref="MangaBinder.Bindings.IThumbnailImageProcessor"/> スタブです。
/// 画像処理は行わず、空のストリームを返します。
/// </summary>
file class StubImageProcessor : MangaBinder.Bindings.IThumbnailImageProcessor
{
    public ValueTask<Stream> ProcessThumbnailAsync(Stream input, MangaBinder.Settings.ThumbnailOptions options, CancellationToken ct)
        => ValueTask.FromResult<Stream>(new MemoryStream());
}
