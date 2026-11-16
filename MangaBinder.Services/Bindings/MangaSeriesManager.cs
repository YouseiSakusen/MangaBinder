using HalationGhost.Utilities;
using MangaBinder.Helpers;
using MangaBinder.Series;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Bindings;

/// <summary>
/// 作品編集（Editor）の編集開始・編集中の操作・保存処理を担当する Scoped の Manager です。
/// 現時点では編集開始処理のみを実装しています。
/// 状態は <see cref="EditorStore"/> が保持し、この Manager は状態を持ちません。
/// 画面遷移は行いません。呼び出し元が <see cref="PrepareEditAsync"/> の完了を待ってから遷移します。
/// </summary>
public sealed class MangaSeriesManager
{
	/// <summary>編集セッション状態を保持するストア。</summary>
	private readonly EditorStore editorStore;

	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>製本待ちを操作する Manager。</summary>
	private readonly BindingQueueManager bindingQueueManager;

	/// <summary>タグマスタの共有 Notifier を所有するストア。</summary>
	private readonly TagStore tagStore;

	/// <summary>ログ出力用の Logger。</summary>
	private readonly ILogger<MangaSeriesManager> logger;

	/// <summary>
	/// <see cref="MangaSeriesManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="editorStore">編集セッション状態を保持するストア。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="bindingQueueManager">製本待ちを操作する Manager。</param>
	/// <param name="tagStore">タグマスタの共有 Notifier を所有するストア。</param>
	/// <param name="logger">ログ出力用の Logger。</param>
	public MangaSeriesManager(
		EditorStore editorStore,
		AppSettings appSettings,
		BindingQueueManager bindingQueueManager,
		TagStore tagStore,
		ILogger<MangaSeriesManager> logger)
	{
		this.editorStore = editorStore;
		this.appSettings = appSettings;
		this.bindingQueueManager = bindingQueueManager;
		this.tagStore = tagStore;
		this.logger = logger;
	}

	/// <summary>
	/// 編集対象の作品を受け取り、<see cref="EditorStore"/> の編集用データをすべて準備します。
	/// 元作品を <see cref="EditorStore.OriginalSeries"/> に保持し、DeepCopy した編集用クローンを
	/// <see cref="EditorStore.EditTarget"/> に設定します。元作品は変更しません。
	/// 画面遷移は行わないため、完了後に呼び出し元が Editor へ遷移してください。
	/// </summary>
	/// <param name="series">編集対象の元作品。</param>
	/// <returns>準備完了を表す非同期処理。</returns>
	public async ValueTask PrepareEditAsync(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		// 編集用クローンを生成する。Tags は JSON でコピーされないため、共有タグインスタンスを別途引き継ぐ
		var clone = DeepCopyHelper.Copy(series);
		foreach (var tag in series.Tags)
		{
			clone.Tags.Add(tag);
		}

		// 素材一覧（既存作品のみ）を取得して初期ソートする
		var materialFiles = this.sortMaterialFiles(this.getMaterialFiles(series));

		// 素材合計サイズを計算する
		var materialTotalSizeBytes = materialFiles.Count == 0
			? 0L
			: await StorageSizeHelper.GetTotalAllAsync(materialFiles.Select(m => m.FullPath).ToList());

		// 登録先素材フォルダの候補と初期選択を決定する
		var materialSourceFolders = this.appSettings.SourceFolders
			.Where(f => f.Role.Value == FolderRole.Material)
			.ToList();
		var (selectedFolder, canSelectFolder) = this.resolveMaterialSourceFolder(series, materialSourceFolders);

		// 製本待ち状態は正式登録済み作品のみ Queue の状態を反映する
		var isBindingQueued = series.SeriesId != 0 && !series.IsWork
			&& this.bindingQueueManager.Contains(series.SeriesId);

		// 編集開始時の所持最大巻（素材削除時の復元用）
		double? startOwnedMaxVolume = clone.OwnedMaxVolume > 0 ? clone.OwnedMaxVolume : null;

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
			startOwnedMaxVolume);

		this.logger.LogInformation(
			"編集準備が完了しました。SeriesId={SeriesId}, WorkId={WorkId}, MaterialFileCount={MaterialFileCount}",
			series.SeriesId,
			series.WorkId,
			materialFiles.Count);
	}

	/// <summary>
	/// 指定された作品の素材フォルダ直下のファイル・フォルダを取得します。
	/// 新規作品・登録待ち作品（IsWork == true）は空リストを返します。
	/// </summary>
	/// <param name="series">対象となる作品。</param>
	/// <returns>素材フォルダ直下のファイル・フォルダを表す <see cref="MaterialFileItem"/> のリスト。</returns>
	private List<MaterialFileItem> getMaterialFiles(MangaSeries series)
	{
		if (series.IsWork)
			return [];

		var result = new List<MaterialFileItem>();

		foreach (var source in series.MaterialSources)
		{
			if (!Directory.Exists(source.Path))
			{
				this.logger.LogInformation(
					"素材フォルダが見つかりません。SeriesId={SeriesId}, Path={Path}",
					series.SeriesId,
					source.Path);
				continue;
			}

			try
			{
				var entries = Directory.GetFileSystemEntries(source.Path, "*", SearchOption.TopDirectoryOnly);

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
					source.Path);
			}
		}

		return result;
	}

	/// <summary>
	/// 素材一覧を並び順で並べ替えます。
	/// フォルダ → ファイル（Archive / Epub）→ その他の順で分類し、同じ分類内では名前の昇順にします。
	/// </summary>
	/// <param name="materialFiles">並べ替え対象の素材一覧。</param>
	/// <returns>並べ替え後の素材一覧。</returns>
	private List<MaterialFileItem> sortMaterialFiles(List<MaterialFileItem> materialFiles)
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
	/// 素材フォルダを持つ既存作品は、一致する候補を選択して変更不可にします。
	/// 素材フォルダを持たない既存作品は、未選択で変更可能にします。
	/// </summary>
	/// <param name="series">編集対象の元作品。</param>
	/// <param name="materialSourceFolders">登録先素材フォルダの候補。</param>
	/// <returns>初期選択の素材フォルダ（未選択の場合は null）と、変更可否。</returns>
	private (SourceFolder? Selected, bool CanSelect) resolveMaterialSourceFolder(
		MangaSeries series,
		IReadOnlyList<SourceFolder> materialSourceFolders)
	{
		if (series.SeriesId == 0)
			return (materialSourceFolders.FirstOrDefault(), true);

		if (series.HasMaterialSources)
			return (this.findMatchingSourceFolder(series, materialSourceFolders), false);

		return (null, true);
	}

	/// <summary>
	/// 既存作品の Material フォルダに対応する SourceFolder を検索します。
	/// 複数の MaterialSource がある場合は先頭を基準に検索し、
	/// 複数の SourceFolder が一致する場合は FolderPath が最も長いものを優先します。
	/// </summary>
	/// <param name="series">既存作品。</param>
	/// <param name="materialSourceFolders">登録先素材フォルダの候補。</param>
	/// <returns>一致した SourceFolder、または見つからない場合は null。</returns>
	private SourceFolder? findMatchingSourceFolder(MangaSeries series, IReadOnlyList<SourceFolder> materialSourceFolders)
	{
		var targetMaterialPath = Path.GetFullPath(series.Sources.First(s => s.Role == FolderRole.Material).Path);

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
