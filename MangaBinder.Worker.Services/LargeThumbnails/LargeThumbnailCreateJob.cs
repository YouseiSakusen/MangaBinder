using MangaBinder.Jobs.FolderScanners;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MangaBinder.Jobs.LargeThumbnails;

/// <summary>
/// 大容量ファイルからのサムネイル作成ジョブです。
/// </summary>
public class LargeThumbnailCreateJob : IJob
{
    /// <summary>リポジトリ。</summary>
    private readonly LargeThumbnailRepository repository;

    /// <summary>サムネイル作成サービス。</summary>
    private readonly ThumbnailCreator thumbnailCreator;

    /// <summary>ロガー。</summary>
    private readonly ILogger<LargeThumbnailCreateJob> logger;

    /// <inheritdoc />
    public bool SkipThumbnailSizeLimit { get; set; }

    public LargeThumbnailCreateJob(
        LargeThumbnailRepository repository,
        ThumbnailCreator thumbnailCreator,
        ILogger<LargeThumbnailCreateJob> logger)
    {
        this.repository = repository;
        this.thumbnailCreator = thumbnailCreator;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(CancellationToken ct)
    {
        this.logger.ZLogInformation($"大容量サムネイル作成ジョブを開始します。");
        var targets = await this.repository.GetTargetSeriesAsync(ct);
        this.logger.ZLogInformation($"対象作品数: {targets.Count}");
        if (targets.Count == 0)
        {
            this.logger.ZLogInformation($"対象作品が0件のため終了します。");
            return;
        }
        foreach (var series in targets)
        {
            ct.ThrowIfCancellationRequested();
            this.logger.ZLogInformation($"サムネイル作成開始: {series.Title}");
            try
            {
                var result = await this.thumbnailCreator.CreateAsync(series, skipThumbnailSizeLimit: true, ct);
                await this.repository.UpdateThumbnailAsync(series.SeriesId, result.ThumbnailFileName, result.Status, ct);
                this.logger.ZLogInformation($"サムネイル作成完了: {series.Title} Status={result.Status}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                this.logger.ZLogError(ex, $"サムネイル作成に失敗しました。スキップして次へ進みます: {series.Title}");
            }
        }
        this.logger.ZLogInformation($"大容量サムネイル作成ジョブが完了しました。");
    }
}