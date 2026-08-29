using System.Collections.Concurrent;
using MangaBinder.Helpers;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 製本済みフォルダをスキャンするジョブです。
/// アーカイブファイル（.zip / .cbz / .rar / .7z）を走査対象とします。
///
/// 実装の流れ：
/// 1. 全物理BindingアーカイブをParseToSeries()で個別に解析
/// 2. 各ファイルのPathが既存MangaSourceと一致するか確認（Path優先）
/// 3. Path未登録ファイルについてGetCandidateSeriesByNormalizedTitlesAsync(...)でスナップショット取得
/// 4. 候補件数に応じた判定：0件→NormalizedTitle+Author集約、1件→ID採用、2件以上→Author補助判定
/// 5. AmbiguousSeriesMatchExceptionは個別catchしてログ・除外、残りは継続
/// 6. SeriesId/新規グループ単位で全ファイルを集約
/// 7. Parallel.ForEachAsyncでInsertBindingSeriesAsync/UpdateBindingSeriesAsync呼び出し
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
        var series = FileSystemNameHelper.ParseAsBinding(info.Name, this.titleSeparatorChars);

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
    /// 全ルートパスを走査し、以下の流れで処理します：
    /// 1. 全物理ファイルを個別にParseToSeries()で解析
    /// 2. Path一致で既存SeriesIdを確定
    /// 3. Path未登録ファイルについてNormalizedTitleInternalの候補をスナップショット取得
    /// 4. 0/1/複数候補に応じた判定＆集約
    /// 5. SeriesId/新規グループ単位でUpdateBindingSeriesAsync/InsertBindingSeriesAsyncを並列実行
    /// </summary>
    /// <param name="rootPaths">スキャン対象のルートフォルダパス一覧。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>保存件数。</returns>
    protected override async ValueTask<int> ScanAndSaveAsync(IEnumerable<string> rootPaths, CancellationToken ct)
    {
        // Step 1: 全物理ファイルを個別に解析
        var allParsedSeries = new List<(MangaSeries series, FileInfo fileInfo)>();

        foreach (var rootPath in rootPaths)
        {
            this.logger.ZLogInformation($"ルートパス走査: {rootPath}");

            foreach (var item in this.GetScanItems(rootPath))
            {
                var parsed = this.ParseToSeries(this.EnsurePhysicalNormalization(item));
                allParsedSeries.Add((parsed, (FileInfo)item));
            }
        }

        if (allParsedSeries.Count == 0)
        {
            this.logger.ZLogInformation($"スキャン対象ファイルが見つかりません");
            return 0;
        }

        this.logger.ZLogInformation($"解析完了: {allParsedSeries.Count}個のBindingアーカイブ");

        // Step 2: Path→SeriesIdマップを構築（既存MangaSourceとの照合）
        var pathToSeriesIdMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in this.targetSourcesByFolder)
        {
            pathToSeriesIdMap[kvp.Value.Path] = kvp.Value.SeriesId;
        }

        this.logger.ZLogInformation($"Path→SeriesIdマップ構築: {pathToSeriesIdMap.Count}件の既存Path");

        // Step 3: 各ファイルのSeriesId/新規グループを判定
        var fileAssignments = new List<(MangaSeries series, FileInfo fileInfo, long? seriesId, string? newGroupKey)>();
        var pathUnregisteredNormalizedTitles = new HashSet<string>(StringComparer.Ordinal);
        var ambiguousFiles = new List<(MangaSeries series, FileInfo fileInfo, string path, string normalizedTitle, string author)>();

        foreach (var (series, fileInfo) in allParsedSeries)
        {
            // Path一致判定（最優先）
            if (pathToSeriesIdMap.TryGetValue(fileInfo.FullName, out var existingSeriesId))
            {
                this.logger.ZLogInformation($"Path一致で既存作品確定: {fileInfo.Name} → SeriesId={existingSeriesId}");
                fileAssignments.Add((series, fileInfo, existingSeriesId, null));
                continue;
            }

            // Path未登録：候補スナップショット取得前に情報収集
            pathUnregisteredNormalizedTitles.Add(series.NormalizedTitleInternal);
            fileAssignments.Add((series, fileInfo, null, null));
        }

        // Step 4: Path未登録ファイルの候補スナップショット取得（Parallel開始前に一度だけ）
        this.logger.ZLogInformation($"Path未登録ファイル用候補スナップショット取得: {pathUnregisteredNormalizedTitles.Count}個のタイトル");
        var candidateSnapshot = await this.GetCandidateSnapshotAsync(
            pathUnregisteredNormalizedTitles.ToList(),
            ct);

        // Step 5: Path未登録ファイルのSeriesId/新規グループを確定
        for (int i = 0; i < fileAssignments.Count; i++)
        {
            var (series, fileInfo, seriesId, newGroupKey) = fileAssignments[i];

            if (seriesId.HasValue)
                continue; // Path一致済み

            try
            {
                var (assignedSeriesId, assignedGroupKey) = this.DetermineSeriesAssignment(
                    series,
                    fileInfo,
                    candidateSnapshot);

                fileAssignments[i] = (series, fileInfo, assignedSeriesId, assignedGroupKey);
            }
            catch (AmbiguousSeriesMatchException ex)
            {
                // 自動判定不能：このファイルのみ除外、他は継続
                this.logger.ZLogError($"自動判定不能な複数候補: Path={ex.Path}, Title={ex.Title}, CandidateCount={ex.CandidateSeriesIds.Count}");
                ambiguousFiles.Add((series, fileInfo, ex.Path, ex.NormalizedTitleInternal, ex.Author));
            }
        }

        // Step 6: SeriesId/新規グループ単位で集約
        var aggregatedBySeriesId = new Dictionary<long, MangaSeries>();
        var aggregatedByNewGroup = new Dictionary<string, MangaSeries>();

        foreach (var (series, fileInfo, seriesId, newGroupKey) in fileAssignments)
        {
            // 曖昧判定で除外されたファイルはスキップ
            if (!seriesId.HasValue && string.IsNullOrEmpty(newGroupKey))
                continue;

            if (seriesId.HasValue)
            {
                // 既存作品
                if (!aggregatedBySeriesId.TryGetValue(seriesId.Value, out var existing))
                {
                    aggregatedBySeriesId[seriesId.Value] = series;
                }
                else
                {
                    // 既存作品へマージ
                    aggregatedBySeriesId[seriesId.Value] = this.MergeSeries(existing, series);
                }
            }
            else
            {
                // 新規グループ
                if (!aggregatedByNewGroup.TryGetValue(newGroupKey!, out var existing))
                {
                    aggregatedByNewGroup[newGroupKey!] = series;
                }
                else
                {
                    // 新規グループへマージ
                    aggregatedByNewGroup[newGroupKey!] = this.MergeSeries(existing, series);
                }
            }
        }

        this.logger.ZLogInformation($"集約完了: 既存作品{aggregatedBySeriesId.Count}件、新規グループ{aggregatedByNewGroup.Count}件、曖昧判定除外{ambiguousFiles.Count}件");

        // Step 7: Parallel.ForEachAsyncで保存
        using var scope = this.scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFolderScannerRepository>();
        var thumbnailCreator = scope.ServiceProvider.GetRequiredService<ThumbnailCreator>();

        var savedCount = 0;
        var sourcesToRemove = new ConcurrentBag<long>();

        // 既存作品用：UpdateBindingSeriesAsync
        await Parallel.ForEachAsync(
            aggregatedBySeriesId,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (kvp, token) =>
            {
                var seriesId = kvp.Key;
                var series = kvp.Value;

                var savedSeries = await repository.UpdateBindingSeriesAsync(
                    seriesId,
                    series,
                    nameof(BindingFolderScanner),
                    token);

                // 実在確認済みSourceIdを収集
                foreach (var source in savedSeries.Sources.Where(s => (int)s.Role == (int)FolderRole.Binding || (int)s.Role == (int)FolderRole.DefaultBinding))
                {
                    var sourceIds = this.targetSourcesByFolder
                        .Where(kvp => kvp.Value.Path.Equals(source.Path, StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => kvp.Key)
                        .ToList();
                    foreach (var sourceId in sourceIds)
                    {
                        sourcesToRemove.Add(sourceId);
                    }
                }

                this.logger.ZLogInformation($"作品情報保存完了（既存更新）: {savedSeries.Title}");

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

        // 新規グループ用：InsertBindingSeriesAsync
        await Parallel.ForEachAsync(
            aggregatedByNewGroup.Values,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (series, token) =>
            {
                var savedSeries = await repository.InsertBindingSeriesAsync(
                    series,
                    nameof(BindingFolderScanner),
                    token);

                // 実在確認済みSourceIdを収集
                foreach (var source in savedSeries.Sources.Where(s => (int)s.Role == (int)FolderRole.Binding || (int)s.Role == (int)FolderRole.DefaultBinding))
                {
                    var sourceIds = this.targetSourcesByFolder
                        .Where(kvp => kvp.Value.Path.Equals(source.Path, StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => kvp.Key)
                        .ToList();
                    foreach (var sourceId in sourceIds)
                    {
                        sourcesToRemove.Add(sourceId);
                    }
                }

                this.logger.ZLogInformation($"作品情報保存完了（新規作成）: {savedSeries.Title}");

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

        // Step 8: targetSourcesByFolderからまとめて除外
        foreach (var sourceId in sourcesToRemove.Distinct())
        {
            this.targetSourcesByFolder.Remove(sourceId);
        }
        this.logger.ZLogInformation($"保存済みMangaSourceを除外: {sourcesToRemove.Distinct().Count()}件");

        return savedCount;
    }

    /// <summary>
    /// Path未登録ファイルの候補スナップショットをRepository経由で取得します。
    /// </summary>
    private async ValueTask<IReadOnlyDictionary<string, IReadOnlyList<MangaSeries>>> GetCandidateSnapshotAsync(
        IList<string> normalizedTitles,
        CancellationToken ct)
    {
        using var scope = this.scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFolderScannerRepository>();

        if (normalizedTitles.Count == 0)
            return new Dictionary<string, IReadOnlyList<MangaSeries>>(StringComparer.Ordinal);

        var snapshot = await repository.GetCandidateSeriesByNormalizedTitlesAsync(normalizedTitles, ct);
        return snapshot;
    }

    /// <summary>
    /// Path未登録ファイルのSeriesId/新規グループを判定します。
    /// 戻り値: (SeriesId?, NewGroupKey?)
    /// - 既存作品確定時: (seriesId, null)
    /// - 新規グループ用: (null, "NormalizedTitle+TrimmedAuthor")
    /// - AmbiguousSeriesMatchExceptionは throw
    /// </summary>
    private (long?, string?) DetermineSeriesAssignment(
        MangaSeries series,
        FileInfo fileInfo,
        IReadOnlyDictionary<string, IReadOnlyList<MangaSeries>> candidateSnapshot)
    {
        var candidates = candidateSnapshot.TryGetValue(series.NormalizedTitleInternal, out var result)
            ? result
            : new List<MangaSeries>();

        if (candidates.Count == 0)
        {
            // 候補0件：新規作品用のグループキー
            var groupKey = $"{series.NormalizedTitleInternal}|{series.Author.Trim()}";
            return (null, groupKey);
        }

        if (candidates.Count == 1)
        {
            // 候補1件：Authorを判定に使わない
            return (candidates[0].SeriesId, null);
        }

        // 候補2件以上：Authorを補助判定に使用
        var trimmedAuthor = series.Author.Trim();
        var authorMatches = candidates
            .Where(c => string.Equals(c.Author.Trim(), trimmedAuthor, StringComparison.Ordinal))
            .ToList();

        if (authorMatches.Count == 1)
        {
            return (authorMatches[0].SeriesId, null);
        }

        if (authorMatches.Count == 0 || authorMatches.Count > 1)
        {
            // 自動判定不能
            throw new AmbiguousSeriesMatchException(
                fileInfo.FullName,
                series.Title,
                series.NormalizedTitleInternal,
                series.Author,
                candidates.Select(c => c.SeriesId).ToList());
        }

        // 到達しないはずだが、念のため
        throw new InvalidOperationException($"予期しない状態");
    }

    /// <summary>
    /// 2つのMangaSeriesをマージします。
    /// - Title/NormalizedTitle/Author: existing優先（既存値を維持）
    /// - StartVolume: MIN
    /// - BoundEndVolume: MAX
    /// - EndVolume: MAX
    /// - SeriesCompleted: OR
    /// - IsOwnedCompleted: OR
    /// - Sources: 全追加
    /// </summary>
    private MangaSeries MergeSeries(MangaSeries existing, MangaSeries parsed)
    {
        var result = new MangaSeries
        {
            SeriesId                = existing.SeriesId,
            Title                   = existing.Title,
            NormalizedTitleInternal = existing.NormalizedTitleInternal,
            NormalizedTitleExternal = existing.NormalizedTitleExternal,
            Author                  = existing.Author.Length > 0 ? existing.Author : parsed.Author,
            StartVolume             = Math.Min(existing.StartVolume, parsed.StartVolume),
            BoundEndVolume          = Math.Max(existing.BoundEndVolume, parsed.BoundEndVolume),
            EndVolume               = Math.Max(existing.EndVolume, parsed.EndVolume),
            SeriesCompleted         = existing.SeriesCompleted || parsed.SeriesCompleted,
            IsOwnedCompleted        = existing.IsOwnedCompleted || parsed.IsOwnedCompleted,
            ShortTitle              = existing.ShortTitle,
            ThumbnailFileName       = existing.ThumbnailFileName,
        };
        result.Sources.AddRange(existing.Sources);
        result.Sources.AddRange(parsed.Sources);
        return result;
    }

    /// <summary>
    /// 抽象メソッド SaveResultsAsync の実装（実際には使用されない）。
    /// ScanAndSaveAsync で新しい InsertBindingSeriesAsync/UpdateBindingSeriesAsync API を直接使用するため。
    /// </summary>
    protected override ValueTask<MangaSeries> SaveResultsAsync(MangaSeries series, IFolderScannerRepository repository, CancellationToken ct)
        => throw new NotImplementedException($"BindingFolderScanner は SaveResultsAsync を使用しません。直接 InsertBindingSeriesAsync/UpdateBindingSeriesAsync を呼び出します。");
}
