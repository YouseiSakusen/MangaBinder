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

    /// <summary>フォルダパスから対応する Role へのマッピング。SourceFolder のフルパスをキーとして使用。</summary>
    private Dictionary<string, FolderRole> folderPathToRoleMapping = new();

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
    /// ファイルが属するフォルダから取得元の Role を判定して MangaSource.Role に反映します。
    /// </summary>
    /// <param name="info">解析対象のアーカイブファイル情報。</param>
    /// <returns>解析結果の <see cref="MangaSeries"/>。</returns>
    protected override MangaSeries ParseToSeries(FileSystemInfo info)
    {
        var series = MangaTitleHelper.ParseAsBinding(info.Name, this.titleSeparatorChars);

        // ファイルパスから対応するフォルダパスと Role を特定
        var filePath = info.FullName;
        var sourceFolder = this.folderPathToRoleMapping
            .FirstOrDefault(kvp => filePath.StartsWith(kvp.Key + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                                   filePath.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));

        var role = sourceFolder.Value != default(FolderRole) ? sourceFolder.Value : FolderRole.Binding;

        series.Sources.Add(new MangaSource
        {
            Role = role,
            Path = info.FullName,
        });
        return series;
    }
    /// <summary>
    /// フォルダスキャンを非同期で実行します。
    /// Role=Binding と Role=DefaultBinding の両方をスキャン対象とします。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public override async ValueTask ExecuteAsync(CancellationToken ct)
    {
        this.logger.ZLogInformation($"フォルダスキャン開始: Role=Binding および Role=DefaultBinding");

        using var scope = this.scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFolderScannerRepository>();

        // 両方の Role に対応したフォルダを取得
        var bindingFolders = (await repository.GetSourceFoldersAsync((int)FolderRole.Binding, ct)).ToList();
        var defaultBindingFolders = (await repository.GetSourceFoldersAsync((int)FolderRole.DefaultBinding, ct)).ToList();

        // フォルダパス → Role のマッピングを構築
        this.folderPathToRoleMapping.Clear();
        foreach (var folder in bindingFolders)
        {
            this.folderPathToRoleMapping[folder] = FolderRole.Binding;
        }
        foreach (var folder in defaultBindingFolders)
        {
            this.folderPathToRoleMapping[folder] = FolderRole.DefaultBinding;
        }

        var allRootPaths = bindingFolders.Concat(defaultBindingFolders).ToList();

        // スキャン開始時：対象 SourceFolder 配下の MangaSource を取得してメモリに保持
        // Binding と DefaultBinding 両方のソースを取得対象に
        this.targetSourcesByFolder.Clear();
        var bindingSources = await repository.GetSourcesByFolderRoleAsync((int)FolderRole.Binding, allRootPaths, ct);
        var defaultBindingSources = await repository.GetSourcesByFolderRoleAsync((int)FolderRole.DefaultBinding, allRootPaths, ct);

        foreach (var kvp in bindingSources)
        {
            this.targetSourcesByFolder[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in defaultBindingSources)
        {
            this.targetSourcesByFolder[kvp.Key] = kvp.Value;
        }

        this.logger.ZLogInformation($"スキャン対象フォルダ配下に存在する MangaSource: {this.targetSourcesByFolder.Count} 件");

        // 通常スキャン実行
        var savedCount = await this.ScanAndSaveAsync(allRootPaths, ct);

        this.logger.ZLogInformation($"フォルダスキャン完了: {savedCount} 件保存");

        // スキャン終了時：メモリに残っている MangaSource は削除されたと判断し削除
        if (this.targetSourcesByFolder.Count > 0)
        {
            var sourceIdsToDelete = this.targetSourcesByFolder.Keys.ToList();
            this.logger.ZLogInformation($"スキャン対象フォルダから削除された MangaSource を削除: {sourceIdsToDelete.Count} 件");
            await repository.DeleteSourcesByIdAsync(sourceIdsToDelete, ct);
            this.logger.ZLogInformation($"MangaSource 削除完了");
        }
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

            // スキャン中に見つかった MangaSource をメモリから除外
            foreach (var source in savedSeries.Sources.Where(s => (int)s.Role == (int)FolderRole.Binding || (int)s.Role == (int)FolderRole.DefaultBinding))
            {
                var keysToRemove = this.targetSourcesByFolder
                    .Where(kvp => kvp.Value.Path.Equals(source.Path, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var key in keysToRemove)
                {
                    this.targetSourcesByFolder.Remove(key);
                }
            }

            // DB保存完了
            this.logger.ZLogInformation($"作品情報保存完了：{savedSeries.Title}");

            if (this.HasCompletedThumbnail(savedSeries))
            {
                this.logger.ZLogInformation($"サムネイル生成済みのためスキップ");
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
