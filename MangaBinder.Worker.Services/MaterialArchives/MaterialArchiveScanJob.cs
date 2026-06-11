using MangaBinder.Bindings;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MangaBinder.Jobs.MaterialArchives;

/// <summary>
/// アーカイブ内部構造のスキャンジョブです。
/// 素材フォルダ内のアーカイブをスキャンして、キャッシュ用のデータを生成します。
/// </summary>
public class MaterialArchiveScanJob : IJob
{
	/// <summary>リポジトリ。</summary>
	private readonly MaterialArchiveScanRepository repository;

	/// <summary>素材フォルダローダー。</summary>
	private readonly SeriesMaterialFolderLoader materialFolderLoader;

	/// <summary>ロガー。</summary>
	private readonly ILogger<MaterialArchiveScanJob> logger;

	/// <inheritdoc />
	public bool SkipThumbnailSizeLimit { get; set; }

	/// <summary>
	/// <see cref="MaterialArchiveScanJob"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="repository">リポジトリ。</param>
	/// <param name="materialFolderLoader">素材フォルダローダー。</param>
	/// <param name="logger">ロガー。</param>
	public MaterialArchiveScanJob(
		MaterialArchiveScanRepository repository,
		SeriesMaterialFolderLoader materialFolderLoader,
		ILogger<MaterialArchiveScanJob> logger)
	{
		this.repository = repository;
		this.materialFolderLoader = materialFolderLoader;
		this.logger = logger;
	}

	/// <inheritdoc />
	public async ValueTask ExecuteAsync(CancellationToken ct)
	{
		this.logger.ZLogInformation($"アーカイブ内部構造スキャンジョブを開始します。");
		var allSeries = await this.repository.GetAllSeriesAsync(ct);
		this.logger.ZLogInformation($"対象作品数: {allSeries.Count}");

		if (allSeries.Count == 0)
		{
			this.logger.ZLogInformation($"対象作品が0件のため終了します。");
			return;
		}

		foreach (var series in allSeries)
		{
			ct.ThrowIfCancellationRequested();

			this.logger.ZLogInformation($"アーカイブ構造スキャン開始: {series.Title}");
			try
			{
				// 素材フォルダを読み込む
				var result = await this.materialFolderLoader.GetMaterialsAsync(series, ct);

				// 読み込み結果を確認
				switch (result.Status)
				{
					case MaterialFolderStatus.Success:
						this.logger.ZLogInformation($"アーカイブ構造スキャン完了: {series.Title}");
						break;

					case MaterialFolderStatus.NoMaterialSource:
						this.logger.ZLogDebug($"素材ソースなし: {series.Title}");
						break;

					case MaterialFolderStatus.MaterialSourceNotFound:
						this.logger.ZLogWarning($"素材フォルダが見つかりません: {series.Title} Path={result.TargetPath}");
						break;

					case MaterialFolderStatus.DriveNotReady:
						this.logger.ZLogWarning($"ドライブが接続されていません: {series.Title} Path={result.TargetPath}");
						break;

					default:
						this.logger.ZLogWarning($"不明なステータス: {series.Title} Status={result.Status}");
						break;
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				this.logger.ZLogError(ex, $"アーカイブ構造スキャンに失敗しました。スキップして次へ進みます: {series.Title}");
			}
		}

		this.logger.ZLogInformation($"アーカイブ内部構造スキャンジョブが完了しました。");
	}
}
