using HalationGhost.Utilities;
using MangaBinder.Helpers;
using MangaBinder.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Bindings;

/// <summary>
/// 作品編集（Editor）の編集開始・編集中の操作・検証・保存フローを担当する Scoped の Manager です。
/// 状態は <see cref="EditorStore"/> が保持し、この Manager は状態を持ちません。
/// UI（Dialog / Snackbar / Loading / 画面遷移）の責務は持たず、画面が結果を見て表示・遷移を行います。
/// 編集中の変更は <see cref="EditorStore.EditTarget"/> と <see cref="EditorStore"/> の編集状態にだけ反映し、
/// 保存成功まで <see cref="EditorStore.OriginalSeries"/>（正本）は変更しません。
/// </summary>
public sealed class MangaSeriesManager
{
	/// <summary>別ドライブ移動の確認を行う、追加素材の合計サイズ（1GiB）。</summary>
	private const long differentDriveConfirmationThresholdBytes = 1024L * 1024L * 1024L;

	/// <summary>編集セッション状態を保持するストア。</summary>
	private readonly EditorStore editorStore;

	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>製本待ちを操作する Manager。</summary>
	private readonly BindingQueueManager bindingQueueManager;

	/// <summary>作品の正本を保持するストア。</summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>タグマスタの共有 Notifier を所有するストア。</summary>
	private readonly TagStore tagStore;

	/// <summary>重複作品検索を提供する Manager。</summary>
	private readonly MangaBinderManager mangaBinderManager;

	/// <summary>素材の解析を担う Manager。</summary>
	private readonly MaterialManager materialManager;

	/// <summary>所持最大巻数の推定を行うクラス。</summary>
	private readonly OwnedVolumeEstimator ownedVolumeEstimator;

	/// <summary>保存処理（Keyed）を解決するための、同一 Scope の ServiceProvider。</summary>
	private readonly IServiceProvider serviceProvider;

	/// <summary>ログ出力用の Logger。</summary>
	private readonly ILogger<MangaSeriesManager> logger;

	/// <summary>
	/// <see cref="MangaSeriesManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="editorStore">編集セッション状態を保持するストア。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="bindingQueueManager">製本待ちを操作する Manager。</param>
	/// <param name="mangaSeriesStore">作品の正本を保持するストア。</param>
	/// <param name="tagStore">タグマスタの共有 Notifier を所有するストア。</param>
	/// <param name="mangaBinderManager">重複作品検索を提供する Manager。</param>
	/// <param name="materialManager">素材の解析を担う Manager。</param>
	/// <param name="ownedVolumeEstimator">所持最大巻数の推定を行うクラス。</param>
	/// <param name="serviceProvider">保存処理（Keyed）を解決するための、同一 Scope の ServiceProvider。</param>
	/// <param name="logger">ログ出力用の Logger。</param>
	public MangaSeriesManager(
		EditorStore editorStore,
		AppSettings appSettings,
		BindingQueueManager bindingQueueManager,
		MangaSeriesStore mangaSeriesStore,
		TagStore tagStore,
		MangaBinderManager mangaBinderManager,
		MaterialManager materialManager,
		OwnedVolumeEstimator ownedVolumeEstimator,
		IServiceProvider serviceProvider,
		ILogger<MangaSeriesManager> logger)
	{
		this.editorStore = editorStore;
		this.appSettings = appSettings;
		this.bindingQueueManager = bindingQueueManager;
		this.mangaSeriesStore = mangaSeriesStore;
		this.tagStore = tagStore;
		this.mangaBinderManager = mangaBinderManager;
		this.materialManager = materialManager;
		this.ownedVolumeEstimator = ownedVolumeEstimator;
		this.serviceProvider = serviceProvider;
		this.logger = logger;
	}

	/// <summary>
	/// 編集対象の作品を受け取り、<see cref="EditorStore"/> の編集用データをすべて準備します。
	/// 実在する素材フォルダが複数ある場合は編集準備を中止し、<see cref="EditorStore"/> を初期化しません。
	/// 準備できた場合は、元作品を <see cref="EditorStore.OriginalSeries"/> に保持し、DeepCopy した編集用クローンを
	/// <see cref="EditorStore.EditTarget"/> に設定します。元作品は変更しません。
	/// 素材フォルダのサイズ計算は非同期で起動するだけで、完了を待たずに <see cref="EditPrepareResult.Ready"/> を返します。
	/// 画面遷移は行わないため、<see cref="EditPrepareResult.Ready"/> の場合に呼び出し元が Editor へ遷移してください。
	/// </summary>
	/// <param name="series">編集対象の元作品。</param>
	/// <returns>編集準備の結果。</returns>
	public ValueTask<EditPrepareResult> PrepareEditAsync(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		// 実在する素材フォルダが複数ある作品は編集できない。Store は初期化しない
		if (MaterialSourceAvailabilityHelper.HasMultipleExistingMaterialFolders(series))
		{
			this.logger.LogWarning(
				"実在する素材フォルダが複数あるため、編集を開始できません。SeriesId={SeriesId}, WorkId={WorkId}",
				series.SeriesId,
				series.WorkId);
			return ValueTask.FromResult(EditPrepareResult.MultipleMaterialFolders);
		}

		// 編集対象の素材ソースを特定する（実在する 1 件。実在 0 件の場合は null で、古い DB レコードは物理フォルダとして扱わない）
		var materialSource = MaterialSourceAvailabilityHelper.ResolveEditingMaterialSource(series);

		var clone = this.copySeries(series);

		// 素材一覧（既存作品のみ）を取得して初期ソートする
		var materialFiles = this.getMaterialFiles(series, materialSource);
		materialFiles.Sort(MaterialFileItemComparer.Default);

		// 登録先素材フォルダの候補と初期選択を決定する
		var materialSourceFolders = this.appSettings.SourceFolders
			.Where(f => f.Role.Value == FolderRole.Material)
			.ToList();
		var (selectedFolder, canSelectFolder) = this.resolveMaterialSourceFolder(series, materialSource, materialSourceFolders);

		// 製本待ち状態は正式登録済み作品のみ Queue の状態を反映する
		var isBindingQueued = series.SeriesId != 0 && !series.IsWork
			&& this.bindingQueueManager.Contains(series.SeriesId);

		// 編集用クローン専用の Notifier を生成する。タグマスタの TagNotifier は TagStore の共有物を使用する
		var notifier = new MangaSeriesNotifier(clone, this.tagStore);

		// サイズが確定しているファイルの合計だけを初期値にする。フォルダのサイズは非同期で計算して加算する
		var knownTotalSizeBytes = materialFiles.Sum(m => m.SizeBytes ?? 0L);

		this.editorStore.Initialize(
			series,
			clone,
			notifier,
			materialFiles,
			materialSourceFolders,
			selectedFolder,
			canSelectFolder,
			isBindingQueued,
			knownTotalSizeBytes,
			materialSource);

		this.startMaterialSizeCalculation(clone, materialFiles);

		this.logger.LogInformation(
			"編集準備が完了しました。SeriesId={SeriesId}, WorkId={WorkId}, MaterialFileCount={MaterialFileCount}",
			series.SeriesId,
			series.WorkId,
			materialFiles.Count);

		return ValueTask.FromResult(EditPrepareResult.Ready);
	}

	/// <summary>
	/// 編集用クローン（<see cref="EditorStore.EditTarget"/>）へ、画面入力以外で決まる値を反映します。
	/// 通常の編集項目と巻情報は、画面入力時に編集用クローンへ直接反映済みです。
	/// Worker（MaterialFolderScanner / GoogleBooksImporter）と同等の品質を目指します。
	/// 既存作品（SeriesId != 0 かつ IsWork == false）の場合は、共通処理のみを実施します。
	/// </summary>
	private void applyDerivedValues()
	{
		var target = this.getEditTarget();

		// === 【共通処理】タイトルから派生する値 ===
		this.applyTitle(target, target.Title);

		// === 【共通処理】Description 出典 ===
		// 既存正式作品ではここでは変更しない。新規・登録待ち作品のみ、入力があれば Manual、なければ None
		if (target.SeriesId == 0 || target.IsWork)
		{
			target.DescriptionSource = string.IsNullOrEmpty(target.Description)
				? DescriptionSource.None
				: DescriptionSource.Manual;
			target.DescriptionSourceTitle = string.Empty;
		}

		// === 【共通処理】GoogleBooks 関連 ===
		// 作者・出版社・あらすじのいずれかが未入力の場合のみ NotImported 状態に設定する。
		// 3 項目すべて入力済みの場合は、現在の状態（Success, NotFound, Failed など）を維持する
		var hasMissingGoogleBooksField =
			string.IsNullOrWhiteSpace(target.Author) ||
			string.IsNullOrWhiteSpace(target.Publisher) ||
			string.IsNullOrWhiteSpace(target.Description);

		if (hasMissingGoogleBooksField)
		{
			target.GoogleBooksImportStatus = GoogleBooksImportStatus.NotImported;
			target.GoogleBooksImportedAt = string.Empty;
			target.GoogleBooksImportMessage = string.Empty;
		}

		// 既存正式作品はここで終了
		if (target.SeriesId != 0 && !target.IsWork)
		{
			return;
		}

		// === 【登録待ち・新規のみ】Worker で生成される MangaSeries と同等の初期値 ===
		target.IsSourceMissing = false;
		target.HasNestedArchive = false;
		target.BoundEndVolume = 0;
		target.NormalizedTitleExternal = string.Empty;
	}

	/// <summary>
	/// 編集用クローンへ派生値を反映したうえで、正式登録の前提条件を検証します。
	/// 検証はユーザーが登録ボタンを押したときにだけ実行します。
	/// </summary>
	/// <returns>検出したエラーの種類。問題がない場合は <see cref="EditorValidationError.None"/>。</returns>
	public EditorValidationError ValidateForRegister()
	{
		this.applyDerivedValues();

		var target = this.getEditTarget();
		var isExistingSeries = target.SeriesId != 0 && !target.IsWork;

		if (string.IsNullOrWhiteSpace(target.Title))
		{
			return EditorValidationError.TitleRequired;
		}

		if (isExistingSeries)
		{
			// 既存正式作品は、同一作品と判定できる範囲の表記修正だけ許可する
			if (!this.isTitleMatchedWithOriginal(target))
			{
				return EditorValidationError.TitleMismatch;
			}

			// 追加素材がある場合のみ登録先フォルダが必須
			return this.editorStore.MaterialFiles.Count > 0 && this.editorStore.SelectedMaterialSourceFolder.Value == null
				? EditorValidationError.DestinationRequired
				: EditorValidationError.None;
		}

		// 新規・登録待ち作品：同一タイトルの正式作品が存在する場合は作者が必須
		if (string.IsNullOrWhiteSpace(target.Author))
		{
			var hasSameTitleSeries = this.mangaBinderManager.FindSameTitle(target.Title)
				.Any(series => series.SeriesId != 0 && !series.IsWork);
			if (hasSameTitleSeries)
			{
				return EditorValidationError.AuthorRequired;
			}
		}

		if (this.mangaBinderManager.FindDuplicateSeries(target).Count > 0)
		{
			return EditorValidationError.DuplicateSeries;
		}

		if (this.editorStore.MaterialFiles.Count == 0)
		{
			return EditorValidationError.MaterialRequired;
		}

		return this.editorStore.SelectedMaterialSourceFolder.Value == null
			? EditorValidationError.DestinationRequired
			: EditorValidationError.None;
	}

	/// <summary>
	/// 編集用クローンへ派生値を反映したうえで、一時保存の前提条件を検証します。
	/// </summary>
	/// <returns>検出したエラーの種類。問題がない場合は <see cref="EditorValidationError.None"/>。</returns>
	public EditorValidationError ValidateForWorkSave()
	{
		this.applyDerivedValues();

		return string.IsNullOrWhiteSpace(this.getEditTarget().Title)
			? EditorValidationError.TitleRequired
			: EditorValidationError.None;
	}

	/// <summary>
	/// 編集用クローンのタイトルを、編集開始時の元のタイトルへ戻し、画面へ再通知します。
	/// 正本（<see cref="EditorStore.OriginalSeries"/>）は変更しません。
	/// </summary>
	public void RestoreOriginalTitle()
	{
		this.applyTitle(this.getEditTarget(), this.getOriginalSeries().Title);
		this.editorStore.EditTarget.ForceNotify();
	}

	/// <summary>
	/// ドラッグアンドドロップまたはダイアログで指定されたファイル・フォルダを解析し、素材として追加します。
	/// 重複判定は以下の順で行います。
	/// 1. 同一 FullPath が存在する場合は追加せず「既に追加済み」として記録します。
	/// 2. FullPath は異なるが同名の素材が存在する場合は追加せず「同名ファイル」として記録します。
	/// 3. 上記以外は追加します。
	/// 追加後は並び替えと所持最大巻数の推定を行い、サイズ未確定の素材は非同期でサイズ計算を起動します。
	/// </summary>
	/// <param name="droppedPaths">追加するファイル・フォルダのパス。</param>
	/// <returns>素材追加の結果。</returns>
	public MaterialAddResult AddMaterialFiles(IReadOnlyCollection<string> droppedPaths)
	{
		ArgumentNullException.ThrowIfNull(droppedPaths);

		var existingFullPaths = this.editorStore.MaterialFiles.Select(m => m.FullPath).ToHashSet();
		var existingFileNames = this.editorStore.MaterialFiles.Select(m => m.Name).ToHashSet();

		var candidates = this.materialManager.AnalyzePaths(droppedPaths, existingFullPaths.ToList()).ToList();

		var alreadyAddedFiles = new List<string>();
		var sameNameFiles = new List<string>();
		var addedItems = new List<MaterialFileItem>();

		foreach (var candidate in candidates)
		{
			if (existingFullPaths.Contains(candidate.FullPath))
			{
				alreadyAddedFiles.Add(candidate.FileName);
				continue;
			}

			if (existingFileNames.Contains(candidate.FileName))
			{
				sameNameFiles.Add(candidate.FileName);
				continue;
			}

			addedItems.Add(new MaterialFileItem
			{
				Name = candidate.FileName,
				FullPath = candidate.FullPath,
				ItemType = candidate.Type,
				SizeBytes = candidate.Size,
				CanRemove = true,
			});
		}

		var ownedMaxVolumeUpdated = false;
		if (addedItems.Count > 0)
		{
			this.editorStore.MaterialFiles.AddRange(addedItems);
			this.editorStore.MaterialFiles.Sort(MaterialFileItemComparer.Default);

			// サイズが確定している素材は即時に合計へ加算し、フォルダのサイズは計算完了後に加算する
			this.editorStore.MaterialTotalSizeBytes.Value += addedItems.Sum(m => m.SizeBytes ?? 0L);

			ownedMaxVolumeUpdated = this.recalculateOwnedVolumeEstimate();
			this.startMaterialSizeCalculation(this.getEditTarget(), addedItems);
		}

		return new MaterialAddResult(addedItems.Count > 0, ownedMaxVolumeUpdated, alreadyAddedFiles, sameNameFiles);
	}

	/// <summary>
	/// 素材一覧から指定された素材を削除します。
	/// 削除できるのは今回の編集で追加した素材（CanRemove == true）のうち、サイズが確定している素材だけです。
	/// 削除した素材の保持済みサイズを合計から減算し、所持最大巻数の推定を行います。素材が 0 件になっても編集開始時の値へは戻しません。
	/// </summary>
	/// <param name="fullPath">削除対象の素材のフルパス。</param>
	/// <returns>素材削除の結果。</returns>
	public MaterialRemoveResult RemoveMaterialFile(string fullPath)
	{
		ArgumentException.ThrowIfNullOrEmpty(fullPath);

		var item = this.editorStore.MaterialFiles.FirstOrDefault(m => m.FullPath == fullPath);
		if (item is null || !item.CanRemove || item.IsSizePending)
		{
			return new MaterialRemoveResult(false, false);
		}

		this.editorStore.MaterialFiles.Remove(item);
		this.editorStore.MaterialTotalSizeBytes.Value -= item.SizeBytes ?? 0L;

		return new MaterialRemoveResult(true, this.recalculateOwnedVolumeEstimate());
	}

	/// <summary>
	/// 別ドライブ移動の確認が必要かどうかを判定します。
	/// 今回追加した素材（CanRemove == true）の合計サイズが 1GiB 以上で、
	/// 追加素材の中に登録先素材フォルダとは異なるドライブの素材がある場合に true を返します。
	/// 素材サイズの計算中は登録できないため、サイズはすべて確定済みです。
	/// </summary>
	/// <returns>別ドライブ移動の確認が必要な場合は true。</returns>
	public bool RequiresDifferentDriveConfirmation()
	{
		var selectedFolder = this.editorStore.SelectedMaterialSourceFolder.Value;
		if (selectedFolder is null)
		{
			return false;
		}

		var addedMaterials = this.editorStore.MaterialFiles.Where(m => m.CanRemove).ToList();
		if (addedMaterials.Count == 0)
		{
			return false;
		}

		var totalSize = addedMaterials.Sum(m => m.SizeBytes ?? 0L);
		if (totalSize < differentDriveConfirmationThresholdBytes)
		{
			return false;
		}

		var selectedDriveLetter = Path.GetPathRoot(selectedFolder.FolderPath.Value)?[0];
		if (selectedDriveLetter is null)
		{
			return false;
		}

		return addedMaterials.Any(m =>
		{
			var materialDriveLetter = Path.GetPathRoot(m.FullPath)?[0];
			return materialDriveLetter is not null && materialDriveLetter != selectedDriveLetter;
		});
	}

	/// <summary>
	/// 編集中の作品を正式な <see cref="MangaSeries"/> として保存します。
	/// 新規作品・登録待ち作品は <see cref="SeriesSaveType.New"/>、既存正式作品は <see cref="SeriesSaveType.Existing"/> の保存処理を呼び分けます。
	/// 保存成功後は、製本待ち状態を反映します。編集状態のクリアと画面遷移は呼び出し元が行います。
	/// </summary>
	/// <remarks>
	/// 既存正式作品の保存処理には、編集用クローンを作業対象として、正本（<see cref="EditorStore.OriginalSeries"/>）を比較用・参照用に渡します。
	/// 保存処理は DB Commit 後に、正本（<see cref="MangaSeriesStore"/> の共有インスタンス）へ編集内容を反映します。
	/// サムネイルは <see cref="EditorStore.ThumbnailJpegBytes"/> を使用します。
	/// </remarks>
	/// <returns>保存処理の結果（作品情報と移動失敗素材を含む）。</returns>
	public async ValueTask<SeriesSaveResult> RegisterSeriesAsync()
	{
		var target = this.getEditTarget();
		var thumbnailBytes = this.editorStore.ThumbnailJpegBytes;
		var materialFiles = this.editorStore.MaterialFiles
			.Select(item => new MaterialFile
			{
				FullPath = item.FullPath,
				Type = item.ItemType,
				CanRemove = item.CanRemove,
			})
			.ToList();
		var selectedFolder = this.editorStore.SelectedMaterialSourceFolder.Value;

		SeriesSaveResult saveResult;

		if (target.SeriesId != 0 && !target.IsWork)
		{
			if (this.mangaBinderManager.FindDuplicateSeries(target).Count > 0)
			{
				throw new InvalidOperationException("タイトル重複エラー：同じタイトルと作者の別作品が既に存在します。");
			}

			var saveManager = this.serviceProvider.GetRequiredKeyedService<ISeriesSaveManager>(SeriesSaveType.Existing);
			saveResult = await saveManager.SaveAsync(target, this.getOriginalSeries(), materialFiles, selectedFolder, thumbnailBytes);
		}
		else
		{
			var saveManager = this.serviceProvider.GetRequiredKeyedService<ISeriesSaveManager>(SeriesSaveType.New);
			saveResult = await saveManager.SaveAsync(target, null, materialFiles, selectedFolder, thumbnailBytes);
		}

		if (saveResult.Series is not null)
		{
			this.updateBindingQueue(saveResult.Series);
		}

		return saveResult;
	}

	/// <summary>
	/// 編集中の作品を登録待ち作品（WorkMangaSeries）として一時保存します。
	/// 新規の一時保存では保存対象のインスタンスが Store の正本になるため、編集用クローンから DeepCopy した作業用コピーを渡します。
	/// サムネイルは <see cref="EditorStore.ThumbnailJpegBytes"/> を使用します。
	/// </summary>
	/// <returns>一時保存後の WorkId。</returns>
	public async ValueTask<int> SaveWorkSeriesAsync()
	{
		var workingCopy = this.copySeries(this.getEditTarget());
		var saveManager = this.serviceProvider.GetRequiredKeyedService<ISeriesSaveManager>(SeriesSaveType.Work);
		var saveResult = await saveManager.SaveAsync(workingCopy, null, [], null, this.editorStore.ThumbnailJpegBytes);

		var savedSeries = saveResult.Series
			?? throw new InvalidOperationException("保存結果が不正です：Series が null です。");

		return savedSeries.WorkId;
	}

	/// <summary>
	/// 編集用クローンを取得します。
	/// </summary>
	/// <returns>編集用クローン。</returns>
	/// <exception cref="InvalidOperationException">編集セッションが開始されていない場合。</exception>
	private MangaSeries getEditTarget()
		=> this.editorStore.EditTarget.Value
			?? throw new InvalidOperationException("EditorStore が初期化されていません。PrepareEditAsync を完了してください。");

	/// <summary>
	/// 編集開始前の元作品（正本）を取得します。読み取り専用として扱い、変更してはいけません。
	/// </summary>
	/// <returns>元作品。</returns>
	/// <exception cref="InvalidOperationException">編集セッションが開始されていない場合。</exception>
	private MangaSeries getOriginalSeries()
		=> this.editorStore.OriginalSeries
			?? throw new InvalidOperationException("EditorStore が初期化されていません。PrepareEditAsync を完了してください。");

	/// <summary>
	/// 作品を DeepCopy します。Tags は JSON でコピーされないため、共有タグインスタンスを別途引き継ぎます。
	/// </summary>
	/// <param name="series">コピー元の作品。</param>
	/// <returns>DeepCopy した作品。</returns>
	private MangaSeries copySeries(MangaSeries series)
	{
		var copy = DeepCopyHelper.Copy(series);
		foreach (var tag in series.Tags)
		{
			copy.Tags.Add(tag);
		}

		return copy;
	}

	/// <summary>
	/// タイトルと、タイトルから派生する値（正規化タイトル・短縮タイトル）を反映します。
	/// </summary>
	/// <param name="series">反映先の作品。</param>
	/// <param name="title">タイトル。</param>
	private void applyTitle(MangaSeries series, string title)
	{
		series.Title = title;
		series.NormalizedTitleInternal = MangaTitleHelper.NormalizeTitleInternal(title);
		series.ShortTitle = MangaTitleHelper.GetShortTitle(title, string.Empty);
	}

	/// <summary>
	/// 編集後のタイトルが、元のタイトルと同一作品と判定できる範囲（正規化タイトルが一致）かどうかを判定します。
	/// </summary>
	/// <param name="target">編集用クローン。</param>
	/// <returns>正規化タイトルが一致する場合は true。</returns>
	private bool isTitleMatchedWithOriginal(MangaSeries target)
	{
		var normalizedOriginal = MangaTitleHelper.NormalizeTitleInternal(this.getOriginalSeries().Title);
		var normalizedEdited = MangaTitleHelper.NormalizeTitleInternal(target.Title);

		return normalizedOriginal == normalizedEdited;
	}

	/// <summary>
	/// 現在の素材一覧から所持最大巻数を推定し、推定できた場合のみ <see cref="EditorStore"/> 経由で値を更新します。
	/// 推定できない場合は現在の値を変更しません。自動推定のため、手動変更フラグは変更しません。
	/// </summary>
	/// <returns>所持最大巻数を更新した場合は true。</returns>
	private bool recalculateOwnedVolumeEstimate()
	{
		var materials = this.editorStore.MaterialFiles
			.Select(m => (m.Name, this.getVolumeNumberSourceType(m.ItemType)))
			.ToList();

		var result = this.ownedVolumeEstimator.Estimate(materials);
		return this.editorStore.SetEstimatedOwnedMaxVolume(result.OwnedMaxVolume);
	}

	/// <summary>
	/// <see cref="MaterialItemType"/> から <see cref="VolumeNumberSourceType"/> に変換します。
	/// </summary>
	/// <param name="itemType">素材アイテムの種別。</param>
	/// <returns>対応する <see cref="VolumeNumberSourceType"/>。Root などはアーカイブとして扱います。</returns>
	private VolumeNumberSourceType getVolumeNumberSourceType(MaterialItemType itemType)
		=> itemType switch
		{
			MaterialItemType.Folder => VolumeNumberSourceType.Folder,
			MaterialItemType.Epub => VolumeNumberSourceType.Epub,
			_ => VolumeNumberSourceType.Archive,
		};

	/// <summary>
	/// サイズが未確定の素材について、サイズの非同期計算を起動します。完了は待ちません。
	/// 非同期処理には、計算に必要な値（素材のパス・編集対象・Store・Logger）だけを渡し、Scoped の Manager 自体は保持させません。
	/// WPF のスレッドから呼び出した場合、計算結果の Store への反映は呼び出し元の同期コンテキスト上で行われます。
	/// </summary>
	/// <param name="editTarget">計算を開始した時点の編集対象。計算完了時に現在の編集対象と参照比較します。</param>
	/// <param name="materialFiles">計算対象を含む素材一覧。</param>
	private void startMaterialSizeCalculation(MangaSeries editTarget, IEnumerable<MaterialFileItem> materialFiles)
	{
		var pendingPaths = materialFiles
			.Where(m => m.IsSizePending)
			.Select(m => m.FullPath)
			.ToList();

		if (pendingPaths.Count == 0)
		{
			return;
		}

		_ = calculateMaterialSizesAsync(this.editorStore, editTarget, pendingPaths, this.logger);
	}

	/// <summary>
	/// 指定された素材のサイズを並行して計算し、完了した順に Store へ反映します。
	/// Scoped の Manager に依存しないよう static としています。
	/// </summary>
	/// <param name="editorStore">計算結果を反映するストア。</param>
	/// <param name="editTarget">計算を開始した時点の編集対象。</param>
	/// <param name="fullPaths">サイズを計算する素材のフルパス。</param>
	/// <param name="logger">ログ出力用の Logger。</param>
	private static Task calculateMaterialSizesAsync(EditorStore editorStore, MangaSeries editTarget, IReadOnlyList<string> fullPaths, ILogger logger)
		=> Task.WhenAll(fullPaths.Select(fullPath => calculateMaterialSizeAsync(editorStore, editTarget, fullPath, logger)));

	/// <summary>
	/// 1 つの素材のサイズを計算し、Store へ反映します。
	/// 計算に失敗した場合は、サイズ計算中の状態が残り続けないよう、サイズ 0 として確定します。
	/// </summary>
	/// <param name="editorStore">計算結果を反映するストア。</param>
	/// <param name="editTarget">計算を開始した時点の編集対象。</param>
	/// <param name="fullPath">サイズを計算する素材のフルパス。</param>
	/// <param name="logger">ログ出力用の Logger。</param>
	private static async Task calculateMaterialSizeAsync(EditorStore editorStore, MangaSeries editTarget, string fullPath, ILogger logger)
	{
		var sizeBytes = 0L;

		try
		{
			sizeBytes = await Task.Run(() => StorageSizeHelper.GetPathSize(fullPath));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "素材サイズの計算に失敗しました。サイズ 0 として扱います。Path={Path}", fullPath);
		}

		// await 後は呼び出し元の同期コンテキストへ戻る。編集対象が変わっている場合は Store 側で破棄する
		editorStore.ApplyCalculatedMaterialSize(editTarget, fullPath, sizeBytes);
	}

	/// <summary>
	/// 保存後の製本待ち状態を反映します。
	/// ON の場合は正本の共有 Notifier を Queue へ追加し、OFF の場合は Queue から削除します。
	/// </summary>
	/// <param name="savedSeries">保存済みの正式作品。</param>
	private void updateBindingQueue(MangaSeries savedSeries)
	{
		if (this.editorStore.IsBindingQueued.Value)
		{
			var notifier = this.mangaSeriesStore.FindNotifierById(savedSeries.SeriesId)
				?? throw new InvalidOperationException($"SeriesId {savedSeries.SeriesId} に対応する MangaSeriesNotifier が見つかりません。");
			this.bindingQueueManager.Add(notifier);
		}
		else
		{
			this.bindingQueueManager.Remove(savedSeries.SeriesId);
		}
	}

	/// <summary>
	/// 指定された素材フォルダ直下のファイル・フォルダを取得します。
	/// 新規作品・登録待ち作品（IsWork == true）や、素材フォルダが特定できない・存在しない場合は空リストを返します。
	/// ファイルのサイズは取得済みの値を使用し、フォルダのサイズは未確定（null）のままにします。
	/// </summary>
	/// <param name="series">対象となる作品。</param>
	/// <param name="materialSource">編集対象として特定した素材ソース。</param>
	/// <returns>素材フォルダ直下のファイル・フォルダを表す <see cref="MaterialFileItem"/> のリスト。</returns>
	private List<MaterialFileItem> getMaterialFiles(MangaSeries series, MangaSource? materialSource)
	{
		if (series.IsWork || materialSource is null)
			return [];

		if (!Directory.Exists(materialSource.Path))
		{
			this.logger.LogInformation(
				"素材フォルダが見つかりません。SeriesId={SeriesId}, Path={Path}",
				series.SeriesId,
				materialSource.Path);
			return [];
		}

		var result = new List<MaterialFileItem>();

		try
		{
			var entries = Directory.GetFileSystemEntries(materialSource.Path, "*", SearchOption.TopDirectoryOnly);

			foreach (var entry in entries)
			{
				var isDirectory = (File.GetAttributes(entry) & FileAttributes.Directory) != 0;

				var itemType = isDirectory
					? MaterialItemType.Folder
					: this.getItemTypeFromExtension(Path.GetExtension(entry));

				long? sizeBytes = null;
				if (!isDirectory)
				{
					try
					{
						sizeBytes = new FileInfo(entry).Length;
					}
					catch
					{
						// ファイル情報取得失敗時は null のまま（サイズ未確定として非同期で計算する）
					}
				}

				result.Add(new MaterialFileItem
				{
					Name = Path.GetFileName(entry),
					FullPath = entry,
					ItemType = itemType,
					SizeBytes = sizeBytes,
					CanRemove = false,
				});
			}
		}
		catch (Exception ex)
		{
			this.logger.LogWarning(
				ex,
				"素材フォルダの列挙中にエラーが発生しました。SeriesId={SeriesId}, Path={Path}",
				series.SeriesId,
				materialSource.Path);
		}

		return result;
	}

	/// <summary>
	/// ファイル拡張子から <see cref="MaterialItemType"/> を判定します。
	/// </summary>
	/// <param name="extension">ファイル拡張子（例：".zip", ".jpg"）。</param>
	/// <returns>判定された <see cref="MaterialItemType"/>。</returns>
	private MaterialItemType getItemTypeFromExtension(string extension)
	{
		if (string.IsNullOrWhiteSpace(extension))
			return MaterialItemType.Root;

		if (SupportedExtensionHelper.IsArchive(extension))
			return MaterialItemType.Archive;

		if (extension.Equals(".epub", StringComparison.OrdinalIgnoreCase))
			return MaterialItemType.Epub;

		// 画像ファイルはアイコン表示用に Folder として扱う
		if (SupportedExtensionHelper.IsImage(extension))
			return MaterialItemType.Folder;

		return MaterialItemType.Root;
	}

	/// <summary>
	/// 登録先素材フォルダの初期選択と、変更可否を決定します。
	/// 新規作品・登録待ち作品は先頭の候補を初期選択して変更可能にします。
	/// 実在する素材ソースを特定できた既存作品は、そのパスを含む登録済み素材フォルダを選択して変更不可にします。
	/// 実在する素材フォルダを持たない既存作品は、未選択で変更可能にします。
	/// </summary>
	/// <param name="series">編集対象の元作品。</param>
	/// <param name="materialSource">編集対象として特定した、実在する素材ソース。</param>
	/// <param name="materialSourceFolders">登録先素材フォルダの候補。</param>
	/// <returns>初期選択の素材フォルダ（未選択の場合は null）と、変更可否。</returns>
	private (SourceFolder? Selected, bool CanSelect) resolveMaterialSourceFolder(
		MangaSeries series,
		MangaSource? materialSource,
		IReadOnlyList<SourceFolder> materialSourceFolders)
	{
		if (series.SeriesId == 0)
			return (materialSourceFolders.FirstOrDefault(), true);

		if (materialSource is not null)
			return (this.findMatchingSourceFolder(materialSource.Path, materialSourceFolders), false);

		return (null, true);
	}

	/// <summary>
	/// 素材ソースのパスに対応する SourceFolder を検索します。
	/// 複数の SourceFolder が一致する場合は FolderPath が最も長いものを優先します。
	/// </summary>
	/// <param name="materialSourcePath">素材ソースのパス。</param>
	/// <param name="materialSourceFolders">登録先素材フォルダの候補。</param>
	/// <returns>一致した SourceFolder、または見つからない場合は null。</returns>
	private SourceFolder? findMatchingSourceFolder(string materialSourcePath, IReadOnlyList<SourceFolder> materialSourceFolders)
	{
		var targetMaterialPath = Path.GetFullPath(materialSourcePath);

		return materialSourceFolders
			.Select(folder => (Folder: folder, FullPath: Path.GetFullPath(folder.FolderPath.Value)))
			.Where(x => this.isPathUnderFolder(targetMaterialPath, x.FullPath))
			.OrderByDescending(x => x.FullPath.Length)
			.Select(x => x.Folder)
			.FirstOrDefault();
	}

	/// <summary>
	/// targetPath が folderPath の配下にあるかを判定します。
	/// </summary>
	/// <param name="targetPath">判定対象のパス。</param>
	/// <param name="folderPath">親フォルダのパス。</param>
	/// <returns>targetPath が folderPath の配下にある場合は true。</returns>
	private bool isPathUnderFolder(string targetPath, string folderPath)
	{
		var targetFull = Path.GetFullPath(targetPath);
		var folderFull = Path.GetFullPath(folderPath);

		if (!folderFull.EndsWith(Path.DirectorySeparatorChar))
		{
			folderFull += Path.DirectorySeparatorChar;
		}

		return targetFull.StartsWith(folderFull, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(targetFull, folderFull.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
	}
}
