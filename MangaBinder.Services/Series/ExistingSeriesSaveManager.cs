using System.Data.SQLite;
using HalationGhost.Utilities;
using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Series;

/// <summary>
/// 既存作品の更新処理を実行するマネージャーです。
/// 編集用クローン（<see cref="EditorStore.EditTarget"/>）を保存処理の作業対象とし、DB Commit 後に正本へ反映します。
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
	/// 編集内容は編集用クローン（<paramref name="editingSeries"/>）へ反映済みである必要があります。
	/// 保存処理は編集用クローンを作業対象とし、DB Commit 成功後に正本へ <see cref="MangaSeries.UpdateValues(MangaSeries)"/> で反映して、共有 Notifier へ 1 回だけ通知します。
	/// </summary>
	/// <param name="editingSeries">編集用クローン。SeriesId != 0 かつ IsWork == false である必要があります。</param>
	/// <param name="originalSeries">編集開始時の正本。比較用・参照用であり、DB Commit 成功前には変更しません。null の場合は InvalidOperationException をスロー。</param>
	/// <param name="materialFiles">追加された素材ファイル。</param>
	/// <param name="selectedMaterialSourceFolder">素材の移動先フォルダ。</param>
	/// <param name="thumbnailBytes">新しいサムネイルのバイト列。</param>
	/// <returns>保存処理の結果（作品情報と移動失敗素材を含む）。</returns>
	/// <exception cref="InvalidOperationException">editingSeries.SeriesId == 0 または editingSeries.IsWork == true の場合、originalSeries が null、または編集用クローンと同一インスタンスの場合。</exception>
	public async ValueTask<SeriesSaveResult> SaveAsync(
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
			throw new InvalidOperationException("編集開始時の正本が見つかりません。");

		if (ReferenceEquals(originalSeries, editingSeries))
			throw new InvalidOperationException("編集用クローンと正本は別のインスタンスである必要があります。");

		this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] 開始。SeriesId: {editingSeries.SeriesId}, Title: {editingSeries.Title}");

		// 実在する素材ソースを特定する（実在しない古い Material レコードは、物理フォルダとして扱わない）
		var materialSourceId = MaterialSourceAvailabilityHelper.ResolveEditingMaterialSource(editingSeries)?.SourceId;
		var targetMaterialSourceId = materialSourceId;

		// ===== 編集内容の保存前処理（編集用クローンへ反映） =====
		this.PrepareEditingSeriesDataForUpdate(editingSeries, originalSeries);

		// ===== ファイルシステム更新 =====
		this.ExecuteFileSystemUpdatesForUpdate(editingSeries, materialSourceId);

		// ===== DB更新 =====
		using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
		await connection.OpenAsync();

		using var tx = connection.BeginTransaction();
		try
		{
			// === DB UPDATE（編集用クローンを対象） ===
			await this.mangaRepository.UpdateSeriesAsync(connection, tx, editingSeries, Constants.ViewNames.EditorPage);

			// === MangaSources更新 ===
			// Rename後の新しいPathでMangaSourcesテーブルを更新（実在する Material の MangaSource のみ）
			var renamedMaterialSource = this.findSourceById(editingSeries, materialSourceId);
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
			await this.mangaRepository.ReplaceSeriesTagsInTransactionAsync(connection, tx, editingSeries.SeriesId, editingSeries.Tags);

			// === 追加素材の移動処理 ===
			// CanRemove=true の追加素材を作品素材フォルダへ移動する
			var addedMaterials = materialFiles.Where(m => m.CanRemove).ToList();
			if (addedMaterials.Count > 0 && selectedMaterialSourceFolder != null)
			{
				targetMaterialSourceId = await this.moveAddedMaterialsAsync(
					connection,
					tx,
					editingSeries,
					materialSourceId,
					addedMaterials,
					selectedMaterialSourceFolder);
			}

			// === サムネイル更新 ===
			// TODO: 将来的に以下の処理に拡張される想定：
			// - サムネイルRename
			// - 新規保存
			// - 古いサムネイル削除
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				// サムネイルファイルを保存
				var fileName = $"{FileSystemCharSanitizer.Sanitize(editingSeries.ThumbnailFileNameBase)}.jpg";
				await this.thumbnailManager.SaveThumbnailAsync(fileName, thumbnailBytes);

				// 編集用クローンへサムネイル情報を反映
				editingSeries.ThumbnailFileName = fileName;
				editingSeries.ThumbnailStatus = ThumbnailStatus.Completed;

				// DB へサムネイル情報を反映
				await this.mangaRepository.UpdateSeriesThumbnailAsync(
					connection,
					tx,
					editingSeries.SeriesId,
					fileName,
					ThumbnailStatus.Completed);
			}

			// === Commit ===
			tx.Commit();
		}
		catch (Exception ex)
		{
			this.logger?.LogError($"[ExistingSeriesSaveManager.SaveAsync] DB更新エラー発生。例外: {ex.GetType().Name}, メッセージ: {ex.Message}, スタックトレース: {ex.StackTrace}");

			// TODO: ファイルシステム巻き戻し処理をここに追加予定
			// this.CleanupFileSystemChangesOnDatabaseFailure(editingSeries);

			tx.Rollback();
			throw;
		}

		// === Commit 成功後、Store 内の正式作品インスタンスを更新 ===
		var storeViewModel = this.mangaSeriesStore.FindNotifierById(editingSeries.SeriesId);
		if (storeViewModel is null)
			throw new InvalidOperationException($"Store から SeriesId {editingSeries.SeriesId} の正式作品が見つかりません。");

		var storeInstance = storeViewModel.Series.Value;

		// 編集用クローンから正本インスタンスへ、作品情報を反映する（正本インスタンスは維持する）
		storeInstance.UpdateValues(editingSeries);

		// Rename 後（または新規登録・Path 更新された）Material MangaSource を正本インスタンスへ反映
		this.UpdateMaterialMangaSourceInStore(editingSeries, storeInstance, targetMaterialSourceId);

		// サムネイル差し替え時の情報反映
		if (thumbnailBytes != null && thumbnailBytes.Length > 0)
		{
			storeInstance.ThumbnailFileName = editingSeries.ThumbnailFileName;
			storeInstance.ThumbnailStatus = editingSeries.ThumbnailStatus;
		}

		// 正本への全ての反映が完了したので、共有 MangaSeriesNotifier.Series に 1 回だけ通知
		this.mangaSeriesStore.NotifySeriesChanged(editingSeries.SeriesId);

		this.logger?.LogInformation($"[ExistingSeriesSaveManager.SaveAsync] 更新処理完了。SeriesId: {editingSeries.SeriesId}, Title: {editingSeries.Title}");
		return new SeriesSaveResult
		{
			Series = storeInstance,
			FailedItems = [],
		};
	}

	/// <summary>
	/// 追加素材を選択された素材ルートの作品素材フォルダへ移動し、必要に応じて Material の MangaSource を登録・更新します。
	/// 実在する素材ソースがある場合は、現在の物理フォルダが作者付き形式かで作品素材フォルダ名を決定します。
	/// 実在する素材ソースがない場合は、同一正規化タイトルの別の正式作品があるかで決定し、
	/// 古い Material レコードが 1 件あればその SourceId を再利用して Path を更新し、なければ新規登録します。
	/// </summary>
	/// <param name="connection">DB 接続。</param>
	/// <param name="tx">更新中のトランザクション。</param>
	/// <param name="editingSeries">編集用クローン。新しい MangaSource を Sources へ反映します。</param>
	/// <param name="materialSourceId">実在する素材ソースの SourceId。実在する素材フォルダがない場合は null。</param>
	/// <param name="addedMaterials">移動する追加素材。</param>
	/// <param name="selectedMaterialSourceFolder">素材の移動先の素材ルート。</param>
	/// <returns>反映対象の素材ソースの SourceId。素材ソースがない場合は null。</returns>
	/// <exception cref="InvalidOperationException">実在する素材フォルダがなく、更新対象を一意に決められない古い Material レコードが複数ある場合。</exception>
	private async ValueTask<long?> moveAddedMaterialsAsync(
		SQLiteConnection connection,
		SQLiteTransaction tx,
		MangaSeries editingSeries,
		long? materialSourceId,
		IReadOnlyList<MaterialFile> addedMaterials,
		SourceFolder selectedMaterialSourceFolder)
	{
		var materialSource = this.findSourceById(editingSeries, materialSourceId);

		// 古い Material レコードの扱いは、素材を移動する前に確定する
		var staleMaterialSource = materialSource is null
			? MaterialSourceAvailabilityHelper.FindReusableStaleMaterialSource(editingSeries)
			: null;

		bool includeAuthorPrefix;
		if (materialSource != null)
		{
			// 現在の物理フォルダが作者付き形式かで期待フォルダ名を生成
			includeAuthorPrefix = FileSystemNameHelper.TryExtractAuthorPrefix(Path.GetFileName(materialSource.Path), out _, out _);
		}
		else
		{
			// 自分以外に同一 NormalizedTitleInternal の正式作品がいるかで著者プレフィックスを決定
			includeAuthorPrefix = this.mangaSeriesStore.All
				.Where(vm => vm.Series.Value.SeriesId != editingSeries.SeriesId)
				.Any(vm => string.Equals(
					vm.Series.Value.NormalizedTitleInternal,
					editingSeries.NormalizedTitleInternal,
					StringComparison.Ordinal));
		}

		var materialFolderName = MaterialFolderNameHelper.Create(editingSeries, includeAuthorPrefix);

		this.logger?.LogInformation($"[ExistingSeriesSaveManager.moveAddedMaterialsAsync] 素材移動開始。移動対象素材数: {addedMaterials.Count}, destinationSourceFolder: {selectedMaterialSourceFolder.FolderPath.Value}, materialFolderName: {materialFolderName}, includeAuthorPrefix: {includeAuthorPrefix}");

		var moveResult = await this.materialManager.MoveMaterialsAsync(
			selectedMaterialSourceFolder,
			materialFolderName,
			addedMaterials);

		this.logger?.LogInformation($"[ExistingSeriesSaveManager.moveAddedMaterialsAsync] MoveMaterialsAsync完了。MovedItems: {moveResult.MovedItems.Count}, SkippedItems: {moveResult.SkippedItems.Count}, SeriesFolderPath: {moveResult.SeriesFolderPath}");

		if (materialSource != null)
		{
			return materialSource.SourceId;
		}

		if (staleMaterialSource != null)
		{
			// 古い Material レコードの SourceId を再利用し、Path を新しい保存先へ更新する
			await this.mangaRepository.UpdateMangaSourcePathAsync(connection, tx, staleMaterialSource.SourceId, moveResult.SeriesFolderPath);

			var staleIndex = editingSeries.Sources.FindIndex(s => s.SourceId == staleMaterialSource.SourceId);
			editingSeries.Sources[staleIndex] = new MangaSource
			{
				SourceId = staleMaterialSource.SourceId,
				SeriesId = staleMaterialSource.SeriesId,
				Path = moveResult.SeriesFolderPath,
				Role = FolderRole.Material,
			};

			this.logger?.LogInformation($"[ExistingSeriesSaveManager.moveAddedMaterialsAsync] 古い MaterialSource を再利用して Path を更新。SourceId: {staleMaterialSource.SourceId}, Path: {moveResult.SeriesFolderPath}");
			return staleMaterialSource.SourceId;
		}

		// MangaSources へ新しい MaterialSource を登録
		var newSourceId = await this.mangaRepository.InsertMangaSourceAsync(
			connection,
			tx,
			editingSeries.SeriesId,
			moveResult.SeriesFolderPath,
			FolderRole.Material);

		editingSeries.Sources.Add(new MangaSource
		{
			SourceId = newSourceId,
			SeriesId = editingSeries.SeriesId,
			Path = moveResult.SeriesFolderPath,
			Role = FolderRole.Material,
		});

		this.logger?.LogInformation($"[ExistingSeriesSaveManager.moveAddedMaterialsAsync] MangaSources へ新規登録完了。SourceId: {newSourceId}, Path: {moveResult.SeriesFolderPath}");
		return newSourceId;
	}

	/// <summary>
	/// 既存作品更新時に、保存前の編集用クローンの状態を整えます。
	/// 編集内容は編集用クローンへ反映済みのため、再コピーは行いません。
	/// ManuallyEditedAt の設定と、Description の変更判定に基づく出典設定を行います。
	/// OwnedMaxVolume の手動変更フラグは、<see cref="EditorStore"/> が入力時に編集用クローンへ反映済みです。
	/// </summary>
	/// <param name="editingSeries">編集内容を反映済みの編集用クローン。</param>
	/// <param name="originalSeries">編集開始時の正本。Description の変更判定に使用する読み取り専用の参照です。</param>
	private void PrepareEditingSeriesDataForUpdate(MangaSeries editingSeries, MangaSeries originalSeries)
	{
		// === ManuallyEditedAt の設定 ===
		// 編集画面から保存される場合、現在日時を設定
		editingSeries.ManuallyEditedAt = DateTime.Now;

		// === Description が変更されている場合のみ出典を設定 ===
		// 既存作品では、実際にあらすじが変更された場合のみ出典を変更する。変更がない場合は現在の出典値を維持する
		if (originalSeries.Description != editingSeries.Description)
		{
			editingSeries.DescriptionSource = string.IsNullOrEmpty(editingSeries.Description)
				? DescriptionSource.None
				: DescriptionSource.Manual;
			editingSeries.DescriptionSourceTitle = string.Empty;
		}
	}

	/// <summary>
	/// 既存作品更新時のファイルシステム関連操作を実行します。
	/// 素材フォルダのRename、サムネイルの処理などがここに集約されます。
	/// </summary>
	/// <param name="editedSeries">編集内容を反映した編集用クローン。</param>
	/// <param name="materialSourceId">対象の素材ソースの SourceId。素材ソースがない場合は null。</param>
	private void ExecuteFileSystemUpdatesForUpdate(MangaSeries editedSeries, long? materialSourceId)
	{
		// === 素材フォルダ名変更の実行 ===
		// 必要に応じてRenameを実行し、MangaSource.Path を更新
		// Rename失敗時は例外をそのまま throw（MangaSeriesManager 経由で EditorPageViewModel の catch で処理）
		this.PerformMaterialFolderRenameIfNeeded(editedSeries, materialSourceId);

		// TODO: サムネイルRename処理をここに追加予定

		// TODO: クリーンアップ処理をここに追加予定
	}

	/// <summary>
	/// DB更新が失敗した場合の巻き戻し処理（クリーンアップ）を実行します。
	/// ファイルシステム上の変更を元に戻します。
	/// 現在の実装では実装されていません。
	/// </summary>
	/// <param name="editedSeries">編集内容を反映した編集用クローン。</param>
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
	/// <param name="editedSeries">編集後の編集用クローン。期待フォルダ名の生成と現在の素材フォルダ情報の取得に使用されます。</param>
	/// <param name="materialSourceId">対象の素材ソースの SourceId。素材ソースがない場合は null。</param>
	/// <returns>フォルダ名変更判定結果。Ok の場合のみ保存処理を続行してください。</returns>
	private MaterialFolderRenameCheckResult CheckMaterialFolderRenameStatus(MangaSeries editedSeries, long? materialSourceId)
	{
		// 素材フォルダが登録されていない場合は判定不要
		var originalMaterialSource = this.findSourceById(editedSeries, materialSourceId);
		if (originalMaterialSource == null)
		{
			// 素材フォルダが登録されていない場合は問題なし
			return MaterialFolderRenameCheckResult.Ok;
		}

		// 現在フォルダ名を取得
		var currentFolderPath = originalMaterialSource.Path;
		var currentFolderName = Path.GetFileName(currentFolderPath);

		// 現在の物理素材フォルダが作者付き形式かで期待フォルダ名を生成
		var includeAuthorPrefix = FileSystemNameHelper.TryExtractAuthorPrefix(currentFolderName, out _, out _);
		var expectedFolderName = MaterialFolderNameHelper.Create(editedSeries, includeAuthorPrefix);

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
	/// <param name="editedSeries">編集後の編集用クローン。期待フォルダ名の生成と、現在の素材フォルダ情報の取得・更新に使用されます。</param>
	/// <param name="materialSourceId">対象の素材ソースの SourceId。素材ソースがない場合は null。</param>
	private void PerformMaterialFolderRenameIfNeeded(MangaSeries editedSeries, long? materialSourceId)
	{
		// 素材フォルダ名変更の判定
		var folderRenameCheckResult = this.CheckMaterialFolderRenameStatus(editedSeries, materialSourceId);

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
		var materialSourceIndex = editedSeries.Sources.FindIndex(s => s.SourceId == materialSourceId);
		if (materialSourceIndex < 0)
		{
			// 通常ここには到達しない（CheckMaterialFolderRenameStatus() で確認済み）
			throw new InvalidOperationException("素材フォルダが見つかりません。");
		}

		var currentMaterialSource = editedSeries.Sources[materialSourceIndex];
		var currentFolderPath = currentMaterialSource.Path;
		var currentFolderName = Path.GetFileName(currentFolderPath);
		var parentDirectoryPath = Path.GetDirectoryName(currentFolderPath);
		if (string.IsNullOrEmpty(parentDirectoryPath))
		{
			throw new InvalidOperationException($"素材フォルダの親ディレクトリを取得できません。Path: {currentFolderPath}");
		}

		// 現在の物理素材フォルダが作者付き形式かで期待フォルダ名を生成
		var includeAuthorPrefix = FileSystemNameHelper.TryExtractAuthorPrefix(currentFolderName, out _, out _);
		var expectedFolderName = MaterialFolderNameHelper.Create(editedSeries, includeAuthorPrefix);
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
		editedSeries.Sources[materialSourceIndex] = newMaterialSource;

		this.logger?.LogInformation($"[PerformMaterialFolderRenameIfNeeded] MangaSource.Path更新。SeriesId: {editedSeries.SeriesId}, OldPath: {currentFolderPath}, NewPath: {newFolderPath}");
	}

	/// <summary>
	/// Rename 後（または新規登録・Path 更新された）Material フォルダ用 MangaSource を Store 内の正本インスタンスへ反映します。
	/// 対象の SourceId で編集用クローン内の MangaSource を特定し、Store 内の同じ SourceId の要素を置き換えます。
	/// 同じ作品に別の古い Material レコードが残っていても、それらは更新しません。
	/// </summary>
	/// <param name="editingSeries">新しい Path を保持する編集用クローン。</param>
	/// <param name="storeInstance">Store 内の正本インスタンス。</param>
	/// <param name="materialSourceId">反映対象の素材ソースの SourceId。素材ソースがない場合は null。</param>
	private void UpdateMaterialMangaSourceInStore(MangaSeries editingSeries, MangaSeries storeInstance, long? materialSourceId)
	{
		// Rename 後（または新規登録された）Material MangaSource を取得
		var renamedMaterialSource = this.findSourceById(editingSeries, materialSourceId);
		if (renamedMaterialSource == null)
		{
			// 反映対象の Material MangaSource が存在しない場合は更新不要
			return;
		}

		var newMangaSource = new MangaSource
		{
			SourceId = renamedMaterialSource.SourceId,
			SeriesId = renamedMaterialSource.SeriesId,
			Path = renamedMaterialSource.Path,
			Role = renamedMaterialSource.Role,
		};

		// Store 内の同じ SourceId の要素を置き換え、存在しなければ追加
		var index = storeInstance.Sources.FindIndex(s => s.SourceId == renamedMaterialSource.SourceId);
		if (index >= 0)
		{
			storeInstance.Sources[index] = newMangaSource;
			this.logger?.LogInformation(
				$"[UpdateMaterialMangaSourceInStore] Store 内の Material MangaSource を更新。SourceId: {renamedMaterialSource.SourceId}, NewPath: {renamedMaterialSource.Path}");
		}
		else
		{
			storeInstance.Sources.Add(newMangaSource);
			this.logger?.LogInformation(
				$"[UpdateMaterialMangaSourceInStore] Store 内に Material MangaSource を追加。SourceId: {renamedMaterialSource.SourceId}, Path: {renamedMaterialSource.Path}");
		}
	}

	/// <summary>
	/// 指定した SourceId の MangaSource を作品の Sources から取得します。
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <param name="sourceId">取得する MangaSource の SourceId。null の場合は取得しません。</param>
	/// <returns>一致した MangaSource。sourceId が null または見つからない場合は null。</returns>
	private MangaSource? findSourceById(MangaSeries series, long? sourceId)
	{
		return sourceId is null
			? null
			: series.Sources.FirstOrDefault(s => s.SourceId == sourceId.Value);
	}
}
