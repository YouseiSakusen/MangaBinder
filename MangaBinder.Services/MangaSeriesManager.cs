using HalationGhost.Utilities;
using MangaBinder.Bindings;
using MangaBinder.Core.Series;
using MangaBinder.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MangaBinder;

/// <summary>
/// Home 画面の初期化と BindingQueue 復元を統一する Manager クラスです。
/// また、作品編集セッションを管理します。
/// </summary>
public class MangaSeriesManager
{
	/// <summary>MangaSeries の取得を担う Repository。</summary>
	private readonly MangaRepository mangaRepository;

	/// <summary>登録待ち作品の取得を担う Repository。</summary>
	private readonly WorkMangaSeriesRepository workMangaSeriesRepository;

	/// <summary>BindingQueue の SeriesId 復元を担う Repository。</summary>
	private readonly BindingQueueRepository bindingQueueRepository;

	/// <summary>製本開始状態 Dispatcher。</summary>
	private readonly BindingQueueDispatcher bindingQueueDispatcher;

	/// <summary>MangaSeries の正本リストを管理するストア。</summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>タグを取得する Repository。</summary>
	private readonly TagRepository tagRepository;

	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>ログ出力用の Logger。</summary>
	private readonly ILogger<MangaSeriesManager>? logger;

	/// <summary>DI スコープを作成するファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>
	/// <see cref="MangaSeriesManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaRepository">MangaSeries の取得を担う Repository。</param>
	/// <param name="workMangaSeriesRepository">登録待ち作品の取得を担う Repository。</param>
	/// <param name="bindingQueueRepository">BindingQueue の SeriesId 復元を担う Repository。</param>
	/// <param name="bindingQueueDispatcher">製本開始状態 Dispatcher。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="tagRepository">タグを取得する Repository。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="serviceScopeFactory">DI スコープを作成するファクトリー。</param>
	/// <param name="logger">ログ出力用の Logger。オプション。</param>
	public MangaSeriesManager(
		MangaRepository mangaRepository,
		WorkMangaSeriesRepository workMangaSeriesRepository,
		BindingQueueRepository bindingQueueRepository,
		BindingQueueDispatcher bindingQueueDispatcher,
		MangaSeriesStore mangaSeriesStore,
		TagRepository tagRepository,
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory,
		ILogger<MangaSeriesManager>? logger = null)
	{
		this.mangaRepository = mangaRepository;
		this.workMangaSeriesRepository = workMangaSeriesRepository;
		this.bindingQueueRepository = bindingQueueRepository;
		this.bindingQueueDispatcher = bindingQueueDispatcher;
		this.mangaSeriesStore = mangaSeriesStore;
		this.tagRepository = tagRepository;
		this.appSettings = appSettings;
		this.serviceScopeFactory = serviceScopeFactory;
		this.logger = logger;
	}

	/// <summary>
	/// Home 画面初期化時に全 MangaSeries を取得し、BindingQueue を復元します。
	/// DB から取得した MangaSeries インスタンスを MangaSeriesStore に格納し、
	/// Store から取得した参照を BindingQueue や呼び出し元に返すことで、
	/// アプリケーション全体で同一のインスタンスを参照するようにします。
	/// 同時に、登録待ち作品（WorkMangaSeries）も取得して Store に格納します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>MangaSeriesStore に格納された MangaSeries のリスト。</returns>
	public async ValueTask<List<MangaSeries>> GetAllSeriesAsync(CancellationToken cancellationToken = default)
	{
		// 1. MangaRepository から MangaSeries 一覧を取得する
		var allSeriesReadOnly = await this.mangaRepository.GetAllSeriesAsync();
		var allSeries = allSeriesReadOnly.ToList();

		// 2. MangaSeriesStore に DB から取得した MangaSeries を格納する
		this.mangaSeriesStore.ReplaceAll(allSeries);

		// 3. WorkMangaSeriesRepository から登録待ち作品を取得する
		var allWorkSeriesReadOnly = await this.workMangaSeriesRepository.GetAllAsync();
		var allWorkSeries = allWorkSeriesReadOnly.ToList();

		// 4. MangaSeriesStore に登録待ち作品を格納する
		this.mangaSeriesStore.ReplaceWorkSeries(allWorkSeries);

		// 5. TagRepository からタグ一覧を取得する
		var allTags = this.tagRepository.GetAll();

		// 6. MangaSeriesStore にタグを格納する
		this.mangaSeriesStore.ReplaceTags(allTags);

		// 7. BindingQueue から SeriesId 一覧を取得する
		var queuedSeriesIds = await this.bindingQueueRepository.GetQueuedSeriesIdsAsync(cancellationToken);

		// 8. MangaSeriesStore から SeriesId が一致するインスタンスを探し、BindingSeries を構築する
		var bindingSeriesList = new List<BindingSeries>();
		foreach (var seriesId in queuedSeriesIds)
		{
			var matchedSeries = this.mangaSeriesStore.FindById(seriesId);
			if (matchedSeries != null)
			{
				var bindingSeries = new BindingSeries
				{
					Series = matchedSeries,
					Status = BindingStartStatus.Configuring,
					CurrentStep = 0,
					AddedAt = DateTime.Now,
					UpdatedAt = DateTime.Now,
				};
				bindingSeriesList.Add(bindingSeries);
			}
		}

		// 9. BindingQueueDispatcher に復元したBindingQueue一覧を設定する
		this.bindingQueueDispatcher.ReplaceAll(bindingSeriesList);

		// 10. MangaSeriesStore から取得した MangaSeries 一覧を返す
		return this.mangaSeriesStore.GetAll().ToList();
	}

	/// <summary>
	/// 検索文字列を利用して MangaSeries を検索します。
	/// 対象は MergedSeriesList（正式作品＋登録待ち作品）です。
	/// 検索は Google 風の複数ワード AND 検索です。
	/// </summary>
	/// <param name="searchText">検索文字列。</param>
	/// <returns>検索結果の MangaSeries リスト。</returns>
	public IReadOnlyList<MangaSeries> Search(string searchText)
	{
		// Store から MergedSeries を取得して検索
		var mergedSeries = this.mangaSeriesStore.GetMergedSeries();
		return this.Search(searchText, mergedSeries);
	}

	/// <summary>
	/// 指定されたリストに対して、検索文字列を利用して MangaSeries を検索します。
	/// 検索は Google 風の複数ワード AND 検索です。
	/// </summary>
	/// <param name="searchText">検索文字列。</param>
	/// <param name="targetSeries">検索対象となる MangaSeries リスト。</param>
	/// <returns>検索結果の MangaSeries リスト。</returns>
	public IReadOnlyList<MangaSeries> Search(string searchText, IReadOnlyList<MangaSeries> targetSeries)
	{
		// searchText が null、空文字、または空白のみの場合は空リストを返す
		if (string.IsNullOrWhiteSpace(searchText))
			return new List<MangaSeries>();

		// ワード分割（半角スペース・全角スペース）
		var words = searchText
			.Split(new[] { ' ', '\u3000' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(word => MangaTitleHelper.NormalizeTitleInternal(word))
			.Where(word => !string.IsNullOrEmpty(word))
			.ToList();

		// ワードが空の場合は空リストを返す
		if (words.Count == 0)
			return new List<MangaSeries>();

		// デバッグ出力
		System.Diagnostics.Debug.WriteLine($"[MangaSeriesManager.Search] Input: {searchText}, Words: {string.Join(", ", words)}, TargetCount: {targetSeries.Count}");

		// AND 検索：すべてのワードが Title OR Author OR Memo に含まれた作品のみヒット
		var results = targetSeries
			.Where(series => words.All(word =>
				series.NormalizedTitleInternal.Contains(word, StringComparison.OrdinalIgnoreCase) ||
				series.Author.Contains(word, StringComparison.OrdinalIgnoreCase) ||
				series.Memo.Contains(word, StringComparison.OrdinalIgnoreCase)
			))
			.ToList();

		System.Diagnostics.Debug.WriteLine($"[MangaSeriesManager.Search] Results: {results.Count}");

		return results;
	}

	/// <summary>
	/// 指定されたタイトルと同じタイトルを持つ作品を検索します。
	/// 対象は MergedSeriesList（正式作品＋登録待ち作品）です。
	/// 検索は正規化タイトルの完全一致で行います。
	/// </summary>
	/// <param name="title">検索するタイトル。</param>
	/// <returns>同一タイトルの作品リスト。タイトルが null または空白の場合は空リスト。</returns>
	public IReadOnlyList<MangaSeries> FindSameTitle(string? title)
	{
		// title が null、空文字、または空白のみの場合は空リストを返す
		if (string.IsNullOrWhiteSpace(title))
			return new List<MangaSeries>();

		// 入力タイトルを正規化
		var normalizedTitle = MangaTitleHelper.NormalizeTitleInternal(title);

		// 正規化後に空になった場合は空リストを返す
		if (string.IsNullOrEmpty(normalizedTitle))
			return new List<MangaSeries>();

		// MergedSeriesList から検索（正規化タイトルの完全一致）
		var mergedSeries = this.mangaSeriesStore.GetMergedSeries();

		var results = mergedSeries
			.Where(series => series.NormalizedTitleInternal == normalizedTitle)
			.ToList();

		return results;
	}

	/// <summary>
	/// 編集中の正式作品に対して、編集後タイトルが同一タイトルの作品と一致するかを判定します。
	/// 既存作品の保存処理で使用されることを想定しています。
	/// </summary>
	/// <remarks>
	/// 処理フロー：
	/// 1. 入力タイトルを正規化
	/// 2. 正規化タイトルで完全一致検索を実行
	/// 3. 検索結果から以下を判定：
	///    - 編集中作品自身のみ一致 → SameAsEditingSeriesSelf
	///    - 一致作品なし → NoMatchFound
	///    - 別の SeriesId の作品が一致 → DifferentSeriesMatched
	/// 
	/// 編集中作品とは、EditorStore.EditingSeries が示す作品を指します。
	/// 既存作品（SeriesId != 0 かつ IsWork == false）対象です。
	/// </remarks>
	/// <param name="editorStore">編集状態を保持するストア。EditingSeries と比較対象のタイトルを取得します。</param>
	/// <returns>タイトル一致結果を表す ExistingSeriesTitleMatchResult。タイトルが null / 空白の場合は NoMatchFound を返します。</returns>
	/// <exception cref="ArgumentNullException">editorStore が null または EditingSeries が null の場合にスローされます。</exception>
	public ExistingSeriesTitleMatchResult CheckExistingSeriesTitleMatch(EditorStore editorStore)
	{
		ArgumentNullException.ThrowIfNull(editorStore);
		ArgumentNullException.ThrowIfNull(editorStore.EditingSeries);

		// 編集中の作品を取得
		var editingSeries = editorStore.EditingSeries;
		var newTitle = editingSeries.Title;

		// 新しいタイトルが空白の場合は一致なしとして扱う
		if (string.IsNullOrWhiteSpace(newTitle))
			return ExistingSeriesTitleMatchResult.NoMatchFound;

		// 新しいタイトルを正規化
		var normalizedNewTitle = MangaTitleHelper.NormalizeTitleInternal(newTitle);

		// 正規化後に空になった場合は一致なしとして扱う
		if (string.IsNullOrEmpty(normalizedNewTitle))
			return ExistingSeriesTitleMatchResult.NoMatchFound;

		// 正規化タイトルで完全一致検索を実行
		var matchedSeries = this.FindSameTitle(newTitle);

		// 一致作品がない場合
		if (matchedSeries.Count == 0)
			return ExistingSeriesTitleMatchResult.NoMatchFound;

		// 一致作品から編集中作品を特定
		var editingSeriesInMatch = matchedSeries.FirstOrDefault(s =>
		{
			// 編集中の正式作品の場合、SeriesId で判定
			if (editingSeries.SeriesId != 0 && editingSeries.IsWork == false)
			{
				return s.SeriesId == editingSeries.SeriesId;
			}

			// 編集中の登録待ち作品の場合、WorkId で判定
			if (editingSeries.WorkId != 0)
			{
				return s.WorkId == editingSeries.WorkId;
			}

			// 新規作品の場合は該当なし
			return false;
		});

		// 編集中作品が一致に含まれている場合
		if (editingSeriesInMatch != null)
		{
			// 一致作品がちょうど1件（編集中作品のみ）の場合
			if (matchedSeries.Count == 1)
				return ExistingSeriesTitleMatchResult.SameAsEditingSeriesSelf;

			// 編集中作品以外にも一致作品が存在する場合
			return ExistingSeriesTitleMatchResult.DifferentSeriesMatched;
		}

		// 編集中作品が一致に含まれていない場合は、別の SeriesId と一致
		return ExistingSeriesTitleMatchResult.DifferentSeriesMatched;
	}

	/// <summary>
	/// 保存前の確認フローを実行します。
	/// 複数素材ソースの選択確認、別ドライブ移動の確認などを判定します。
	/// </summary>
	/// <param name="editorStore">編集状態を保持するストア。EditingSeries を参照します。</param>
	/// <param name="materialFiles">素材ファイル一覧。</param>
	/// <returns>確認が必要な場合は SaveSeriesConfirmationType と詳細情報、不要な場合は None を含む確認結果。</returns>
	/// <exception cref="ArgumentNullException">editorStore または materialFiles が null の場合にスローされます。</exception>
	public ValueTask<SaveSeriesConfirmationResult> GetSaveSeriesConfirmationAsync(
		EditorStore editorStore,
		IReadOnlyList<MaterialFile> materialFiles)
	{
		ArgumentNullException.ThrowIfNull(editorStore);
		ArgumentNullException.ThrowIfNull(materialFiles);

		var editingSeries = editorStore.EditingSeries;
		if (editingSeries == null)
			throw new InvalidOperationException("EditorStore に編集対象作品が設定されていません。");

		this.logger?.LogInformation($"[GetSaveSeriesConfirmationAsync] 開始。SeriesId: {editingSeries.SeriesId}, Title: {editingSeries.Title}");

		// 新規作品・登録待ち作品の場合は確認不要
		if (editingSeries.SeriesId == 0 || editingSeries.IsWork)
		{
			this.logger?.LogInformation("[GetSaveSeriesConfirmationAsync] 新規作品/登録待ち作品のため確認なし。");
			return ValueTask.FromResult(new SaveSeriesConfirmationResult(SaveSeriesConfirmationType.None));
		}

		// ① 素材ソース複数判定
		if (editingSeries.HasMultipleMaterialSources)
		{
			this.logger?.LogInformation($"[GetSaveSeriesConfirmationAsync] 複数の素材ソースを検出。Count: {editingSeries.MaterialSources.Count}");
			return ValueTask.FromResult(
				new SaveSeriesConfirmationResult(
					SaveSeriesConfirmationType.MaterialSource,
					editingSeries.MaterialSources));
		}

		// ② 別ドライブ移動判定
		if (!editorStore.DifferentDriveConfirmed && this.needsDifferentDriveConfirmation(editingSeries, editorStore.SelectedMaterialSourceFolder))
		{
			this.logger?.LogInformation("[GetSaveSeriesConfirmationAsync] 別ドライブ移動が必要。");
			return ValueTask.FromResult(new SaveSeriesConfirmationResult(SaveSeriesConfirmationType.DifferentDrive));
		}

		// ③ 確認不要
		this.logger?.LogInformation("[GetSaveSeriesConfirmationAsync] 確認不要。");
		return ValueTask.FromResult(new SaveSeriesConfirmationResult(SaveSeriesConfirmationType.None));
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
			$"[needsDifferentDriveConfirmation] originalDrive: {originalDriveLetter}, selectedDrive: {selectedDriveLetter}, isDifferent: {isDifferentDrive}");

		return isDifferentDrive;
	}

	/// <summary>
	/// 指定された作品の編集セッションを開始します。
	/// 編集開始時点の作品状態を DeepCopy して EditorStore に保持し、
	/// 後で変更判定や比較処理などに使用できるようにします。
	/// 新規作品・登録待ち作品・既存作品のすべてが同じ処理で扱われます。
	/// </summary>
	/// <param name="series">編集対象の作品。</param>
	/// <param name="editorStore">編集状態を保持するストア。</param>
	/// <exception cref="ArgumentNullException">series または editorStore が null の場合にスローされます。</exception>
	public void BeginEdit(MangaSeries series, EditorStore editorStore)
	{
		ArgumentNullException.ThrowIfNull(series);
		ArgumentNullException.ThrowIfNull(editorStore);

		// 編集対象を EditorStore に保持
		editorStore.EditingSeries = series;

		// 編集開始時点での状態を DeepCopy で EditorStore に保持
		editorStore.OriginalSeries = DeepCopyHelper.Copy(series);

		// DeepCopy 前後の Sources.Count を比較
		this.logger?.LogInformation($"[BeginEdit] DeepCopy前のSources.Count: {series.Sources.Count}, DeepCopy後のSources.Count: {editorStore.OriginalSeries?.Sources.Count ?? 0}");
	}

	/// <summary>
	/// 指定された作品の素材フォルダ直下のファイル・フォルダを取得します。
	/// 既存作品（IsWork == false）のみ対象。新規作品・登録待ち作品は空リストを返します。
	/// 素材フォルダが見つからない場合はログ出力されます。
	/// </summary>
	/// <param name="series">対象となる作品。</param>
	/// <returns>素材フォルダ直下のファイル・フォルダを表す MaterialFileItem のリスト。</returns>
	public List<MaterialFileItem> GetMaterialFiles(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		// 新規作品・登録待ち作品は対象外
		if (series.IsWork)
			return [];

		var result = new List<MaterialFileItem>();

		// 素材フォルダ一覧を取得
		var materialSources = series.MaterialSources;

		foreach (var source in materialSources)
		{
			// フォルダの存在確認
			if (!Directory.Exists(source.Path))
			{
				this.logger?.LogInformation(
					"素材フォルダが見つかりません。SeriesId={SeriesId}, Path={Path}",
					series.SeriesId,
					source.Path);
				continue;
			}

			try
			{
				// フォルダ直下のファイル・フォルダを列挙
				var entries = Directory.GetFileSystemEntries(source.Path, "*", SearchOption.TopDirectoryOnly);

				foreach (var entry in entries)
				{
					var fileAttributes = File.GetAttributes(entry);
					var isDirectory = (fileAttributes & FileAttributes.Directory) != 0;

					string name = Path.GetFileName(entry);
					var itemType = isDirectory
						? MaterialItemType.Folder
						: GetItemTypeFromExtension(Path.GetExtension(entry));

					long? sizeBytes = null;
					if (!isDirectory)
					{
						try
						{
							var fileInfo = new FileInfo(entry);
							sizeBytes = fileInfo.Length;
						}
						catch
						{
							// ファイル情報取得失敗時は null のまま
						}
					}

					result.Add(new MaterialFileItem
					{
						Name = name,
						FullPath = entry,
						ItemType = itemType,
						SizeBytes = sizeBytes,
						CanRemove = false,
					});
				}
			}
			catch (Exception ex)
			{
				this.logger?.LogWarning(
					ex,
					"素材フォルダの列挙中にエラーが発生しました。SeriesId={SeriesId}, Path={Path}",
					series.SeriesId,
					source.Path);
			}
		}

		return result
			.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
			.ToList();
	}

	/// <summary>
	/// ファイル拡張子から MaterialItemType を判定します。
	/// </summary>
	/// <param name="extension">ファイル拡張子（例：".zip", ".jpg"）。</param>
	/// <returns>判定された MaterialItemType。</returns>
	private MaterialItemType GetItemTypeFromExtension(string extension)
	{
		// 拡張子が空の場合
		if (string.IsNullOrWhiteSpace(extension))
			return MaterialItemType.Root;

		// SupportedExtensionHelper を使って判定
		if (SupportedExtensionHelper.IsArchive(extension))
			return MaterialItemType.Archive;

		// EPUB の判定（一般的に .epub 拡張子）
		if (extension.Equals(".epub", StringComparison.OrdinalIgnoreCase))
			return MaterialItemType.Epub;

		// 画像の判定
		if (SupportedExtensionHelper.IsImage(extension))
			return MaterialItemType.Folder; // 画像ファイルはアイコン表示用に Folder として返す

		// その他
		return MaterialItemType.Root;
	}

	/// <summary>
	/// 指定された作品を WorkMangaSeries テーブルへ一時保存し、Store へ反映します。
	/// series.WorkId == 0 の場合は新規 INSERT を行い、採番された WorkId を series.WorkId に反映します。
	/// series.WorkId != 0 の場合は UPDATE を行います。
	/// サムネイル byte[] が指定されている場合、WorkThumbnail フォルダへ JPEG ファイルとして保存し、
	/// 保存後に series.ThumbnailFileName と series.ThumbnailStatus を更新してから DB へ反映します。
	/// タグ（series.Tags）も WorkMangaSeriesTags テーブルへ保存します。
	/// 保存成功後、MangaSeriesStore の登録待ち作品一覧へ即座に反映されます。
	/// </summary>
	/// <param name="series">保存対象の作品。</param>
	/// <param name="thumbnailBytes">保存するサムネイル JPEG byte[]。null または空の場合はファイル保存をスキップします。</param>
	/// <returns>一時保存後の WorkId。</returns>
	/// <summary>
	/// 一時保存（作業作品）として編集中の作品を保存します。
	/// このメソッドは互換性維持のための委譲メソッドです。WorkSeriesSaveManager を呼び出します。
	/// </summary>
	/// <param name="series">保存対象の編集中作品。</param>
	/// <param name="thumbnailBytes">新しいサムネイル画像（バイナリ）。null の場合はスキップします。</param>
	/// <returns>保存後の作品の WorkId。</returns>
	public async ValueTask<int> SaveWorkSeriesAsync(MangaSeries series, byte[]? thumbnailBytes = null)
	{
		ArgumentNullException.ThrowIfNull(series);

		using var scope = this.serviceScopeFactory.CreateScope();
		var saveManager = scope.ServiceProvider.GetRequiredKeyedService<ISeriesSaveManager>(SeriesSaveType.Work);
		var savedSeries = await saveManager.SaveAsync(series, null, [], null, thumbnailBytes);
		return savedSeries.WorkId;
	}

	/// <summary>
	/// 編集中の作品を正式な MangaSeries として登録します。
	/// 新規作品（SeriesId == 0）および登録待ち作品（WorkId != 0）が対象です。
	/// 既存作品の更新は対象外です。
	/// </summary>
	/// <remarks>
	/// 処理順序：
	/// 1. 入力値検証
	/// 2. MangaSeries INSERT
	/// 3. 採番された SeriesId を editingSeries へ反映
	/// 4. サムネイル保存
	/// 5. 素材移動
	/// 6. 登録待ち作品の場合は WorkMangaSeries を削除
	/// 7. DB Commit
	/// </remarks>
	/// <param name="editingSeries">登録対象の MangaSeries。SeriesId == 0 または WorkId != 0 である必要があります。</param>
	/// <param name="materialFiles">移動対象の素材。CanRemove プロパティ付き。</param>
	/// <param name="destinationSourceFolder">登録先の素材フォルダ。</param>
	/// <param name="thumbnailBytes">アップロード済みサムネイル JPEG。null の場合は WorkThumbnail からコピーまたは作成なし。</param>
	/// <returns>登録後の MangaSeries。</returns>
	/// <summary>
	/// 新規作品を正式登録します。
	/// 処理は NewSeriesSaveManager へ委譲されます。
	/// </summary>
	/// <summary>
	/// 編集中の作品を正式に MangaSeries へ保存します。
	/// editingSeries の状態に応じて、新規作品・登録待ち作品の場合は NewSeriesSaveManager へ、
	/// 既存作品の場合は ExistingSeriesSaveManager へ委譲します。
	/// </summary>
	/// <param name="editorStore">編集状態を保持するストア。EditingSeries、OriginalSeries、SelectedMaterialSourceFolder を参照します。</param>
	/// <param name="materialFiles">素材ファイル一覧。</param>
	/// <param name="thumbnailBytes">サムネイル画像（バイナリ）。null の場合はスキップします。</param>
	/// <returns>保存後の正式作品。</returns>
	/// <exception cref="ArgumentNullException">editorStore が null の場合、または EditingSeries が null の場合にスローされます。</exception>
	/// <exception cref="InvalidOperationException">タイトル判定エラーまたはその他のバリデーションエラー。</exception>
	public async ValueTask<MangaSeries> SaveSeriesAsync(
		EditorStore editorStore,
		IReadOnlyList<MaterialFile> materialFiles,
		byte[]? thumbnailBytes)
	{
		ArgumentNullException.ThrowIfNull(editorStore);
		ArgumentNullException.ThrowIfNull(editorStore.EditingSeries);

		var editingSeries = editorStore.EditingSeries;
		var selectedMaterialSourceFolder = editorStore.SelectedMaterialSourceFolder;

		// editingSeries の状態で保存方法を判定
		if (editingSeries.SeriesId != 0 && !editingSeries.IsWork)
		{
			// 既存作品の場合：タイトル判定と DeepCopy を取得してから ExistingSeriesSaveManager へ委譲
			var titleMatchResult = this.CheckExistingSeriesTitleMatch(editorStore);
			if (titleMatchResult != ExistingSeriesTitleMatchResult.SameAsEditingSeriesSelf)
				throw new InvalidOperationException($"タイトル判定が不一致です。結果: {titleMatchResult}");

			var originalSeries = editorStore.OriginalSeries;
			if (originalSeries == null)
				throw new InvalidOperationException("編集開始時の DeepCopy が見つかりません。");

			using var scope = this.serviceScopeFactory.CreateScope();
			var saveManager = scope.ServiceProvider.GetRequiredKeyedService<ISeriesSaveManager>(SeriesSaveType.Existing);
			return await saveManager.SaveAsync(editingSeries, originalSeries, materialFiles, selectedMaterialSourceFolder, thumbnailBytes);
		}
		else
		{
			// 新規・登録待ち作品の場合：NewSeriesSaveManager へ委譲（originalSeries = null）
			using var scope = this.serviceScopeFactory.CreateScope();
			var saveManager = scope.ServiceProvider.GetRequiredKeyedService<ISeriesSaveManager>(SeriesSaveType.New);
			return await saveManager.SaveAsync(editingSeries, null, materialFiles, selectedMaterialSourceFolder, thumbnailBytes);
		}
	}
}

