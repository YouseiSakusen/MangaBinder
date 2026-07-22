using System.Data.SQLite;
using HalationGhost.Utilities;
using MangaBinder.Bindings;
using MangaBinder.Core.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Series;

/// <summary>
/// 既存作品の更新処理を実行するマネージャーです。
/// </summary>
public class ExistingSeriesSaveManager : ISeriesSaveManager
{
	/// <summary>MangaSeries データの取得・操作を担う Repository。</summary>
	private readonly MangaRepository mangaRepository;

	/// <summary>MangaSeries の正本リストを管理するストア。</summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>サムネイル操作を管理する Manager。</summary>
	private readonly ThumbnailManager thumbnailManager;

	/// <summary>素材操作を管理する Manager。</summary>
	private readonly MaterialManager materialManager;

	/// <summary>ログ出力用の Logger。</summary>
	private readonly ILogger<ExistingSeriesSaveManager>? logger;

	/// <summary>
	/// <see cref="ExistingSeriesSaveManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaRepository">MangaSeries データの取得・操作を担う Repository。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="thumbnailManager">サムネイル操作を管理する Manager。</param>
	/// <param name="materialManager">素材操作を管理する Manager。</param>
	/// <param name="logger">ログ出力用の Logger。オプション。</param>
	public ExistingSeriesSaveManager(
		MangaRepository mangaRepository,
		MangaSeriesStore mangaSeriesStore,
		AppSettings appSettings,
		ThumbnailManager thumbnailManager,
		MaterialManager materialManager,
		ILogger<ExistingSeriesSaveManager>? logger = null)
	{
		this.mangaRepository = mangaRepository;
		this.mangaSeriesStore = mangaSeriesStore;
		this.appSettings = appSettings;
		this.thumbnailManager = thumbnailManager;
		this.materialManager = materialManager;
		this.logger = logger;
	}

	/// <summary>
	/// 既存作品の更新処理を実行します。
	/// </summary>
	/// <param name="editingSeries">編集中の作品。SeriesId != 0 かつ IsWork == false である必要があります。</param>
	/// <param name="originalSeries">編集開始時の DeepCopy。null の場合は InvalidOperationException をスロー。</param>
	/// <param name="materialFiles">追加された素材ファイル。</param>
	/// <param name="selectedMaterialSourceFolder">素材の移動先フォルダ。</param>
	/// <param name="thumbnailBytes">新しいサムネイルのバイト列。</param>
	/// <returns>保存後の作品インスタンス。</returns>
	/// <exception cref="InvalidOperationException">editingSeries.SeriesId == 0 または editingSeries.IsWork == true の場合、またはoriginalSeries が null の場合。</exception>
	public async ValueTask<MangaSeries> SaveAsync(
		MangaSeries editingSeries,
		MangaSeries? originalSeries,
		IReadOnlyList<MaterialFile> materialFiles,
		SourceFolder? selectedMaterialSourceFolder,
		byte[]? thumbnailBytes)
	{
		// 入力値の検証
		if (editingSeries.SeriesId == 0 || editingSeries.IsWork)
			throw new InvalidOperationException("SaveAsync は既存の正式作品（SeriesId != 0 かつ IsWork == false）でのみ実行可能です。");

		if (originalSeries == null)
			throw new InvalidOperationException("編集開始時の DeepCopy が見つかりません。");

		this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] 開始。SeriesId: {editingSeries.SeriesId}, Title: {editingSeries.Title}");

		// ===== 編集内容をDeepCopyへ反映 =====
		this.PrepareEditingSeriesDataForUpdate(editingSeries, originalSeries);

		// ===== ファイルシステム更新 =====
		this.ExecuteFileSystemUpdatesForUpdate(originalSeries);

		// ===== DB更新 =====
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		using var tx = connection.BeginTransaction();
		try
		{
			// === DB UPDATE（DeepCopy を対象） ===
			await this.mangaRepository.UpdateSeriesAsync(connection, tx, originalSeries);

			// === MangaSources更新 ===
			// Rename後の新しいPathでMangaSourcesテーブルを更新（Material の MangaSource のみ）
			var renamedMaterialSource = originalSeries.SingleMaterialSource;
			if (renamedMaterialSource != null)
			{
				await this.mangaRepository.UpdateMangaSourcePathAsync(
					connection,
					tx,
					renamedMaterialSource.SourceId,
					renamedMaterialSource.Path);

				this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] MangaSources.Path更新完了。SourceId: {renamedMaterialSource.SourceId}, NewPath: {renamedMaterialSource.Path}");
			}

			// === MangaSeriesTags の更新（DELETE → INSERT） ===
			await this.mangaRepository.ReplaceSeriesTagsInTransactionAsync(connection, tx, originalSeries.SeriesId, originalSeries.Tags);

			// === 追加素材の移動処理 ===
			// CanRemove=true の追加素材を既存の作品素材フォルダへ移動
			// サニタイズ済みフォルダ名を取得（素材移動時に使用）
			var materialFolderName = MaterialFolderNameHelper.Create(originalSeries);

			this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] 素材移動処理開始。materialFiles count: {materialFiles?.Count}, selectedMaterialSourceFolder: {selectedMaterialSourceFolder?.FolderPath.Value}");

			if (materialFiles != null && materialFiles.Count > 0 && selectedMaterialSourceFolder != null)
			{
				var materialSource = originalSeries.SingleMaterialSource;
				this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] materialSource: {materialSource?.Path}");

				if (materialSource != null)
				{
					// CanRemove=true の追加素材のみを抽出
					var addedMaterials = materialFiles
						.Where(m => m.CanRemove)
						.ToList();

					this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] 移動対象素材数（CanRemove=true）: {addedMaterials.Count}");

					if (addedMaterials.Count > 0)
					{
						// 既存の MoveMaterialsAsync を使用して素材を移動
						// selectedMaterialSourceFolder は既存のMangaSource.Pathに対応する素材ルート
						this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] MoveMaterialsAsync実行前。destinationSourceFolder: {selectedMaterialSourceFolder.FolderPath.Value}, materialFolderName: {materialFolderName}");

						var moveResult = await this.materialManager.MoveMaterialsAsync(
							selectedMaterialSourceFolder,
							materialFolderName,
							addedMaterials);

						this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] MoveMaterialsAsync完了。MovedItems: {moveResult.MovedItems.Count}, SkippedItems: {moveResult.SkippedItems.Count}");

						foreach (var movedItem in moveResult.MovedItems)
						{
							this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] 移動完了: {movedItem.SourcePath} -> {movedItem.DestinationPath}");
						}

						foreach (var skippedItem in moveResult.SkippedItems)
						{
							this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] スキップ: {skippedItem.SourcePath} -> {skippedItem.DestinationPath}");
						}
					}
				}
				else
				{
					this.logger?.LogWarning($"[ExistingSeriesSaveManager.SaveAsync] SingleMaterialSource が null です。");
				}
			}
			else
			{
				this.logger?.LogWarning($"[ExistingSeriesSaveManager.SaveAsync] 素材移動条件不満足。materialFiles: {materialFiles?.Count}, selectedMaterialSourceFolder: {(selectedMaterialSourceFolder == null ? "null" : "not null")}");
			}

			// === サムネイル更新 ===
			// TODO: 将来的に以下の処理に拡張される想定：
			// - サムネイルRename
			// - 新規保存
			// - 古いサムネイル削除
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				// サムネイルファイルを保存
				var fileName = $"{FileSystemCharSanitizer.Sanitize(originalSeries.ThumbnailFileNameBase)}.jpg";
				await this.thumbnailManager.SaveThumbnailAsync(fileName, thumbnailBytes);

				// DeepCopy へサムネイル情報を反映
				originalSeries.ThumbnailFileName = fileName;
				originalSeries.ThumbnailStatus = ThumbnailStatus.Completed;

				// DB へサムネイル情報を反映
				await this.mangaRepository.UpdateSeriesThumbnailAsync(
					connection,
					tx,
					originalSeries.SeriesId,
					fileName,
					ThumbnailStatus.Completed);
			}

			// === Commit ===
			tx.Commit();

			// === Commit 成功後、Store 内の正式作品インスタンスを更新 ===
			var storeInstance = this.mangaSeriesStore.FindById(originalSeries.SeriesId);
			if (storeInstance is null)
				throw new InvalidOperationException($"Store から SeriesId {originalSeries.SeriesId} の正式作品が見つかりません。");

			// DeepCopy から Store インスタンスへ編集可能項目をコピー
			this.CopyEditableFieldsFromToEditableToStore(originalSeries, storeInstance);

			// Rename 後の Material MangaSource を Store 内の正本インスタンスへ反映
			this.UpdateMaterialMangaSourceInStore(originalSeries, storeInstance);

			// サムネイル差し替え時の情報反映
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				storeInstance.ThumbnailFileName = originalSeries.ThumbnailFileName;
				storeInstance.ThumbnailStatus = originalSeries.ThumbnailStatus;
			}

			this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] 更新処理完了。SeriesId: {originalSeries.SeriesId}, Title: {originalSeries.Title}");
			return storeInstance;
		}
		catch (Exception ex)
		{
			this.logger?.LogError($"[ExistingSeriesSaveManager.SaveAsync] エラー発生。例外: {ex.GetType().Name}, メッセージ: {ex.Message}, スタックトレース: {ex.StackTrace}");

			// TODO: ファイルシステム巻き戻し処理をここに追加予定
			// this.CleanupFileSystemChangesOnDatabaseFailure(originalSeries);

			tx.Rollback();
			throw;
		}
	}

	/// <summary>
	/// 既存作品更新時に、編集内容をDeepCopyへ反映します。
	/// OwnedMaxVolume の手修正判定、Description の変更判定と出典設定を含みます。
	/// </summary>
	/// <param name="editingSeries">UI入力後の編集中作品。</param>
	/// <param name="originalSeries">編集開始時のDeepCopy。編集内容を反映する対象。</param>
	private void PrepareEditingSeriesDataForUpdate(MangaSeries editingSeries, MangaSeries originalSeries)
	{
		// === OwnedMaxVolume の手修正判定（反映前に実施） ===
		// DeepCopy（編集開始時）と editingSeries（UI 入力後）の OwnedMaxVolume を比較
		var isOwnedMaxVolumeChanged = originalSeries.OwnedMaxVolume != editingSeries.OwnedMaxVolume;
		if (isOwnedMaxVolumeChanged)
		{
			originalSeries.IsOwnedMaxVolumeManuallyEdited = true;
		}
		// 変更がない場合は現在値を維持

		// === Description の変更判定 ===
		// 既存作品では、実際にあらすじが変更された場合のみ出典を変更する
		var isDescriptionChanged = originalSeries.Description != editingSeries.Description;

		// === DeepCopy へ画面入力値を反映 ===
		// 共通処理（UpdateEditingSeriesFromUI で実施済みの値）を DeepCopy へコピー
		this.CopyEditableFieldsFromToEditingToDeepCopy(editingSeries, originalSeries);

		// === Description が変更されている場合のみ出典を設定 ===
		// CopyEditableFieldsFromToEditingToDeepCopy() の後に処理することで、
		// editingSeries の古い出典値で上書きされることを防ぐ
		if (isDescriptionChanged)
		{
			// あらすじが変更された場合、新しい値に基づいて出典を決定
			if (!string.IsNullOrEmpty(editingSeries.Description))
			{
				originalSeries.DescriptionSource = DescriptionSource.Manual;
				originalSeries.DescriptionSourceTitle = string.Empty;
			}
			else
			{
				originalSeries.DescriptionSource = DescriptionSource.None;
				originalSeries.DescriptionSourceTitle = string.Empty;
			}
		}
		// 変更がない場合は、CopyEditableFieldsFromToEditingToDeepCopy() によりコピーされた
		// DescriptionSource / DescriptionSourceTitle をそのまま利用する
	}

	/// <summary>
	/// 既存作品更新時のファイルシステム関連操作を実行します。
	/// 素材フォルダのRename、サムネイルの処理などがここに集約されます。
	/// </summary>
	/// <param name="editedSeries">編集内容を反映したDeepCopy。</param>
	private void ExecuteFileSystemUpdatesForUpdate(MangaSeries editedSeries)
	{
		// === 素材フォルダ名変更の実行 ===
		// 必要に応じてRenameを実行し、MangaSource.Path を更新
		// Rename失敗時は例外をそのまま throw（EditorPageViewModel の catch で処理）
		this.PerformMaterialFolderRenameIfNeeded(editedSeries, editedSeries);

		// TODO: サムネイルRename処理をここに追加予定

		// TODO: クリーンアップ処理をここに追加予定
	}

	/// <summary>
	/// DB更新が失敗した場合の巻き戻し処理（クリーンアップ）を実行します。
	/// ファイルシステム上の変更を元に戻します。
	/// 現在の実装では実装されていません。
	/// </summary>
	/// <param name="editedSeries">編集内容を反映したDeepCopy。</param>
	private void CleanupFileSystemChangesOnDatabaseFailure(MangaSeries editedSeries)
	{
		// TODO: ファイルシステムの巻き戻し処理（素材フォルダRename、サムネイル削除など）
		// DB更新失敗時に呼び出され、ファイルシステムの変更を元に戻す
	}

	/// <summary>
	/// 既存作品更新時に、素材フォルダ名の変更判定を実施します。
	/// 編集後の作品情報から生成される期待フォルダ名と、現在登録されている素材フォルダ名を比較し、
	/// Rename の必要性を判定します。フォルダ存在確認も事前に行い、問題がある場合は適切な状態を返します。
	/// </summary>
	/// <param name="editedSeries">編集後の保存用 DeepCopy。期待フォルダ名の生成に使用されます。</param>
	/// <param name="originalSeries">編集開始時の DeepCopy。現在の素材フォルダ情報を取得するために使用されます。</param>
	/// <returns>フォルダ名変更判定結果。Ok の場合のみ保存処理を続行してください。</returns>
	private MaterialFolderRenameCheckResult CheckMaterialFolderRenameStatus(MangaSeries editedSeries, MangaSeries originalSeries)
	{
		// 素材フォルダが登録されていない場合は判定不要
		var originalMaterialSource = originalSeries.Sources.FirstOrDefault(s => s.Role == FolderRole.Material);
		if (originalMaterialSource == null)
		{
			// 素材フォルダが登録されていない場合は問題なし
			return MaterialFolderRenameCheckResult.Ok;
		}

		// 現在フォルダ名を取得
		var currentFolderPath = originalMaterialSource.Path;
		var currentFolderName = Path.GetFileName(currentFolderPath);

		// 期待フォルダ名を生成（編集後の作品情報を使用）
		var expectedFolderName = MaterialFolderNameHelper.Create(editedSeries);

		// 大小文字を区別しない比較
		if (string.Equals(currentFolderName, expectedFolderName, StringComparison.OrdinalIgnoreCase))
		{
			// 名前が一致している
			return MaterialFolderRenameCheckResult.Ok;
		}

		// 現在フォルダが物理的に存在するか確認
		if (!Directory.Exists(currentFolderPath))
		{
			return MaterialFolderRenameCheckResult.CurrentFolderNotFound;
		}

		// Rename先パスを生成
		var parentDirectoryPath = Path.GetDirectoryName(currentFolderPath);
		if (string.IsNullOrEmpty(parentDirectoryPath))
		{
			// 親ディレクトリが取得できない場合は Rename 不可
			throw new InvalidOperationException($"素材フォルダの親ディレクトリを取得できません。Path: {currentFolderPath}");
		}

		var renameTargetPath = Path.Combine(parentDirectoryPath, expectedFolderName);

		// Rename先フォルダが既に存在するか確認
		if (Directory.Exists(renameTargetPath))
		{
			return MaterialFolderRenameCheckResult.RenameTargetAlreadyExists;
		}

		// Rename が必要
		return MaterialFolderRenameCheckResult.RenameNeeded;
	}

	/// <summary>
	/// 既存作品更新時に、素材フォルダ名の変更判定を実施し、必要に応じて実際にRenameを行います。
	/// CheckMaterialFolderRenameStatus() で RenameNeeded と判定された場合のみ、
	/// Directory.Move() を使用してRenameを実行し、MangaSource.Path を更新します。
	/// Rename失敗時は例外をそのまま throw します。
	/// </summary>
	/// <param name="editedSeries">編集後の保存用 DeepCopy。期待フォルダ名の生成に使用されます。</param>
	/// <param name="originalSeries">編集開始時の DeepCopy。現在の素材フォルダ情報を取得・更新するために使用されます。</param>
	private void PerformMaterialFolderRenameIfNeeded(MangaSeries editedSeries, MangaSeries originalSeries)
	{
		// 素材フォルダ名変更の判定
		var folderRenameCheckResult = this.CheckMaterialFolderRenameStatus(editedSeries, originalSeries);

		// Ok の場合はRenameが不要なため、処理をスキップ
		if (folderRenameCheckResult == MaterialFolderRenameCheckResult.Ok)
		{
			return;
		}

		// Ok 以外の場合は例外を投げて保存処理を中止（EditorPageViewModel の catch で処理）
		if (folderRenameCheckResult != MaterialFolderRenameCheckResult.RenameNeeded)
		{
			throw new InvalidOperationException($"素材フォルダ名の変更判定でエラーが発生しました。結果: {folderRenameCheckResult}");
		}

		// === RenameNeeded の場合のみここに到達 ===
		// 素材フォルダが登録されていることは CheckMaterialFolderRenameStatus() で確認済み
		var materialSourceIndex = originalSeries.Sources.FindIndex(s => s.Role == FolderRole.Material);
		if (materialSourceIndex < 0)
		{
			// 通常ここには到達しない（CheckMaterialFolderRenameStatus() で確認済み）
			throw new InvalidOperationException("素材フォルダが見つかりません。");
		}

		var currentMaterialSource = originalSeries.Sources[materialSourceIndex];
		var currentFolderPath = currentMaterialSource.Path;
		var parentDirectoryPath = Path.GetDirectoryName(currentFolderPath);
		if (string.IsNullOrEmpty(parentDirectoryPath))
		{
			throw new InvalidOperationException($"素材フォルダの親ディレクトリを取得できません。Path: {currentFolderPath}");
		}

		var expectedFolderName = MaterialFolderNameHelper.Create(editedSeries);
		var newFolderPath = Path.Combine(parentDirectoryPath, expectedFolderName);

		// === Directory.Move() でRenameを実行 ===
		try
		{
			Directory.Move(currentFolderPath, newFolderPath);
			this.logger?.LogInformation($"[PerformMaterialFolderRenameIfNeeded] Rename成功。 {currentFolderPath} -> {newFolderPath}");
		}
		catch (Exception ex)
		{
			this.logger?.LogError($"[PerformMaterialFolderRenameIfNeeded] Rename失敗。例外: {ex.GetType().Name}, メッセージ: {ex.Message}");
			throw;
		}

		// === MangaSource.Path を更新（Path は init-only なので新しいインスタンスで置き換え） ===
		var newMaterialSource = new MangaSource
		{
			SourceId = currentMaterialSource.SourceId,
			SeriesId = currentMaterialSource.SeriesId,
			Path = newFolderPath,
			Role = currentMaterialSource.Role
		};
		originalSeries.Sources[materialSourceIndex] = newMaterialSource;

		this.logger?.LogInformation($"[PerformMaterialFolderRenameIfNeeded] MangaSource.Path更新。SeriesId: {originalSeries.SeriesId}, OldPath: {currentFolderPath}, NewPath: {newFolderPath}");
	}

	/// <summary>
	/// UI 入力後の editingSeries から DeepCopy へ、編集可能項目をコピーします。
	/// OwnedMaxVolume の手修正判定の後、UI 値を DeepCopy へ反映する際に使用されます。
	/// </summary>
	private void CopyEditableFieldsFromToEditingToDeepCopy(MangaSeries source, MangaSeries destination)
	{
		destination.Title = source.Title;
		destination.Author = source.Author;
		destination.Publisher = source.Publisher;
		destination.Description = source.Description;
		destination.Memo = source.Memo;
		destination.NormalizedTitleInternal = source.NormalizedTitleInternal;
		destination.ShortTitle = source.ShortTitle;
		destination.StartVolume = source.StartVolume;
		destination.EndVolume = source.EndVolume;
		destination.SeriesCompleted = source.SeriesCompleted;
		destination.IsOwnedCompleted = source.IsOwnedCompleted;
		destination.OwnedMaxVolume = source.OwnedMaxVolume;
		destination.DescriptionSource = source.DescriptionSource;
		destination.DescriptionSourceTitle = source.DescriptionSourceTitle;
		destination.GoogleBooksImportStatus = source.GoogleBooksImportStatus;
		destination.GoogleBooksImportedAt = source.GoogleBooksImportedAt;
		destination.GoogleBooksImportMessage = source.GoogleBooksImportMessage;

		// タグもコピー
		destination.Tags.Clear();
		foreach (var tag in source.Tags)
		{
			destination.Tags.Add(tag);
		}
	}

	/// <summary>
	/// DB 更新成功後、DeepCopy（編集対象）から Store 内インスタンスへ編集可能項目をコピーします。
	/// Store への反映は Commit 成功後のみ実施されます。
	/// </summary>
	private void CopyEditableFieldsFromToEditableToStore(MangaSeries source, MangaSeries destination)
	{
		destination.Title = source.Title;
		destination.Author = source.Author;
		destination.Publisher = source.Publisher;
		destination.Description = source.Description;
		destination.Memo = source.Memo;
		destination.NormalizedTitleInternal = source.NormalizedTitleInternal;
		destination.ShortTitle = source.ShortTitle;
		destination.StartVolume = source.StartVolume;
		destination.EndVolume = source.EndVolume;
		destination.SeriesCompleted = source.SeriesCompleted;
		destination.IsOwnedCompleted = source.IsOwnedCompleted;
		destination.OwnedMaxVolume = source.OwnedMaxVolume;
		destination.IsOwnedMaxVolumeManuallyEdited = source.IsOwnedMaxVolumeManuallyEdited;
		destination.DescriptionSource = source.DescriptionSource;
		destination.DescriptionSourceTitle = source.DescriptionSourceTitle;

		// タグもコピー
		destination.Tags.Clear();
		foreach (var tag in source.Tags)
		{
			destination.Tags.Add(tag);
		}
	}

	/// <summary>
	/// Rename 後の Material フォルダ用 MangaSource を Store 内の正本インスタンスへ反映します。
	/// DeepCopy 内の Material MangaSource を使用して、Store 内の対応する要素を置き換えます。
	/// </summary>
	/// <param name="originalSeries">Rename 後の新しい Path を保持する DeepCopy。</param>
	/// <param name="storeInstance">Store 内の正本インスタンス。</param>
	private void UpdateMaterialMangaSourceInStore(MangaSeries originalSeries, MangaSeries storeInstance)
	{
		// Rename 後の Material MangaSource を取得
		var renamedMaterialSource = originalSeries.SingleMaterialSource;
		if (renamedMaterialSource == null)
		{
			// Material MangaSource が存在しない場合は更新不要
			return;
		}

		// Store 内で既に Material MangaSource が存在するか確認
		var storeExistingMaterialSource = storeInstance.Sources
			.FirstOrDefault(s => s.Role == FolderRole.Material);

		if (storeExistingMaterialSource != null)
		{
			// 既存の Material MangaSource を新しいインスタンスに置き換え
			var newMangaSource = new MangaSource
			{
				SourceId = renamedMaterialSource.SourceId,
				SeriesId = renamedMaterialSource.SeriesId,
				Path = renamedMaterialSource.Path,
				Role = renamedMaterialSource.Role,
			};

			var index = storeInstance.Sources.IndexOf(storeExistingMaterialSource);
			if (index >= 0)
			{
				storeInstance.Sources[index] = newMangaSource;
				this.logger?.LogInformation(
					$"[UpdateMaterialMangaSourceInStore] Store 内の Material MangaSource を更新。SourceId: {renamedMaterialSource.SourceId}, NewPath: {renamedMaterialSource.Path}");
			}
		}
		else
		{
			// Store 内に Material MangaSource が存在しない場合は、originalSeries に存在する場合のみ追加
			if (originalSeries.Sources.Any(s => s.Role == FolderRole.Material))
			{
				var newMangaSource = new MangaSource
				{
					SourceId = renamedMaterialSource.SourceId,
					SeriesId = renamedMaterialSource.SeriesId,
					Path = renamedMaterialSource.Path,
					Role = renamedMaterialSource.Role,
				};

				storeInstance.Sources.Add(newMangaSource);
				this.logger?.LogInformation(
					$"[UpdateMaterialMangaSourceInStore] Store 内に Material MangaSource を追加。SourceId: {renamedMaterialSource.SourceId}, Path: {renamedMaterialSource.Path}");
			}
		}
	}
}
