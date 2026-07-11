using HalationGhost.Utilities;
using MangaBinder.Bindings;
using MangaBinder.Core.Series;
using MangaBinder.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;
using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using System.IO;
using System.Text;
using Dapper;

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

	/// <summary>サムネイル操作を管理する Manager。</summary>
	private readonly ThumbnailManager thumbnailManager;

	/// <summary>素材操作を管理する Manager。</summary>
	private readonly MaterialManager materialManager;

	/// <summary>ログ出力用の Logger。</summary>
	private readonly ILogger<MangaSeriesManager>? logger;

	/// <summary>編集セッション中の編集対象 Series。</summary>
	private MangaSeries? editingSeriesSnapshot;

	/// <summary>編集セッション中の編集開始時点での DeepCopy。</summary>
	private MangaSeries? editingSeriesOriginalSnapshot;

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
	/// <param name="thumbnailManager">サムネイル操作を管理する Manager。</param>
	/// <param name="materialManager">素材操作を管理する Manager。</param>
	/// <param name="logger">ログ出力用の Logger。オプション。</param>
	public MangaSeriesManager(
		MangaRepository mangaRepository,
		WorkMangaSeriesRepository workMangaSeriesRepository,
		BindingQueueRepository bindingQueueRepository,
		BindingQueueDispatcher bindingQueueDispatcher,
		MangaSeriesStore mangaSeriesStore,
		TagRepository tagRepository,
		AppSettings appSettings,
		ThumbnailManager thumbnailManager,
		MaterialManager materialManager,
		ILogger<MangaSeriesManager>? logger = null)
	{
		this.mangaRepository = mangaRepository;
		this.workMangaSeriesRepository = workMangaSeriesRepository;
		this.bindingQueueRepository = bindingQueueRepository;
		this.bindingQueueDispatcher = bindingQueueDispatcher;
		this.mangaSeriesStore = mangaSeriesStore;
		this.tagRepository = tagRepository;
		this.appSettings = appSettings;
		this.thumbnailManager = thumbnailManager;
		this.materialManager = materialManager;
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
	/// 編集中作品とは、GetEditingSeries() が返す作品を指します。
	/// 既存作品（SeriesId != 0 かつ IsWork == false）対象です。
	/// </remarks>
	/// <param name="newTitle">編集後の新しいタイトル。</param>
	/// <returns>タイトル一致結果を表す ExistingSeriesTitleMatchResult。タイトルが null / 空白の場合は NoMatchFound を返します。</returns>
	public ExistingSeriesTitleMatchResult CheckExistingSeriesTitleMatch(string? newTitle)
	{
		// 編集中の作品を取得
		var editingSeries = this.GetEditingSeries();

		// 編集セッションが開始していない場合や、新しいタイトルが空白の場合は一致なしとして扱う
		if (editingSeries == null || string.IsNullOrWhiteSpace(newTitle))
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
	/// 指定された作品の編集セッションを開始します。
	/// 編集開始時点の作品状態を DeepCopy して保持し、
	/// 後で変更判定や比較処理などに使用できるようにします。
	/// 新規作品・登録待ち作品・既存作品のすべてが同じ処理で扱われます。
	/// </summary>
	/// <param name="series">編集対象の作品。</param>
	/// <exception cref="ArgumentNullException">series が null の場合にスローされます。</exception>
	public void BeginEdit(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		// 編集対象を保持
		this.editingSeriesSnapshot = series;

		// 編集開始時点での状態を DeepCopy で保持
		this.editingSeriesOriginalSnapshot = DeepCopyHelper.Copy(series);
	}

	/// <summary>
	/// 現在の編集セッション中の編集対象 Series を取得します。
	/// 編集セッション未開始の場合は null を返します。
	/// </summary>
	/// <returns>編集中の Series、またはセッション未開始時は null。</returns>
	public MangaSeries? GetEditingSeries()
		=> this.editingSeriesSnapshot;

	/// <summary>
	/// 現在の編集セッション中の編集開始時点での DeepCopy を取得します。
	/// 編集セッション未開始の場合は null を返します。
	/// </summary>
	/// <returns>編集開始時点での Series コピー、またはセッション未開始時は null。</returns>
	public MangaSeries? GetEditingSeriesOriginal()
		=> this.editingSeriesOriginalSnapshot;

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
	public async ValueTask<int> SaveWorkSeriesAsync(MangaSeries series, byte[]? thumbnailBytes = null)
	{
		ArgumentNullException.ThrowIfNull(series);

		if (series.WorkId == 0)
		{
			// 新規 INSERT：採番された WorkId を返す
			var workId = await this.workMangaSeriesRepository.InsertAsync(series);
			// series.WorkId は InsertAsync 内で既に設定されているが、念のため保証
			if (series.WorkId == 0)
				series.WorkId = workId;

			// タグを保存
			await this.workMangaSeriesRepository.SaveWorkTagsAsync(new[] { series });

			// サムネイル JPEG を保存（存在する場合のみ）
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				// ファイル名を決定（WorkThumbnailFileNameBase を使用）
				var fileName = $"{series.WorkThumbnailFileNameBase}.jpg";

				// ThumbnailManager で保存
				await this.thumbnailManager.SaveWorkThumbnailAsync(fileName, thumbnailBytes);

				// series の ThumbnailFileName と ThumbnailStatus を更新
				series.ThumbnailFileName = fileName;
				series.ThumbnailStatus = ThumbnailStatus.Completed;

				// ファイル保存後、DB に反映
				await this.workMangaSeriesRepository.UpdateAsync(series);
			}

			// Store へ即座に反映
			this.mangaSeriesStore.UpdateWorkSeries(series);

			return workId;
		}
		else
		{
			// UPDATE（既存の登録待ち作品の更新）
			// タグを保存
			await this.workMangaSeriesRepository.SaveWorkTagsAsync(new[] { series });

			// サムネイル JPEG を保存（存在する場合のみ）
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				// ファイル名を決定（WorkThumbnailFileNameBase を使用）
				var fileName = $"{series.WorkThumbnailFileNameBase}.jpg";

				// ThumbnailManager で保存
				await this.thumbnailManager.SaveWorkThumbnailAsync(fileName, thumbnailBytes);

				// series の ThumbnailFileName と ThumbnailStatus を更新
				series.ThumbnailFileName = fileName;
				series.ThumbnailStatus = ThumbnailStatus.Completed;
			}

			// DB へ反映
			await this.workMangaSeriesRepository.UpdateAsync(series);

			// Store へ即座に反映
			this.mangaSeriesStore.UpdateWorkSeries(series);

			return series.WorkId;
		}
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
	public async ValueTask<MangaSeries> RegisterSeriesAsync(
		MangaSeries editingSeries,
		IReadOnlyList<MaterialFile> materialFiles,
		SourceFolder destinationSourceFolder,
		byte[]? thumbnailBytes)
	{
		// 入力値検証
		ArgumentNullException.ThrowIfNull(editingSeries);
		ArgumentNullException.ThrowIfNull(materialFiles);
		ArgumentNullException.ThrowIfNull(destinationSourceFolder);

		// 登録対象外の作品は例外
		if (editingSeries.SeriesId != 0 && editingSeries.WorkId == 0)
		{
			throw new InvalidOperationException("既存作品（SeriesId != 0 かつ WorkId == 0）の更新は対象外です。");
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
				destinationSourceFolder,
				editingSeries.MaterialFolderName,
				materialFiles);

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

				/// <summary>
				/// 既存の正式作品を更新します。
				/// 編集開始時の DeepCopy を保存用オブジェクトとして利用し、
				/// 画面編集後の値との比較で OwnedMaxVolume の手修正判定を実施します。
				/// タイトル判定（CheckExistingSeriesTitleMatch）で SameAsEditingSeriesSelf の場合のみ処理します。
				/// DB 更新成功後に、DeepCopy からStore 内の正式作品インスタンスへ編集可能項目をコピーします。
				/// </summary>
				/// <param name="editingSeries">更新対象の編集中作品。SeriesId != 0 かつ IsWork == false である必要があります。</param>
				/// <returns>更新後の正式作品（Store 内インスタンス）。</returns>
				/// <exception cref="InvalidOperationException">タイトル判定が不一致の場合または作品が見つからない場合にスローされます。</exception>
				public async ValueTask<MangaSeries> UpdateExistingSeriesAsync(MangaSeries editingSeries)
				{
					// 入力値の検証
					if (editingSeries.SeriesId == 0 || editingSeries.IsWork)
						throw new InvalidOperationException("UpdateExistingSeriesAsync は既存の正式作品（SeriesId != 0 かつ IsWork == false）でのみ実行可能です。");

					// タイトル判定を実施
					var titleMatchResult = this.CheckExistingSeriesTitleMatch(editingSeries.Title);
					if (titleMatchResult != ExistingSeriesTitleMatchResult.SameAsEditingSeriesSelf)
						throw new InvalidOperationException($"タイトル判定が不一致です。結果: {titleMatchResult}");

					// === DeepCopy を取得（保存用オブジェクト） ===
					var originalSeries = this.GetEditingSeriesOriginal();
					if (originalSeries == null)
						throw new InvalidOperationException("編集開始時の DeepCopy が見つかりません。");

					// === OwnedMaxVolume の手修正判定（反映前に実施） ===
					// DeepCopy（編集開始時）と editingSeries（UI 入力後）の OwnedMaxVolume を比較
					var isOwnedMaxVolumeChanged = originalSeries.OwnedMaxVolume != editingSeries.OwnedMaxVolume;
					if (isOwnedMaxVolumeChanged)
					{
						originalSeries.IsOwnedMaxVolumeManuallyEdited = true;
					}
					// 変更がない場合は現在値を維持

					// === DeepCopy へ画面入力値を反映 ===
					// 共通処理（UpdateEditingSeriesFromUI で実施済みの値）を DeepCopy へコピー
					this.CopyEditableFieldsFromToEditingToDeepCopy(editingSeries, originalSeries);

					using var connection = new SQLiteConnection(this.appSettings.ConnectionString);
					await connection.OpenAsync();

					using var tx = connection.BeginTransaction();
					try
					{
						// === DB UPDATE（DeepCopy を対象） ===
						await this.mangaRepository.UpdateSeriesAsync(originalSeries);

						// === MangaSeriesTags の更新（DELETE → INSERT） ===
						await this.SaveSeriesTagsInTransactionAsync(connection, tx, originalSeries.SeriesId, originalSeries.Tags);

						// === Commit ===
						tx.Commit();

						// === Commit 成功後、Store 内の正式作品インスタンスを更新 ===
						var storeInstance = this.mangaSeriesStore.FindById(originalSeries.SeriesId);
						if (storeInstance is null)
							throw new InvalidOperationException($"Store から SeriesId {originalSeries.SeriesId} の正式作品が見つかりません。");

						// DeepCopy から Store インスタンスへ編集可能項目をコピー
						this.CopyEditableFieldsFromToEditableToStore(originalSeries, storeInstance);

						return storeInstance;
					}
					catch
					{
						tx.Rollback();
						throw;
					}
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
			}

