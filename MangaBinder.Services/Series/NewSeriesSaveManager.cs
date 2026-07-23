using System.Data.SQLite;
using HalationGhost.Utilities;
using Microsoft.Extensions.Logging;
using MangaBinder.Bindings;
using MangaBinder.Core.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;
using MangaBinder.Helpers;

namespace MangaBinder.Series;

/// <summary>
/// 新規作品の保存処理を実行するマネージャーです。
/// 新規作品と登録待ち作品の正式登録処理を担当します。
/// </summary>
public class NewSeriesSaveManager : ISeriesSaveManager
{
	/// <summary>ログを出力するロガー。</summary>
	private readonly ILogger<NewSeriesSaveManager> logger;

	/// <summary>MangaSeries の取得を担う Repository。</summary>
	private readonly MangaRepository mangaRepository;

	/// <summary>WorkMangaSeries の操作を担う Repository。</summary>
	private readonly WorkMangaSeriesRepository workMangaSeriesRepository;

	/// <summary>MangaSeries の正本リストを管理するストア。</summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>サムネイル操作を管理する Manager。</summary>
	private readonly ThumbnailManager thumbnailManager;

	/// <summary>素材操作を管理する Manager。</summary>
	private readonly MaterialManager materialManager;

	/// <summary>
	/// <see cref="NewSeriesSaveManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="logger">ログを出力するロガー。</param>
	/// <param name="mangaRepository">MangaSeries の取得を担う Repository。</param>
	/// <param name="workMangaSeriesRepository">WorkMangaSeries の操作を担う Repository。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="thumbnailManager">サムネイル操作を管理する Manager。</param>
	/// <param name="materialManager">素材操作を管理する Manager。</param>
	public NewSeriesSaveManager(
		ILogger<NewSeriesSaveManager> logger,
		MangaRepository mangaRepository,
		WorkMangaSeriesRepository workMangaSeriesRepository,
		MangaSeriesStore mangaSeriesStore,
		AppSettings appSettings,
		ThumbnailManager thumbnailManager,
		MaterialManager materialManager)
	{
		this.logger = logger;
		this.mangaRepository = mangaRepository;
		this.workMangaSeriesRepository = workMangaSeriesRepository;
		this.mangaSeriesStore = mangaSeriesStore;
		this.appSettings = appSettings;
		this.thumbnailManager = thumbnailManager;
		this.materialManager = materialManager;
	}

	/// <summary>
	/// 新規作品の保存処理を実行します。
	/// SeriesId == 0 のみ受け付け、それ以外は InvalidOperationException をスローします。
	/// </summary>
	/// <param name="editingSeries">編集中の作品。SeriesId == 0 である必要があります。</param>
	/// <param name="originalSeries">使用しません。</param>
	/// <param name="materialFiles">追加された素材ファイル。</param>
	/// <param name="selectedMaterialSourceFolder">素材の移動先フォルダ。</param>
	/// <param name="thumbnailBytes">新しいサムネイルのバイト列。</param>
	/// <returns>正式登録後の作品インスタンス。</returns>
	/// <exception cref="InvalidOperationException">SeriesId != 0 の場合。</exception>
	public async ValueTask<MangaSeries> SaveAsync(
		MangaSeries editingSeries,
		MangaSeries? originalSeries,
		IReadOnlyList<MaterialFile> materialFiles,
		SourceFolder? selectedMaterialSourceFolder,
		byte[]? thumbnailBytes)
	{
		// 入力値検証
		ArgumentNullException.ThrowIfNull(editingSeries);
		ArgumentNullException.ThrowIfNull(materialFiles);
		ArgumentNullException.ThrowIfNull(selectedMaterialSourceFolder);

		// SeriesId == 0 のみ受け付け
		if (editingSeries.SeriesId != 0)
		{
			throw new InvalidOperationException("NewSeriesSaveManager は SeriesId == 0 の新規作品のみ受け付けます。");
		}

		var isWorkSeries = editingSeries.IsWork;
		var workId = editingSeries.WorkId;

		// サニタイズ済みフォルダ名を取得（素材移動時に使用）
		var materialFolderName = MaterialFolderNameHelper.Create(editingSeries);

		// DB 接続
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();
		using var tx = connection.BeginTransaction();

		try
		{
			// MangaSeries INSERT
			var seriesId = await this.mangaRepository.InsertSeriesInTransactionAsync(
				connection,
				tx,
				editingSeries);

			// SeriesId を editingSeries に反映
			editingSeries.SeriesId = seriesId;

			// タグを MangaSeriesTags へ保存
			await this.SaveSeriesTagsInTransactionAsync(connection, tx, seriesId, editingSeries.Tags);

			// サムネイル保存
			await this.SaveSeriesThumbnailAsync(connection, tx, editingSeries, thumbnailBytes, isWorkSeries, workId);

			// 素材移動
			var moveResult = await this.materialManager.MoveMaterialsAsync(
				selectedMaterialSourceFolder,
				materialFolderName,
				materialFiles);

			// MangaSources へ作品フォルダ情報を登録
			await this.mangaRepository.InsertMangaSourceAsync(
				connection,
				tx,
				seriesId,
				moveResult.SeriesFolderPath,
				FolderRole.Material);

			// 登録待ち作品の場合は WorkMangaSeriesTags と WorkMangaSeries を削除
			if (isWorkSeries)
			{
				// WorkMangaSeriesTags を削除
				await this.workMangaSeriesRepository.DeleteWorkSeriesTagsByIdInTransactionAsync(
					connection,
					tx,
					workId);

				// WorkMangaSeries を削除
				await this.workMangaSeriesRepository.DeleteWorkSeriesByIdInTransactionAsync(
					connection,
					tx,
					workId);
			}

			// Commit
			tx.Commit();

			// Commit 成功後の処理
			// 1. 登録待ち作品の場合、WorkSeriesから削除
			if (isWorkSeries)
			{
				this.mangaSeriesStore.RemoveWorkSeries(workId);
							}

							// 2. DB から採番済み SeriesId の正式作品を再取得
							var registeredSeries = await this.mangaRepository.GetSeriesAsync(seriesId);
							if (registeredSeries is null)
							{
								throw new InvalidOperationException($"正式登録後の作品再取得に失敗しました。SeriesId: {seriesId}");
							}

							// 追跡開始
							NewSeriesHomeSyncTrace.Begin(registeredSeries.SeriesId);

							try
							{
								// [NewSeriesHomeSync] 正式作品再取得完了ログ
								if (NewSeriesHomeSyncTrace.IsTracking(registeredSeries.SeriesId))
								{
									this.logger.LogInformation(
										"[NewSeriesHomeSync] 正式作品再取得完了 SeriesId={SeriesId} Title={Title} NormalizedTitleInternal={NormalizedTitleInternal} Store追加前件数={Count}",
										registeredSeries.SeriesId, registeredSeries.Title, registeredSeries.NormalizedTitleInternal, this.mangaSeriesStore.All.Count);
								}

								// 3. 再取得した正式作品を Store へ追加
								this.mangaSeriesStore.Add(registeredSeries);

								// [NewSeriesHomeSync] Store.Add呼び出し完了ログ
								if (NewSeriesHomeSyncTrace.IsTracking(registeredSeries.SeriesId))
								{
									var storeContainsResult = this.mangaSeriesStore.FindById(registeredSeries.SeriesId) is not null;
									this.logger.LogInformation(
										"[NewSeriesHomeSync] Store.Add呼び出し完了 SeriesId={SeriesId} Title={Title} NormalizedTitleInternal={NormalizedTitleInternal} Store追加後件数={Count} Store内存在確認結果={Result}",
										registeredSeries.SeriesId, registeredSeries.Title, registeredSeries.NormalizedTitleInternal, this.mangaSeriesStore.All.Count, storeContainsResult);
								}

								// 4. 再取得した正式作品を返す
								return registeredSeries;
							}
							finally
							{
								NewSeriesHomeSyncTrace.End(registeredSeries.SeriesId);
							}
						}
				catch
						{
							tx.Rollback();
							throw;
						}
	}

	/// <summary>
	/// 指定した SeriesId のタグを MangaSeriesTags テーブルへ保存します。
	/// 既存の接続およびトランザクション内での実行を想定しています。
	/// </summary>
	private async ValueTask SaveSeriesTagsInTransactionAsync(
		SQLiteConnection connection,
		SQLiteTransaction transaction,
		long seriesId,
		IEnumerable<MangaTag> tags)
	{
		await this.mangaRepository.InsertSeriesTagsInTransactionAsync(
			connection,
			transaction,
			seriesId,
			tags);
	}

	/// <summary>
	/// 正式登録時のサムネイル保存を実施します。
	/// 優先順位：thumbnailBytes → WorkThumbnail → なし
	/// </summary>
	private async ValueTask SaveSeriesThumbnailAsync(
		SQLiteConnection connection,
		SQLiteTransaction tx,
		MangaSeries editingSeries,
		byte[]? thumbnailBytes,
		bool isWorkSeries,
		int workId)
	{
		if (thumbnailBytes != null && thumbnailBytes.Length > 0)
		{
			// 1. thumbnailBytes を正式 Thumbnail へ保存
			var fileName = $"{FileSystemCharSanitizer.Sanitize(editingSeries.ThumbnailFileNameBase)}.jpg";
			await this.thumbnailManager.SaveThumbnailAsync(fileName, thumbnailBytes);

			editingSeries.ThumbnailFileName = fileName;
			editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;

			// DB に反映
			await this.mangaRepository.UpdateSeriesThumbnailAsync(
				connection,
				tx,
				editingSeries.SeriesId,
				fileName,
				ThumbnailStatus.Completed);
		}
		else if (isWorkSeries)
		{
			// 2. WorkThumbnail が存在する場合、正式 Thumbnail へコピー
			var workThumbnailFileName = $"{FileSystemCharSanitizer.Sanitize(editingSeries.WorkThumbnailFileNameBase)}.jpg";
			var copied = await this.thumbnailManager.CopyWorkThumbnailToThumbnailAsync(
				workThumbnailFileName,
				$"{FileSystemCharSanitizer.Sanitize(editingSeries.ThumbnailFileNameBase)}.jpg");

			if (copied)
			{
				editingSeries.ThumbnailFileName = $"{FileSystemCharSanitizer.Sanitize(editingSeries.ThumbnailFileNameBase)}.jpg";
				editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;

				// DB に反映
				await this.mangaRepository.UpdateSeriesThumbnailAsync(
					connection,
					tx,
					editingSeries.SeriesId,
					editingSeries.ThumbnailFileName,
					ThumbnailStatus.Completed);
			}

			// WorkThumbnail を削除
			this.thumbnailManager.DeleteWorkThumbnailIfExists(workThumbnailFileName);
		}
		else
		{
			// 3. どちらもない場合、ThumbnailFileName は空
			editingSeries.ThumbnailFileName = string.Empty;
			editingSeries.ThumbnailStatus = ThumbnailStatus.None;
		}
	}
}

