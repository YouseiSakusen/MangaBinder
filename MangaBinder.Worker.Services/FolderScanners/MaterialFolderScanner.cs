using System.IO;
using MangaBinder;
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
        var series = MangaTitleHelper.ParseAsMaterial(info.Name, this.titleSeparatorChars);
        var estimate = this.ownedVolumeEstimator.Estimate(info.FullName);
        series.OwnedMaxVolume = estimate.OwnedMaxVolume;
        series.Sources.Add(new MangaSource
        {
            Role = FolderRole.Material,
            Path = info.FullName,
        });
        return series;
    }

    /// <summary>
    /// ルートパスを走査し、各項目をパースして即座に保存します。
    /// DriveInfo.IsReady == false の場合はスキップし、Warning ログを出力します。
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

        var savedCount = 0;

        await Parallel.ForEachAsync(seriesList, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (series, token) =>
        {
            var savedSeries = await this.SaveResultsAsync(series, repository, token);

            // スキャン中に見つかった MangaSource をメモリから除外
            foreach (var source in savedSeries.Sources.Where(s => (int)s.Role == (int)FolderRole.Material))
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
    /// 素材スキャン結果をリポジトリ経由で保存し、DB最新状態の <see cref="MangaSeries"/> を返します。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="repository">保存先リポジトリ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaSeries"/>。</returns>
    protected override ValueTask<MangaSeries> SaveResultsAsync(MangaSeries series, IFolderScannerRepository repository, CancellationToken ct)
        => repository.SaveMaterialSeriesAsync(series, ct);

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

