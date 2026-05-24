using MangaBinder;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 製本済みフォルダをスキャンするジョブです。
/// アーカイブファイル（.zip / .cbz / .rar / .7z）を走査対象とします。
/// </summary>
public class BindingFolderScanner : FolderScannerBase
{
    /// <summary>走査対象とするアーカイブ拡張子のセットです。</summary>
    private static readonly HashSet<string> ArchiveExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".zip", ".cbz", ".rar", ".7z" };

    /// <summary>タイトル区切り文字群。</summary>
    private readonly string titleSeparatorChars;

    /// <summary>
    /// <see cref="BindingFolderScanner"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="scopeFactory">スコープファクトリ。</param>
    /// <param name="workerContext">Worker 実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    public BindingFolderScanner(
        IServiceScopeFactory scopeFactory,
        WorkerContext workerContext,
        ILogger<BindingFolderScanner> logger)
        : base(scopeFactory, logger, FolderRole.Binding, workerContext)
    {
        this.titleSeparatorChars = workerContext.TitleSeparatorChars;
    }

    /// <summary>
    /// 指定されたルートパス直下のアーカイブファイル一覧を返します。
    /// </summary>
    /// <param name="rootPath">スキャン対象のルートフォルダパス。</param>
    /// <returns>アーカイブファイルの <see cref="FileInfo"/> 一覧。</returns>
    protected override IEnumerable<FileSystemInfo> GetScanItems(string rootPath)
        => new DirectoryInfo(rootPath)
            .GetFiles()
            .Where(f => ArchiveExtensions.Contains(f.Extension));

    /// <summary>
    /// ファイル名を解析して <see cref="MangaSeries"/> を生成します。
    /// <see cref="MangaSeries.Sources"/> にファイルの所在情報を追加します。
    /// </summary>
    /// <param name="info">解析対象のアーカイブファイル情報。</param>
    /// <returns>解析結果の <see cref="MangaSeries"/>。</returns>
    protected override MangaSeries ParseToSeries(FileSystemInfo info)
    {
        var series = MangaTitleHelper.ParseAsBinding(info.Name, this.titleSeparatorChars);
        series.Sources.Add(new MangaSource
        {
            Role = FolderRole.Binding,
            Path = info.FullName,
        });
        return series;
    }
    /// <summary>
    /// 全ルートパスを走査して名寄せ（集約）し、1 件ずつ保存します。
    /// </summary>
    /// <param name="rootPaths">スキャン対象のルートフォルダパス一覧。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>保存件数。</returns>
    protected override async ValueTask<int> ScanAndSaveAsync(IEnumerable<string> rootPaths, CancellationToken ct)
    {
        var dict = new Dictionary<string, MangaSeries>(StringComparer.Ordinal);

        foreach (var rootPath in rootPaths)
        {
            this.logger.ZLogInformation($"ルートパス走査: {rootPath}");

            foreach (var item in this.GetScanItems(rootPath))
            {
                var parsed = this.ParseToSeries(this.EnsurePhysicalNormalization(item));
                var key = parsed.NormalizedTitleInternal;

                if (!dict.TryGetValue(key, out var existing))
                {
                    dict[key] = parsed;
                    continue;
                }

                dict[key] = new MangaSeries
                {
                    Title                  = existing.Title,
                    NormalizedTitleInternal = existing.NormalizedTitleInternal,
                    NormalizedTitleExternal = existing.NormalizedTitleExternal,
                    Author                 = existing.Author.Length > 0 ? existing.Author : parsed.Author,
                    StartVolume            = Math.Min(existing.StartVolume, parsed.StartVolume),
                    BoundEndVolume         = Math.Max(existing.BoundEndVolume, parsed.BoundEndVolume),
                    EndVolume              = Math.Max(existing.EndVolume, parsed.EndVolume),
                    SeriesCompleted        = existing.SeriesCompleted || parsed.SeriesCompleted,
                    IsOwnedCompleted       = existing.IsOwnedCompleted || parsed.IsOwnedCompleted,
                    ShortTitle             = existing.ShortTitle,
                    ThumbnailFileName      = existing.ThumbnailFileName,
                };
                dict[key].Sources.AddRange(existing.Sources);
                dict[key].Sources.AddRange(parsed.Sources);
            }
        }

        // 保存フェーズ：スコープを明示的に生成してサービスを解決
        using var scope = this.scopeFactory.CreateScope();
        var repository       = scope.ServiceProvider.GetRequiredService<IFolderScannerRepository>();
        var thumbnailCreator = scope.ServiceProvider.GetRequiredService<ThumbnailCreator>();

        var savedCount = 0;

        await Parallel.ForEachAsync(dict.Values, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (series, token) =>
        {
            var savedSeries = await this.SaveResultsAsync(series, repository, token);
            if (this.HasCompletedThumbnail(savedSeries))
            {
                this.logger.ZLogInformation($"サムネイル作成済みのためスキップ: {savedSeries.Title}");
                Interlocked.Increment(ref savedCount);
                return;
            }
            var result = await thumbnailCreator.CreateAsync(savedSeries, this.SkipThumbnailSizeLimit, token);
            await repository.UpdateThumbnailAsync(savedSeries.SeriesId, result.ThumbnailFileName, result.Status, token);
            Interlocked.Increment(ref savedCount);
        });

        return savedCount;
    }

    /// <summary>
    /// 製本済みスキャン結果をリポジトリ経由で保存し、DB最新状態の <see cref="MangaSeries"/> を返します。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="repository">保存先リポジトリ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaSeries"/>。</returns>
    protected override ValueTask<MangaSeries> SaveResultsAsync(MangaSeries series, IFolderScannerRepository repository, CancellationToken ct)
        => repository.SaveBindingSeriesAsync(series, ct);
}
