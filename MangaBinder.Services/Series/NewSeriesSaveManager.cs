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
	/// <returns>保存処理の結果（作品情報と移動失敗素材を含む）。</returns>
	/// <exception cref="InvalidOperationException">SeriesId != 0 の場合。</exception>
	public async ValueTask<SeriesSaveResult> SaveAsync(
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

		// === ManuallyEditedAt の設定 ===
		// 正式登録時に現在日時を設定
		editingSeries.ManuallyEditedAt = DateTime.Now;

		var isWorkSeries = editingSeries.IsWork;
		var workId = editingSeries.WorkId;

		// 同じ NormalizedTitleInternal を持つ正式作品が存在するかを判定
		var hasSameTitleInFormal = this.mangaSeriesStore.All
			.Any(vm => vm.Series.Value.NormalizedTitleInternal == editingSeries.NormalizedTitleInternal);

		// サニタイズ済みフォルダ名を取得（素材移動時に使用）
		// 同じタイトルの正式作品が既に存在する場合は Author プレフィックスを付加
		var materialFolderName = MaterialFolderNameHelper.Create(editingSeries, hasSameTitleInFormal);

		// 削除対象の WorkThumbnail ファイル名を保持する変数
		string? workThumbnailToDelete = null;
		long seriesId = 0;
		MaterialMoveResult moveResult = null!;

		// DB 接続
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();
		using var tx = connection.BeginTransaction();

		try
		{
			// MangaSeries INSERT
			seriesId = await this.mangaRepository.InsertSeriesInTransactionAsync(
				connection,
				tx,
				editingSeries,
				Constants.ViewNames.EditorPage);

			// タグを MangaSeriesTags へ保存
			await this.SaveSeriesTagsInTransactionAsync(connection, tx, seriesId, editingSeries.Tags);

			// 素材移動
			moveResult = await this.materialManager.MoveMaterialsAsync(
				selectedMaterialSourceFolder,
				materialFolderName,
				materialFiles);

			// 素材移動の成否判定：MovedItems が0件の場合は正式登録を成立させない
			if (moveResult.MovedItems.Count == 0)
			{
				// 全件移動失敗：DB をロールバック、WorkThumbnail は削除しない、正式サムネイルも作成しない
				tx.Rollback();

				return new SeriesSaveResult
				{
					Series = null,
					FailedItems = moveResult.FailedItems,
				};
			}

			// MovedItems が1件以上の場合のみサムネイル保存を実行
			workThumbnailToDelete = await this.SaveSeriesThumbnailAsync(connection, tx, seriesId, editingSeries, thumbnailBytes, isWorkSeries, workId);

			// MovedItems が1件以上：正式登録処理を継続
			// MangaSources へ作品フォルダ情報を登録
			var sourceId = await this.mangaRepository.InsertMangaSourceAsync(
				connection,
				tx,
				seriesId,
				moveResult.SeriesFolderPath,
				FolderRole.Material);

			// Material作品フォルダの作成日時を取得してDBへ保存
			var seriesFolderInfo = new DirectoryInfo(moveResult.SeriesFolderPath);
			var materialFolderCreatedAt = seriesFolderInfo.CreationTime;
			await this.mangaRepository.UpdateMaterialFolderCreatedAtAsync(
				connection,
				tx,
				seriesId,
				materialFolderCreatedAt);

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
		}
		catch (Exception ex)
		{
			this.logger?.LogError($"[NewSeriesSaveManager.SaveAsync] DB更新エラー発生。例外: {ex.GetType().Name}, メッセージ: {ex.Message}, スタックトレース: {ex.StackTrace}");

			// TODO: ファイルシステム巻き戻し処理をここに追加予定
			// this.CleanupFileSystemChangesOnDatabaseFailure(editingSeries);

			tx.Rollback();
			throw;
		}

			// === Commit 成功後の処理 ===
			// WorkThumbnail を削除（COMMIT 成功後に削除）
			if (!string.IsNullOrEmpty(workThumbnailToDelete))
			{
				this.thumbnailManager.DeleteWorkThumbnailIfExists(workThumbnailToDelete);
			}

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

			// 3. 再取得した正式作品を Store へ追加
			this.mangaSeriesStore.Add(registeredSeries);

			// 4. 再取得した正式作品を返す
			return new SeriesSaveResult
			{
				Series = registeredSeries,
				FailedItems = moveResult.FailedItems,
			};
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
	/// WorkThumbnail をコピーした場合、削除対象のファイル名を戻り値で返します。
	/// 呼び出し元は戻り値が null でない場合、COMMIT 成功後に DeleteWorkThumbnailIfExists を呼び出してください。
	/// </summary>
	/// <param name="connection">DB接続。</param>
	/// <param name="tx">DBトランザクション。</param>
	/// <param name="seriesId">正式登録に採番された SeriesId。正式サムネイル名の生成に使用します。</param>
	/// <param name="editingSeries">編集中の作品情報。ShortTitle とログ出力に使用します。</param>
	/// <param name="thumbnailBytes">新しいサムネイルのバイト列。null またはLengthが0の場合はスキップします。</param>
	/// <param name="isWorkSeries">登録待ち作品かどうか。</param>
	/// <param name="workId">作品のWorkId。WorkThumbnail ファイル名の生成に使用します。</param>
	/// <returns>削除対象の WorkThumbnail ファイル名、または null。</returns>
	private async ValueTask<string?> SaveSeriesThumbnailAsync(
		SQLiteConnection connection,
		SQLiteTransaction tx,
		long seriesId,
		MangaSeries editingSeries,
		byte[]? thumbnailBytes,
		bool isWorkSeries,
		int workId)
	{
		if (thumbnailBytes != null && thumbnailBytes.Length > 0)
		{
			// 1. thumbnailBytes を正式 Thumbnail へ保存
			// 正式サムネイル名を seriesId と editingSeries.ShortTitle を使用して生成
			// ShortTitle は生成時点でファイル名として安全にサニタイズ済み
			var thumbnailFileNameBase = $"{seriesId:D6}_{editingSeries.ShortTitle}";
			var fileName = $"{thumbnailFileNameBase}.jpg";
			await this.thumbnailManager.SaveThumbnailAsync(fileName, thumbnailBytes);

			editingSeries.ThumbnailFileName = fileName;
			editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;

			// DB に反映（seriesId を明示的に渡す）
			await this.mangaRepository.UpdateSeriesThumbnailAsync(
				connection,
				tx,
				seriesId,
				fileName,
				ThumbnailStatus.Completed);

			return null;
		}
		else if (isWorkSeries)
		{
			// 2. WorkThumbnail が存在する場合、正式 Thumbnail へコピー
			// WorkThumbnailFileNameBase は ShortTitle を含むため、生成時点でサニタイズ済み
			var workThumbnailFileName = $"{editingSeries.WorkThumbnailFileNameBase}.jpg";
			// 正式サムネイル名を seriesId と editingSeries.ShortTitle を使用して生成
			// ShortTitle は生成時点でファイル名として安全にサニタイズ済み
			var thumbnailFileNameBase = $"{seriesId:D6}_{editingSeries.ShortTitle}";
			var thumbnailFileName = $"{thumbnailFileNameBase}.jpg";
			var copied = await this.thumbnailManager.CopyWorkThumbnailToThumbnailAsync(
				workThumbnailFileName,
				thumbnailFileName);

			if (copied)
			{
				editingSeries.ThumbnailFileName = thumbnailFileName;
				editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;

				// DB に反映（seriesId を明示的に渡す）
				await this.mangaRepository.UpdateSeriesThumbnailAsync(
					connection,
					tx,
					seriesId,
					thumbnailFileName,
					ThumbnailStatus.Completed);

				// COMMIT 成功後に削除するため、ファイル名を返す
				return workThumbnailFileName;
			}

			return null;
		}
		else
		{
			// 3. どちらもない場合、ThumbnailFileName は空
			editingSeries.ThumbnailFileName = string.Empty;
			editingSeries.ThumbnailStatus = ThumbnailStatus.None;

			return null;
		}
	}
}

