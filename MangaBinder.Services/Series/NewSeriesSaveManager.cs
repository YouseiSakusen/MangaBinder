using System.Data.SQLite;
using System.Text;
using Dapper;
using MangaBinder.Bindings;
using MangaBinder.Core.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;

namespace MangaBinder.Series;

/// <summary>
/// 新規作品の保存処理を実行するマネージャーです。
/// 新規作品と登録待ち作品の正式登録処理を担当します。
/// </summary>
public class NewSeriesSaveManager : ISeriesSaveManager
{
	/// <summary>MangaSeries の取得を担う Repository。</summary>
	private readonly MangaRepository mangaRepository;

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
	/// <param name="mangaRepository">MangaSeries の取得を担う Repository。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="thumbnailManager">サムネイル操作を管理する Manager。</param>
	/// <param name="materialManager">素材操作を管理する Manager。</param>
	public NewSeriesSaveManager(
		MangaRepository mangaRepository,
		MangaSeriesStore mangaSeriesStore,
		AppSettings appSettings,
		ThumbnailManager thumbnailManager,
		MaterialManager materialManager)
	{
		this.mangaRepository = mangaRepository;
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

		// DB 接続
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();
		using var tx = connection.BeginTransaction();

		try
		{
			// MangaSeries INSERT
			var insertSql = new StringBuilder();
			insertSql.AppendLine(" INSERT INTO MangaSeries ( ");
			insertSql.AppendLine(" 	  NormalizedTitleInternal ");
			insertSql.AppendLine(" 	, Title ");
			insertSql.AppendLine(" 	, ShortTitle ");
			insertSql.AppendLine(" 	, Author ");
			insertSql.AppendLine(" 	, Description ");
			insertSql.AppendLine(" 	, SeriesCompleted ");
			insertSql.AppendLine(" 	, IsOwnedCompleted ");
			insertSql.AppendLine(" 	, StartVolume ");
			insertSql.AppendLine(" 	, EndVolume ");
			insertSql.AppendLine(" 	, OwnedMaxVolume ");
			insertSql.AppendLine(" 	, NormalizedTitleExternal ");
			insertSql.AppendLine(" 	, ThumbnailFileName ");
			insertSql.AppendLine(" 	, ThumbnailStatus ");
			insertSql.AppendLine(" 	, Publisher ");
			insertSql.AppendLine(" 	, GoogleBooksImportStatus ");
			insertSql.AppendLine(" 	, DescriptionSource ");
			insertSql.AppendLine(" 	, Memo ");
			insertSql.AppendLine(" 	, HasNestedArchive ");
			insertSql.AppendLine(" ) VALUES ( ");
			insertSql.AppendLine(" 	  :NormalizedTitleInternal ");
			insertSql.AppendLine(" 	, :Title ");
			insertSql.AppendLine(" 	, :ShortTitle ");
			insertSql.AppendLine(" 	, :Author ");
			insertSql.AppendLine(" 	, :Description ");
			insertSql.AppendLine(" 	, :SeriesCompleted ");
			insertSql.AppendLine(" 	, :IsOwnedCompleted ");
			insertSql.AppendLine(" 	, :StartVolume ");
			insertSql.AppendLine(" 	, :EndVolume ");
			insertSql.AppendLine(" 	, :OwnedMaxVolume ");
			insertSql.AppendLine(" 	, :NormalizedTitleExternal ");
			insertSql.AppendLine(" 	, :ThumbnailFileName ");
			insertSql.AppendLine(" 	, :ThumbnailStatus ");
			insertSql.AppendLine(" 	, :Publisher ");
			insertSql.AppendLine(" 	, :GoogleBooksImportStatus ");
			insertSql.AppendLine(" 	, :DescriptionSource ");
			insertSql.AppendLine(" 	, :Memo ");
			insertSql.AppendLine(" 	, :HasNestedArchive ");
			insertSql.AppendLine(" ) ");
			insertSql.AppendLine(" RETURNING SeriesId; ");

			var seriesId = await connection.QuerySingleAsync<long>(insertSql.ToString(), new
			{
				NormalizedTitleInternal = MangaTitleHelper.NormalizeTitleInternal(editingSeries.Title),
				editingSeries.Title,
				editingSeries.ShortTitle,
				editingSeries.Author,
				editingSeries.Description,
				editingSeries.SeriesCompleted,
				editingSeries.IsOwnedCompleted,
				editingSeries.StartVolume,
				editingSeries.EndVolume,
				editingSeries.OwnedMaxVolume,
				editingSeries.NormalizedTitleExternal,
				ThumbnailFileName = string.Empty,
				ThumbnailStatus = (int)ThumbnailStatus.None,
				editingSeries.Publisher,
				GoogleBooksImportStatus = (int)GoogleBooksImportStatus.NotImported,
				DescriptionSource = (int)DescriptionSource.None,
				editingSeries.Memo,
				editingSeries.HasNestedArchive,
			}, tx);

			// SeriesId を editingSeries に反映
			editingSeries.SeriesId = seriesId;

			// タグを MangaSeriesTags へ保存
			await this.SaveSeriesTagsInTransactionAsync(connection, tx, seriesId, editingSeries.Tags);

			// サムネイル保存
			await this.SaveSeriesThumbnailAsync(connection, tx, editingSeries, thumbnailBytes, isWorkSeries, workId);

			// 素材移動
			var moveResult = await this.materialManager.MoveMaterialsAsync(
				selectedMaterialSourceFolder,
				editingSeries.MaterialFolderName,
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
				var deleteWorkTagsSql = new StringBuilder();
				deleteWorkTagsSql.AppendLine(" DELETE FROM WorkMangaSeriesTags ");
				deleteWorkTagsSql.AppendLine(" WHERE ");
				deleteWorkTagsSql.AppendLine(" 	WorkId = :WorkId; ");

				await connection.ExecuteAsync(deleteWorkTagsSql.ToString(), new { WorkId = workId }, tx);

				// WorkMangaSeries を削除
				var deleteWorkSeriesSql = new StringBuilder();
				deleteWorkSeriesSql.AppendLine(" DELETE FROM WorkMangaSeries ");
				deleteWorkSeriesSql.AppendLine(" WHERE ");
				deleteWorkSeriesSql.AppendLine(" 	WorkId = :WorkId; ");

				await connection.ExecuteAsync(deleteWorkSeriesSql.ToString(), new { WorkId = workId }, tx);
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

			// 3. 再取得した正式作品を Store へ追加
			this.mangaSeriesStore.Add(registeredSeries);

			// 4. 再取得した正式作品を返す
			return registeredSeries;
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
	/// TagId &lt;= 0 のタグは保存対象外となります（未保存タグの防御）。
	/// </summary>
	private async ValueTask SaveSeriesTagsInTransactionAsync(
		SQLiteConnection connection,
		SQLiteTransaction transaction,
		long seriesId,
		IEnumerable<MangaTag> tags)
	{
		var insertSql = new StringBuilder();
		insertSql.AppendLine(" INSERT INTO MangaSeriesTags ( ");
		insertSql.AppendLine(" 	  SeriesId ");
		insertSql.AppendLine(" 	, TagId ");
		insertSql.AppendLine(" ) VALUES ( ");
		insertSql.AppendLine(" 	  :SeriesId ");
		insertSql.AppendLine(" 	, :TagId ");
		insertSql.AppendLine(" ); ");

		// TagId > 0 のタグのみ保存（未保存タグ TagId=0 は除外）
		var validTags = tags.Where(t => t.TagId > 0).ToList();
		foreach (var tag in validTags)
		{
			await connection.ExecuteAsync(
				insertSql.ToString(),
				new { SeriesId = seriesId, TagId = tag.TagId },
				transaction);
		}
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
			var fileName = $"{editingSeries.ThumbnailFileNameBase}.jpg";
			await this.thumbnailManager.SaveThumbnailAsync(fileName, thumbnailBytes);

			editingSeries.ThumbnailFileName = fileName;
			editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;

			// DB に反映
			var updateSql = new StringBuilder();
			updateSql.AppendLine(" UPDATE MangaSeries ");
			updateSql.AppendLine(" SET ");
			updateSql.AppendLine(" 	  ThumbnailFileName = :ThumbnailFileName ");
			updateSql.AppendLine(" 	, ThumbnailStatus = :ThumbnailStatus ");
			updateSql.AppendLine(" WHERE ");
			updateSql.AppendLine(" 	SeriesId = :SeriesId; ");

			await connection.ExecuteAsync(updateSql.ToString(), new
			{
				ThumbnailFileName = fileName,
				ThumbnailStatus = (int)ThumbnailStatus.Completed,
				SeriesId = editingSeries.SeriesId,
			}, tx);
		}
		else if (isWorkSeries)
		{
			// 2. WorkThumbnail が存在する場合、正式 Thumbnail へコピー
			var workThumbnailFileName = $"{editingSeries.WorkThumbnailFileNameBase}.jpg";
			var copied = await this.thumbnailManager.CopyWorkThumbnailToThumbnailAsync(
				workThumbnailFileName,
				$"{editingSeries.ThumbnailFileNameBase}.jpg");

			if (copied)
			{
				editingSeries.ThumbnailFileName = $"{editingSeries.ThumbnailFileNameBase}.jpg";
				editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;

				// DB に反映
				var updateSql = new StringBuilder();
				updateSql.AppendLine(" UPDATE MangaSeries ");
				updateSql.AppendLine(" SET ");
				updateSql.AppendLine(" 	  ThumbnailFileName = :ThumbnailFileName ");
				updateSql.AppendLine(" 	, ThumbnailStatus = :ThumbnailStatus ");
				updateSql.AppendLine(" WHERE ");
				updateSql.AppendLine(" 	SeriesId = :SeriesId; ");

				await connection.ExecuteAsync(updateSql.ToString(), new
				{
					ThumbnailFileName = editingSeries.ThumbnailFileName,
					ThumbnailStatus = (int)ThumbnailStatus.Completed,
					SeriesId = editingSeries.SeriesId,
				}, tx);
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

