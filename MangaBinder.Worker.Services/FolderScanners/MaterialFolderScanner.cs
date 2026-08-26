using System.IO;
using MangaBinder.Helpers;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 素材フォルダをスキャンするジョブです。
/// </summary>
public class MaterialFolderScanner : FolderScannerBase
{
    /// <summary>タイトル区切り文字群。</summary>
    private readonly string titleSeparatorChars;

    /// <summary>手持ち最大巻数の推定処理。</summary>
    private readonly OwnedVolumeEstimator ownedVolumeEstimator;

    /// <summary>
    /// <see cref="MaterialFolderScanner"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="scopeFactory">スコープファクトリ。</param>
    /// <param name="workerContext">Worker 実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    public MaterialFolderScanner(
        IServiceScopeFactory scopeFactory,
        WorkerContext workerContext,
        ILogger<MaterialFolderScanner> logger)
        : base(scopeFactory, logger, FolderRole.Material, workerContext)
    {
        this.titleSeparatorChars = workerContext.TitleSeparatorChars;
        this.ownedVolumeEstimator = new OwnedVolumeEstimator();
    }

    /// <summary>
    /// 指定されたルートパス直下のサブフォルダ一覧を返します。
    /// </summary>
    /// <param name="rootPath">スキャン対象のルートフォルダパス。</param>
    /// <returns>サブフォルダの <see cref="DirectoryInfo"/> 一覧。</returns>
    protected override IEnumerable<FileSystemInfo> GetScanItems(string rootPath)
        => new DirectoryInfo(rootPath).GetDirectories();

    /// <summary>
    /// フォルダ名を解析して <see cref="MangaSeries"/> を生成します。
    /// <see cref="MangaSeries.Sources"/> に素材フォルダの所在情報を追加します。
    /// </summary>
    /// <param name="info">解析対象のフォルダ情報。</param>
    /// <returns>解析結果の <see cref="MangaSeries"/>。</returns>
    protected override MangaSeries ParseToSeries(FileSystemInfo info)
    {
        var series = FileSystemNameHelper.ParseAsMaterial(info.Name, this.titleSeparatorChars);
        var estimate = this.ownedVolumeEstimator.Estimate(info.FullName);
        series.OwnedMaxVolume = estimate.OwnedMaxVolume;
        series.MaterialFolderCreatedAt = info.CreationTime;
        series.Sources.Add(new MangaSource
        {
            Role = FolderRole.Material,
            Path = info.FullName,
        });
        return series;
    }

    /// <summary>
    /// ルートパスを走査し、各項目をパースして 2 フェーズで保存します。
    /// DriveInfo.IsReady == false の場合はスキップし、Warning ログを出力します。
    /// 
    /// 処理フロー（2フェーズ処理）：
    /// Phase 1: DB既存 Material Path に一致した素材を処理
    ///   - UpdateMaterialSeriesByPathAsync で既存 SeriesId に直接更新
    ///   - targetSourcesByFolder から除外
    /// Phase 2: 新規あるいは Path 不一致の素材を処理
    ///   - SaveMaterialSeriesAsync で従来の NormalizedTitleInternal ベース判定
    ///   - targetSourcesByFolder から除外
    /// 
    /// 注：Path一致群と不一致群は順序立てて処理され、同時にDB更新しません。
    /// </summary>
    /// <param name="rootPaths">スキャン対象のルートフォルダパス一覧。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>保存件数。</returns>
    protected override async ValueTask<int> ScanAndSaveAsync(IEnumerable<string> rootPaths, CancellationToken ct)
    {
        // スキャン・名寄せフェーズ（スコープ不要）
        var seriesList = new List<MangaSeries>();

        foreach (var rootPath in rootPaths)
        {
            // ドライブの準備状態を確認
            var drive = new DriveInfo(rootPath);
            if (!drive.IsReady)
            {
                this.logger.ZLogWarning($"素材ルートを利用できないためスキップ: {rootPath}");
                continue;
            }

            this.logger.ZLogInformation($"ルートパス走査: {rootPath}");

            foreach (var item in this.GetScanItems(rootPath))
                seriesList.Add(this.ParseToSeries(this.EnsurePhysicalNormalization(item)));
        }

        // 保存フェーズ：スコープを明示的に生成してサービスを解決
        using var scope = this.scopeFactory.CreateScope();
        var repository       = scope.ServiceProvider.GetRequiredService<IFolderScannerRepository>();
        var thumbnailCreator = scope.ServiceProvider.GetRequiredService<ThumbnailCreator>();

        // DB登録済み Material MangaSource を Path（物理フルパス）をキーに、(SeriesId, MangaSource) をキャッシュ
        var pathToExistingSourceMap = new Dictionary<string, (long SeriesId, MangaSource Source)>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceId in this.targetSourcesByFolder.Keys.ToList())
        {
            var source = this.targetSourcesByFolder[sourceId];
            if ((int)source.Role == (int)FolderRole.Material)
            {
                pathToExistingSourceMap[source.Path] = (source.SeriesId, source);
            }
        }

        // 素材フォルダを処理経路別に分類
        var pathMatchedSeries = new List<(MangaSeries series, long existingSeriesId)>();
        var pathUnmatchedSeries = new List<MangaSeries>();

        foreach (var series in seriesList)
        {
            // 素材フォルダのパスは Sources[0].Path に格納されている
            var folderPath = series.Sources.FirstOrDefault()?.Path;

            if (!string.IsNullOrEmpty(folderPath) && pathToExistingSourceMap.TryGetValue(folderPath, out var existingEntry))
            {
                // Path一致：既存SeriesIdで更新する群
                pathMatchedSeries.Add((series, existingEntry.SeriesId));
                this.logger.ZLogInformation($"Path一致により既存作品で処理: {folderPath}");
            }
            else
            {
                // Path不一致：従来のタイトル判定で処理する群
                pathUnmatchedSeries.Add(series);
            }
        }

        var savedCount = 0;

        // Phase 1: Path一致群を処理
        this.logger.ZLogInformation($"Phase 1: Path一致群の処理を開始（{pathMatchedSeries.Count}件）");

        var pathMatchedSourceIds = new HashSet<long>();

        await Parallel.ForEachAsync(pathMatchedSeries, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (item, token) =>
        {
            var (series, existingSeriesId) = item;

            // Path一致時：既存SeriesIdで直接更新
            var savedSeries = await repository.UpdateMaterialSeriesByPathAsync(existingSeriesId, series, nameof(MaterialFolderScanner), token);
            this.logger.ZLogInformation($"既存作品を直接更新（Path一致）: {series.Sources.FirstOrDefault()?.Path}");

            // 実在確認済み MangaSource の SourceId を収集
            foreach (var source in savedSeries.Sources.Where(s => (int)s.Role == (int)FolderRole.Material))
            {
                var sourceIdsToAdd = this.targetSourcesByFolder
                    .Where(kvp => kvp.Value.Path.Equals(source.Path, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var sourceId in sourceIdsToAdd)
                {
                    lock (pathMatchedSourceIds)
                    {
                        pathMatchedSourceIds.Add(sourceId);
                    }
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

        // Phase 1 完了後、Path一致群の SourceId を targetSourcesByFolder から除外
        this.logger.ZLogInformation($"Phase 1 完了: Path一致群の SourceId を除外（{pathMatchedSourceIds.Count}件）");
        foreach (var sourceId in pathMatchedSourceIds)
        {
            this.targetSourcesByFolder.Remove(sourceId);
        }

        // Phase 2: Path不一致群を処理
        this.logger.ZLogInformation($"Phase 2: Path不一致群の処理を開始（{pathUnmatchedSeries.Count}件）");

        var pathUnmatchedSourceIds = new HashSet<long>();

        await Parallel.ForEachAsync(pathUnmatchedSeries, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (series, token) =>
        {
            try
            {
                // Path不一致時：新しい Phase 2 ロジック（候補判定 + Author 補助判定）
                var savedSeries = await this.SaveResultsAsync(series, repository, token);
                this.logger.ZLogInformation($"タイトル判定で処理（Path不一致）: {series.Title}");

                // 実在確認済み MangaSource の SourceId を収集
                foreach (var source in savedSeries.Sources.Where(s => (int)s.Role == (int)FolderRole.Material))
                {
                    var sourceIdsToAdd = this.targetSourcesByFolder
                        .Where(kvp => kvp.Value.Path.Equals(source.Path, StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => kvp.Key)
                        .ToList();
                    foreach (var sourceId in sourceIdsToAdd)
                    {
                        lock (pathUnmatchedSourceIds)
                        {
                            pathUnmatchedSourceIds.Add(sourceId);
                        }
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
            }
            catch (AmbiguousSeriesMatchException ex)
            {
                // 自動判定不能な複数候補が存在する場合、この素材フォルダの処理をスキップ
                this.logger.ZLogError(
                    $"自動判定不能なため処理スキップ。Path={ex.Path}, Title={ex.Title}, Author={ex.Author}, CandidateCount={ex.CandidateSeriesIds.Count}");
                // 保存成功扱いとしない（SourceId を除外しない）
                return;
            }
        });

        // Phase 2 完了後、処理成功群の SourceId を targetSourcesByFolder から除外
        this.logger.ZLogInformation($"Phase 2 完了: Path不一致群の SourceId を除外（{pathUnmatchedSourceIds.Count}件）");
        foreach (var sourceId in pathUnmatchedSourceIds)
        {
            this.targetSourcesByFolder.Remove(sourceId);
        }

        // Phase 2 完了
        this.logger.ZLogInformation($"Phase 2 完了: Path不一致群の処理が完了");


        return savedCount;
    }

    /// <summary>
    /// 素材スキャン結果をリポジトリ経由で保存し、DB最新状態の <see cref="MangaSeries"/> を返します。
    /// Phase 2 用の新しい実装。NormalizedTitleInternal の一意性を前提とせず、
    /// 候補数に応じた判定（0件:新規/1件:既存/2件以上:Author補助判定）を行います。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="repository">保存先リポジトリ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaSeries"/>。</returns>
    /// <exception cref="AmbiguousSeriesMatchException">自動判定不能な複数候補が存在する場合。</exception>
    protected override async ValueTask<MangaSeries> SaveResultsAsync(MangaSeries series, IFolderScannerRepository repository, CancellationToken ct)
    {
        // NormalizedTitleInternal で候補検索
        var candidates = await repository.GetCandidateSeriesByNormalizedTitleAsync(series.NormalizedTitleInternal, ct);

        long seriesId;

        if (candidates.Count == 0)
        {
            // === 候補 0 件 === 新しい MangaSeries として INSERT
            var savedSeries = await repository.InsertMaterialSeriesAsync(series, nameof(MaterialFolderScanner), ct);
            seriesId = savedSeries.SeriesId;
            this.logger.ZLogInformation($"新規作品として INSERT: SeriesId={seriesId}, Title={series.Title}");
        }
        else if (candidates.Count == 1)
        {
            // === 候補 1 件 === Author は判定に使用しない、既存作品として UPDATE
            seriesId = candidates[0].SeriesId;
            await repository.UpdateMaterialSeriesAsync(seriesId, series, nameof(MaterialFolderScanner), ct);
            this.logger.ZLogInformation($"既存作品として UPDATE: SeriesId={seriesId}, Title={series.Title}");
        }
        else
        {
            // === 候補 2 件以上 === Author を補助情報として使用
            var matchedByAuthor = candidates
                .Where(c => c.Author?.Trim() == series.Author?.Trim())
                .ToList();

            if (matchedByAuthor.Count == 1)
            {
                // Author 完全一致 1 件
                seriesId = matchedByAuthor[0].SeriesId;
                await repository.UpdateMaterialSeriesAsync(seriesId, series, nameof(MaterialFolderScanner), ct);
                this.logger.ZLogInformation($"Author 一致で既存作品として UPDATE: SeriesId={seriesId}, Title={series.Title}");
            }
            else
            {
                // 自動判定不能
                var candidateIds = candidates.Select(c => c.SeriesId).ToList().AsReadOnly();
                throw new AmbiguousSeriesMatchException(
                    series.Sources.FirstOrDefault()?.Path ?? string.Empty,
                    series.Title,
                    series.NormalizedTitleInternal,
                    series.Author ?? string.Empty,
                    candidateIds);
            }
        }

        // 最新の作品情報を Repository から取得して返却
        var savedResult = await repository.GetCandidateSeriesByNormalizedTitleAsync(series.NormalizedTitleInternal, ct);
        return savedResult.FirstOrDefault(s => s.SeriesId == seriesId) ?? series;
    }

    /// <summary>
    /// 素材フォルダスキャンを実行し、完了後に後続ジョブを自動投入します。
    /// 基本的なスキャン処理を実行した後、以下の後続ジョブを順序投入します：
    /// 1. <see cref="JobType.LargeThumbnailCreate"/> - 巨大サムネイル作成（サイズ超過判定時）
    /// 2. <see cref="JobType.MaterialArchiveScan"/> - アーカイブキャッシュ作成
    /// 後続ジョブの投入時には既存の重複チェック処理を利用し、Pending/Running 状態のジョブが存在する場合はスキップします。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public override async ValueTask ExecuteAsync(CancellationToken ct)
    {
        // 基本的なスキャン処理を実行
        await base.ExecuteAsync(ct);

        // 素材スキャン完了後、後続ジョブを自動投入
        this.logger.ZLogInformation($"素材フォルダスキャンが完了しました。後続ジョブの投入を準備します。");

        using var scope = this.scopeFactory.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IFolderScannerRepository>();
        var jobRepository = scope.ServiceProvider.GetRequiredService<JobRepository>();

        // 巨大サムネイル作成ジョブを投入（条件付き）
        if (await repository.HasLimitExceededAsync(ct))
        {
            this.logger.ZLogInformation($"サイズ超過によりスキップした作品が存在するため、巨大サムネイル作成ジョブをキューに登録します。");
            await jobRepository.EnqueueAsync(JobType.LargeThumbnailCreate, skipThumbnailSizeLimit: true);
        }

        // アーカイブキャッシュ作成ジョブを投入
        this.logger.ZLogInformation($"アーカイブキャッシュ作成ジョブをキューに登録します。");
        await jobRepository.EnqueueAsync(JobType.MaterialArchiveScan, skipThumbnailSizeLimit: false);

        this.logger.ZLogInformation($"素材フォルダスキャン完了処理が終了しました。");
    }
}

