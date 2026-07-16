using MangaBinder.Bindings;
using MangaBinder.Core.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Series;

/// <summary>
/// 一時保存（作業作品）の保存処理を実行するマネージャーです。
/// </summary>
public sealed class WorkSeriesSaveManager : ISeriesSaveManager
{
	/// <summary>登録待ち作品の取得・保存を担う Repository。</summary>
	private readonly WorkMangaSeriesRepository workMangaSeriesRepository;

	/// <summary>サムネイル操作を管理する Manager。</summary>
	private readonly ThumbnailManager thumbnailManager;

	/// <summary>MangaSeries の正本リストを管理するストア。</summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>ログ出力。</summary>
	private readonly ILogger<WorkSeriesSaveManager> logger;

	/// <summary>
	/// <see cref="WorkSeriesSaveManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="workMangaSeriesRepository">登録待ち作品の取得・保存を担う Repository。</param>
	/// <param name="thumbnailManager">サムネイル操作を管理する Manager。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="logger">ログ出力。</param>
	public WorkSeriesSaveManager(
		WorkMangaSeriesRepository workMangaSeriesRepository,
		ThumbnailManager thumbnailManager,
		MangaSeriesStore mangaSeriesStore,
		ILogger<WorkSeriesSaveManager> logger)
	{
		this.workMangaSeriesRepository = workMangaSeriesRepository;
		this.thumbnailManager = thumbnailManager;
		this.mangaSeriesStore = mangaSeriesStore;
		this.logger = logger;
	}

	/// <summary>
	/// 一時保存処理を実行します。
	/// </summary>
	/// <param name="editingSeries">編集中の作品。</param>
	/// <param name="originalSeries">編集開始時の DeepCopy。</param>
	/// <param name="materialFiles">追加された素材ファイル。</param>
	/// <param name="selectedMaterialSourceFolder">素材の移動先フォルダ。</param>
	/// <param name="thumbnailBytes">新しいサムネイルのバイト列。</param>
	/// <returns>保存後の作品インスタンス。</returns>
	public async ValueTask<MangaSeries> SaveAsync(
		MangaSeries editingSeries,
		MangaSeries? originalSeries,
		IReadOnlyList<MaterialFile> materialFiles,
		SourceFolder? selectedMaterialSourceFolder,
		byte[]? thumbnailBytes)
	{
		ArgumentNullException.ThrowIfNull(editingSeries);

		if (editingSeries.WorkId == 0)
		{
			// 新規 INSERT：採番された WorkId を返す
			var workId = await this.workMangaSeriesRepository.InsertAsync(editingSeries);
			// series.WorkId は InsertAsync 内で既に設定されているが、念のため保証
			if (editingSeries.WorkId == 0)
				editingSeries.WorkId = workId;

			// タグを保存
			await this.workMangaSeriesRepository.SaveWorkTagsAsync(new[] { editingSeries });

			// サムネイル JPEG を保存（存在する場合のみ）
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				// ファイル名を決定（WorkThumbnailFileNameBase を使用）
				var fileName = $"{editingSeries.WorkThumbnailFileNameBase}.jpg";

				// ThumbnailManager で保存
				await this.thumbnailManager.SaveWorkThumbnailAsync(fileName, thumbnailBytes);

				// series の ThumbnailFileName と ThumbnailStatus を更新
				editingSeries.ThumbnailFileName = fileName;
				editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;

				// ファイル保存後、DB に反映
				await this.workMangaSeriesRepository.UpdateAsync(editingSeries);
			}

			// Store へ即座に反映
			this.mangaSeriesStore.UpdateWorkSeries(editingSeries);

			return editingSeries;
		}
		else
		{
			// UPDATE（既存の登録待ち作品の更新）
			// タグを保存
			await this.workMangaSeriesRepository.SaveWorkTagsAsync(new[] { editingSeries });

			// サムネイル JPEG を保存（存在する場合のみ）
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				// ファイル名を決定（WorkThumbnailFileNameBase を使用）
				var fileName = $"{editingSeries.WorkThumbnailFileNameBase}.jpg";

				// ThumbnailManager で保存
				await this.thumbnailManager.SaveWorkThumbnailAsync(fileName, thumbnailBytes);

				// series の ThumbnailFileName と ThumbnailStatus を更新
				editingSeries.ThumbnailFileName = fileName;
				editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;
			}

			// DB へ反映
			await this.workMangaSeriesRepository.UpdateAsync(editingSeries);

			// Store へ即座に反映
			this.mangaSeriesStore.UpdateWorkSeries(editingSeries);

			return editingSeries;
		}
	}
}
