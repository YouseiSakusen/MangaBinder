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
	/// 画面遷移は行わないため、<see cref="EditPrepareResult.Ready"/> の場合に呼び出し元が Editor へ遷移してください。
	/// </summary>
	/// <param name="series">編集対象の元作品。</param>
	/// <returns>編集準備の結果。</returns>
	public async ValueTask<EditPrepareResult> PrepareEditAsync(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		// 実在する素材フォルダが複数ある作品は編集できない。Store は初期化しない
		if (MaterialSourceAvailabilityHelper.HasMultipleExistingMaterialFolders(series))
		{
			this.logger.LogWarning(
				"実在する素材フォルダが複数あるため、編集を開始できません。SeriesId={SeriesId}, WorkId={WorkId}",
				series.SeriesId,
				series.WorkId);
			return EditPrepareResult.MultipleMaterialFolders;
		}

		// 編集対象の素材ソース（実在する 1 件、または実在 0 件の場合は登録 1 件）を特定する
		var materialSource = MaterialSourceAvailabilityHelper.ResolveEditingMaterialSource(series);

		var clone = this.copySeries(series);

		// 素材一覧（既存作品のみ）を取得して初期ソートする
		var materialFiles = this.sortMaterialFiles(this.getMaterialFiles(series, materialSource));

		// 素材合計サイズを計算する
		var materialTotalSizeBytes = materialFiles.Count == 0
			? 0L
			: await StorageSizeHelper.GetTotalAllAsync(materialFiles.Select(m => m.FullPath).ToList());

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

		this.editorStore.Initialize(
			series,
			clone,
			notifier,
			materialFiles,
			materialSourceFolders,
			selectedFolder,
			canSelectFolder,
			isBindingQueued,
			materialTotalSizeBytes,
			materialSource);

		this.logger.LogInformation(
			"編集準備が完了しました。SeriesId={SeriesId}, WorkId={WorkId}, MaterialFileCount={MaterialFileCount}",
			series.SeriesId,
			series.WorkId,
			materialFiles.Count);

		return EditPrepareResult.Ready;
	}

	/// <summary>
	/// 画面で入力された値を編集用クローン（<see cref="EditorStore.EditTarget"/>）へ反映します。
	/// Worker（MaterialFolderScanner / GoogleBooksImporter）と同等の品質を目指します。
	/// 既存作品（SeriesId != 0 かつ IsWork == false）の場合は、共通処理のみを実施します。
	/// 所持最大巻数が編集用クローンの現在値と異なる場合は、ユーザーが手動変更したものとして
	/// <see cref="MangaSeries.IsOwnedMaxVolumeManuallyEdited"/> を true にします（自動推定は即時に編集用クローンへ反映済みのため手動変更になりません）。
	/// </summary>
	/// <param name="input">画面で入力された値。</param>
	public void ApplyInput(EditorInputValues input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var target = this.getEditTarget();
		var titleInput = input.Title ?? string.Empty;

		// === 所持最大巻数の手動変更判定（自動推定は EditTarget へ反映済みのため、差分は手動変更） ===
		var inputOwnedMaxVolume = input.OwnedMaxVolume.HasValue ? (int)input.OwnedMaxVolume.Value : 0;
		if (inputOwnedMaxVolume != target.OwnedMaxVolume)
		{
			target.IsOwnedMaxVolumeManuallyEdited = true;
		}

		// === 【共通処理】基本情報 ===
		this.applyTitle(target, titleInput);
		target.Author = input.Author ?? string.Empty;
		target.Publisher = input.Publisher ?? string.Empty;
		target.Description = input.Description ?? string.Empty;
		target.Memo = input.Memo ?? string.Empty;
		target.IsIncomplete = input.IsIncomplete;

		// === 【共通処理】巻情報 ===
		target.StartVolume = (int)input.StartVolume;

		// 完結巻の反映：EndVolume が入力されている場合は SeriesCompleted = true
		if (input.EndVolume.HasValue && input.EndVolume.Value >= 1)
		{
			target.EndVolume = (int)input.EndVolume.Value;
			target.SeriesCompleted = true;
		}
		else
		{
			target.EndVolume = 0;
			target.SeriesCompleted = false;
			target.IsOwnedCompleted = false;
		}

		target.OwnedMaxVolume = inputOwnedMaxVolume;
		target.IsOwnedCompleted = input.IsOwnedCompleted;

		// === 【共通処理】Description 出典 ===
		// 既存正式作品ではここでは変更しない。新規・登録待ち作品のみ、入力があれば Manual、なければ None
		if (target.SeriesId == 0 || target.IsWork)
		{
			target.DescriptionSource = string.IsNullOrEmpty(input.Description)
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
	/// 入力値を編集用クローンへ反映したうえで、正式登録の前提条件を検証します。
	/// 検証はユーザーが登録ボタンを押したときにだけ実行します。
	/// </summary>
	/// <param name="input">画面で入力された値。</param>
	/// <returns>検出したエラーの種類。問題がない場合は <see cref="EditorValidationError.None"/>。</returns>
	public EditorValidationError ValidateForRegister(EditorInputValues input)
	{
		this.ApplyInput(input);

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
	/// 入力値を編集用クローンへ反映したうえで、一時保存の前提条件を検証します。
	/// </summary>
	/// <param name="input">画面で入力された値。</param>
	/// <returns>検出したエラーの種類。問題がない場合は <see cref="EditorValidationError.None"/>。</returns>
	public EditorValidationError ValidateForWorkSave(EditorInputValues input)
	{
		this.ApplyInput(input);

		return string.IsNullOrWhiteSpace(this.getEditTarget().Title)
			? EditorValidationError.TitleRequired
			: EditorValidationError.None;
	}

	/// <summary>
	/// 編集用クローンのタイトルを、編集開始時の元のタイトルへ戻します。
	/// 正本（<see cref="EditorStore.OriginalSeries"/>）は変更しません。
	/// </summary>
	/// <returns>元に戻したタイトル。</returns>
	public string RestoreOriginalTitle()
	{
		var original = this.getOriginalSeries();
		this.applyTitle(this.getEditTarget(), original.Title);
		return original.Title;
	}

	/// <summary>
	/// ドラッグアンドドロップまたはダイアログで指定されたファイル・フォルダを解析し、素材として追加します。
	/// 重複判定は以下の順で行います。
	/// 1. 同一 FullPath が存在する場合は追加せず「既に追加済み」として記録します。
	/// 2. FullPath は異なるが同名の素材が存在する場合は追加せず「同名ファイル」として記録します。
	/// 3. 上記以外は追加します。
	/// 追加後は並び替えと所持最大巻数の推定を行います。
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
			this.replaceMaterialFiles(this.editorStore.MaterialFiles.Concat(addedItems).ToList());
			ownedMaxVolumeUpdated = this.recalculateOwnedVolumeEstimate();
		}

		return new MaterialAddResult(addedItems.Count > 0, ownedMaxVolumeUpdated, alreadyAddedFiles, sameNameFiles);
	}

	/// <summary>
	/// 素材一覧から指定された素材を削除します。
	/// 削除できるのは今回の編集で追加した素材（CanRemove == true）だけです。
	/// 削除後は所持最大巻数の推定を行います。素材が 0 件になっても編集開始時の値へは戻しません。
	/// </summary>
	/// <param name="fullPath">削除対象の素材のフルパス。</param>
	/// <returns>素材削除の結果。</returns>
	public MaterialRemoveResult RemoveMaterialFile(string fullPath)
	{
		ArgumentException.ThrowIfNullOrEmpty(fullPath);

		var item = this.editorStore.MaterialFiles.FirstOrDefault(m => m.FullPath == fullPath);
		if (item is null || !item.CanRemove)
		{
			return new MaterialRemoveResult(false, false);
		}

		this.editorStore.MaterialFiles.Remove(item);

		return new MaterialRemoveResult(true, this.recalculateOwnedVolumeEstimate());
	}

	/// <summary>
	/// <see cref="EditorStore.MaterialFiles"/> を並び順で並べ替えます。
	/// フォルダ → ファイル（Archive / Epub）→ その他の順で分類し、同じ分類内では名前の昇順にします。
	/// </summary>
	public void SortMaterialFiles()
	{
		if (this.editorStore.MaterialFiles.Count == 0)
		{
			return;
		}

		this.replaceMaterialFiles(this.editorStore.MaterialFiles.ToList());
	}

	/// <summary>
	/// <see cref="EditorStore.MaterialFiles"/> の素材合計サイズ（バイト）を計算します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>素材合計サイズ（バイト）。素材がない場合は 0。</returns>
	public async ValueTask<long> CalculateMaterialTotalSizeAsync(CancellationToken cancellationToken = default)
	{
		var paths = this.editorStore.MaterialFiles.Select(m => m.FullPath).ToList();
		if (paths.Count == 0)
		{
			return 0L;
		}

		return await StorageSizeHelper.GetTotalAllAsync(paths, cancellationToken);
	}

	/// <summary>
	/// 別ドライブ移動の確認が必要かどうかを判定します。
	/// 今回追加した素材（CanRemove == true）の合計サイズが 1GiB 以上で、
	/// 追加素材の中に登録先素材フォルダとは異なるドライブの素材がある場合に true を返します。
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

		var totalSize = addedMaterials.Sum(m => this.getMaterialSize(m.FullPath));
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
	/// 既存正式作品の保存処理には、正本ではなく正本から DeepCopy した作業用コピーを渡します。
	/// 保存処理は DB Commit 後に、正本（<see cref="MangaSeriesStore"/> の共有インスタンス）へ編集内容を反映します。
	/// </remarks>
	/// <param name="thumbnailBytes">サムネイル JPEG の byte[]。ない場合は null。</param>
	/// <returns>保存処理の結果（作品情報と移動失敗素材を含む）。</returns>
	public async ValueTask<SeriesSaveResult> RegisterSeriesAsync(byte[]? thumbnailBytes)
	{
		var target = this.getEditTarget();
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

			// 正本は渡さず、保存処理が変更可能な作業用コピーを渡す
			var workingCopy = this.copySeries(this.getOriginalSeries());
			var saveManager = this.serviceProvider.GetRequiredKeyedService<ISeriesSaveManager>(SeriesSaveType.Existing);
			saveResult = await saveManager.SaveAsync(target, workingCopy, materialFiles, selectedFolder, thumbnailBytes);
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
	/// 一時保存処理には、編集用クローンから DeepCopy した作業用コピーを渡します。
	/// </summary>
	/// <param name="thumbnailBytes">サムネイル JPEG の byte[]。ない場合は null。</param>
	/// <returns>一時保存後の WorkId。</returns>
	public async ValueTask<int> SaveWorkSeriesAsync(byte[]? thumbnailBytes)
	{
		// 新規の一時保存では渡したインスタンスが Store の正本になるため、編集用クローンそのものは渡さない
		var workingCopy = this.copySeries(this.getEditTarget());
		var saveManager = this.serviceProvider.GetRequiredKeyedService<ISeriesSaveManager>(SeriesSaveType.Work);
		var saveResult = await saveManager.SaveAsync(workingCopy, null, [], null, thumbnailBytes);

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
	/// <see cref="EditorStore.MaterialFiles"/> の内容を、並び替えた指定の一覧へ置き換えます。
	/// </summary>
	/// <param name="materialFiles">置き換える素材一覧。</param>
	private void replaceMaterialFiles(List<MaterialFileItem> materialFiles)
	{
		var sorted = this.sortMaterialFiles(materialFiles);

		this.editorStore.MaterialFiles.Clear();
		this.editorStore.MaterialFiles.AddRange(sorted);
	}

	/// <summary>
	/// 現在の素材一覧から所持最大巻数を推定し、推定できた場合のみ編集用クローンの値を更新します。
	/// 推定できない場合は現在の値を変更しません。手動変更フラグは変更しません。
	/// </summary>
	/// <returns>所持最大巻数を更新した場合は true。</returns>
	private bool recalculateOwnedVolumeEstimate()
	{
		var materials = this.editorStore.MaterialFiles
			.Select(m => (m.Name, this.getVolumeNumberSourceType(m.ItemType)))
			.ToList();

		var result = this.ownedVolumeEstimator.Estimate(materials);
		if (result.OwnedMaxVolume <= 0)
		{
			return false;
		}

		this.getEditTarget().OwnedMaxVolume = result.OwnedMaxVolume;
		return true;
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
	/// ファイルまたはフォルダの合計サイズをバイト単位で取得します。
	/// </summary>
	/// <param name="fullPath">ファイルまたはフォルダの完全パス。</param>
	/// <returns>合計サイズ（バイト）。存在しない場合は 0。</returns>
	private long getMaterialSize(string fullPath)
	{
		var fileInfo = new FileInfo(fullPath);
		if (fileInfo.Exists)
		{
			return fileInfo.Length;
		}

		var dirInfo = new DirectoryInfo(fullPath);
		if (!dirInfo.Exists)
		{
			return 0;
		}

		return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
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
						// ファイル情報取得失敗時は null のまま
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
	/// 素材一覧を並び順で並べ替えます。
	/// フォルダ → ファイル（Archive / Epub）→ その他の順で分類し、同じ分類内では名前の昇順にします。
	/// </summary>
	/// <param name="materialFiles">並べ替え対象の素材一覧。</param>
	/// <returns>並べ替え後の素材一覧。</returns>
	private List<MaterialFileItem> sortMaterialFiles(IEnumerable<MaterialFileItem> materialFiles)
	{
		return materialFiles
			.OrderBy(item => item.ItemType switch
			{
				MaterialItemType.Folder => 0,
				MaterialItemType.Archive => 1,
				MaterialItemType.Epub => 1,
				_ => 2,
			})
			.ThenBy(item => item.Name, StringComparer.Ordinal)
			.ToList();
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
	/// 素材ソースを特定できた既存作品は、そのパスを含む登録済み素材フォルダを選択して変更不可にします。
	/// 素材ソースを持たない既存作品は、未選択で変更可能にします。
	/// </summary>
	/// <param name="series">編集対象の元作品。</param>
	/// <param name="materialSource">編集対象として特定した素材ソース。</param>
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
