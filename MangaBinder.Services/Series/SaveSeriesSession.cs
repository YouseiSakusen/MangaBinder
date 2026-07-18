using MangaBinder.Core.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Series;

/// <summary>
/// 作品保存時の確認フローを管理する一時セッションです。
/// Scoped サービスとして、保存前確認の状態を保持します。
/// </summary>
public class SaveSeriesSession
{
	/// <summary>編集対象の MangaSeries。</summary>
	private MangaSeries? editingSeries;

	/// <summary>選択された素材ソースフォルダ。</summary>
	private SourceFolder? selectedMaterialSourceFolder;

	/// <summary>別ドライブ確認済みフラグ。</summary>
	private bool differentDriveConfirmed;

	/// <summary>保存先管理マネージャー。</summary>
	private readonly MangaSeriesManager seriesManager;

	/// <summary>ロガー。</summary>
	private readonly ILogger<SaveSeriesSession>? logger;

	/// <summary>
	/// <see cref="SaveSeriesSession"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="seriesManager">保存を実行するマネージャー。</param>
	/// <param name="logger">ロガー。オプション。</param>
	public SaveSeriesSession(
		MangaSeriesManager seriesManager,
		ILogger<SaveSeriesSession>? logger = null)
	{
		this.seriesManager = seriesManager ?? throw new ArgumentNullException(nameof(seriesManager));
		this.logger = logger;
	}

	/// <summary>
	/// 保存前確認を実行します。
	/// 保存は行わず、確認内容のみを取得します。
	/// </summary>
	/// <param name="editingSeries">編集中の作品。</param>
	/// <param name="materialFiles">素材ファイル一覧。</param>
	/// <param name="selectedMaterialSourceFolder">選択された素材フォルダ。</param>
	/// <returns>確認結果。</returns>
	public ValueTask<SaveSeriesConfirmationResult> GetSaveSeriesConfirmationAsync(
		MangaSeries editingSeries,
		IReadOnlyList<MaterialFile> materialFiles,
		SourceFolder? selectedMaterialSourceFolder)
	{
		ArgumentNullException.ThrowIfNull(editingSeries);
		ArgumentNullException.ThrowIfNull(materialFiles);

		// セッション内の状態を保存
		this.editingSeries = editingSeries;
		this.selectedMaterialSourceFolder = selectedMaterialSourceFolder;

		this.logger?.LogInformation($"[SaveSeriesSession.GetSaveSeriesConfirmationAsync] 開始。SeriesId: {editingSeries.SeriesId}, Title: {editingSeries.Title}");

		// 既存作品の場合のみ確認フローを実行
		if (editingSeries.SeriesId == 0 || editingSeries.IsWork)
		{
			this.logger?.LogInformation("[SaveSeriesSession.GetSaveSeriesConfirmationAsync] 新規作品/登録待ち作品のため確認なし。");
			return ValueTask.FromResult(new SaveSeriesConfirmationResult(SaveSeriesConfirmationType.None));
		}

		// ① 素材ソース複数判定
		if (editingSeries.HasMultipleMaterialSources)
		{
			this.logger?.LogInformation($"[SaveSeriesSession.GetSaveSeriesConfirmationAsync] 複数の素材ソースを検出。Count: {editingSeries.MaterialSources.Count}");
			return ValueTask.FromResult(
				new SaveSeriesConfirmationResult(
					SaveSeriesConfirmationType.MaterialSource,
					editingSeries.MaterialSources));
		}

		// ② 別ドライブ移動判定
		if (!this.differentDriveConfirmed && this.needsDifferentDriveConfirmation(editingSeries, selectedMaterialSourceFolder))
		{
			this.logger?.LogInformation("[SaveSeriesSession.GetSaveSeriesConfirmationAsync] 別ドライブ移動が必要。");
			return ValueTask.FromResult(new SaveSeriesConfirmationResult(SaveSeriesConfirmationType.DifferentDrive));
		}

		// ③ 確認不要
		this.logger?.LogInformation("[SaveSeriesSession.GetSaveSeriesConfirmationAsync] 確認不要。");
		return ValueTask.FromResult(new SaveSeriesConfirmationResult(SaveSeriesConfirmationType.None));
	}

	/// <summary>
	/// 選択された素材ソースを設定します。
	/// MaterialSource 確認後に呼び出されます。
	/// </summary>
	/// <param name="selectedMaterialSource">ユーザーが選択した素材ソース。</param>
	public void SetSelectedMaterialSource(MangaSource selectedMaterialSource)
	{
		ArgumentNullException.ThrowIfNull(selectedMaterialSource);
		this.logger?.LogInformation($"[SaveSeriesSession.SetSelectedMaterialSource] SourceId: {selectedMaterialSource.SourceId}, Path: {selectedMaterialSource.Path}");
	}

	/// <summary>
	/// 別ドライブ確認済みをマークします。
	/// DifferentDrive 確認後に呼び出されます。
	/// </summary>
	public void MarkDifferentDriveConfirmed()
	{
		this.logger?.LogInformation("[SaveSeriesSession.MarkDifferentDriveConfirmed] 別ドライブ確認済みとしてマーク。");
		this.differentDriveConfirmed = true;
	}

	/// <summary>
	/// 保存処理を実行します。
	/// GetSaveSeriesConfirmationAsync() で ConfirmationType.None が返された後に呼び出します。
	/// </summary>
	/// <param name="editorStore">編集状態を保持するストア。保存対象の EditingSeries を取得します。</param>
	/// <param name="materialFiles">素材ファイル一覧。</param>
	/// <param name="thumbnailBytes">サムネイル画像。</param>
	/// <returns>保存後の作品。</returns>
	/// <exception cref="ArgumentNullException">editorStore が null の場合にスローされます。</exception>
	public async ValueTask<MangaSeries> SaveSeriesAsync(
		EditorStore editorStore,
		IReadOnlyList<MaterialFile> materialFiles,
		byte[]? thumbnailBytes)
	{
		ArgumentNullException.ThrowIfNull(editorStore);
		ArgumentNullException.ThrowIfNull(materialFiles);

		if (editorStore.EditingSeries == null)
			throw new InvalidOperationException("EditorStore に編集対象作品が設定されていません。");

		this.logger?.LogInformation($"[SaveSeriesSession.SaveSeriesAsync] 開始。SeriesId: {editorStore.EditingSeries.SeriesId}, Title: {editorStore.EditingSeries.Title}");

		// 既存の MangaSeriesManager.SaveSeriesAsync() へ委譲
		var savedSeries = await this.seriesManager.SaveSeriesAsync(
			editorStore,
			materialFiles,
			this.selectedMaterialSourceFolder,
			thumbnailBytes);

		this.logger?.LogInformation($"[SaveSeriesSession.SaveSeriesAsync] 完了。SavedSeriesId: {savedSeries.SeriesId}");

		return savedSeries;
	}

	/// <summary>
	/// 別ドライブ移動が必要かどうかを判定します。
	/// </summary>
	private bool needsDifferentDriveConfirmation(
		MangaSeries editingSeries,
		SourceFolder? selectedMaterialSourceFolder)
	{
		// 選択された素材フォルダがない場合は確認不要
		if (selectedMaterialSourceFolder == null)
			return false;

		// 単一素材ソースが存在しない場合は確認不要
		var singleSource = editingSeries.SingleMaterialSource;
		if (singleSource == null)
			return false;

		// 元のパスと選択されたフォルダが異なるドライブに存在するかチェック
		var originalDriveLetter = Path.GetPathRoot(singleSource.Path)?[0];
		var selectedDriveLetter = Path.GetPathRoot(selectedMaterialSourceFolder.FolderPath.Value)?[0];

		var isDifferentDrive = originalDriveLetter != null && selectedDriveLetter != null && originalDriveLetter != selectedDriveLetter;

		this.logger?.LogInformation(
			$"[SaveSeriesSession.needsDifferentDriveConfirmation] originalDrive: {originalDriveLetter}, selectedDrive: {selectedDriveLetter}, isDifferent: {isDifferentDrive}");

		return isDifferentDrive;
	}
}
