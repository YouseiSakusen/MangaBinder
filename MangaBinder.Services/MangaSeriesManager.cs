using HalationGhost.Utilities;
using MangaBinder.Bindings;
using MangaBinder.Core.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;
using Microsoft.Extensions.Logging;
using System.IO;

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
	/// <param name="logger">ログ出力用の Logger。オプション。</param>
	public MangaSeriesManager(
		MangaRepository mangaRepository,
		WorkMangaSeriesRepository workMangaSeriesRepository,
		BindingQueueRepository bindingQueueRepository,
		BindingQueueDispatcher bindingQueueDispatcher,
		MangaSeriesStore mangaSeriesStore,
		TagRepository tagRepository,
		AppSettings appSettings,
		ILogger<MangaSeriesManager>? logger = null)
	{
		this.mangaRepository = mangaRepository;
		this.workMangaSeriesRepository = workMangaSeriesRepository;
		this.bindingQueueRepository = bindingQueueRepository;
		this.bindingQueueDispatcher = bindingQueueDispatcher;
		this.mangaSeriesStore = mangaSeriesStore;
		this.tagRepository = tagRepository;
		this.appSettings = appSettings;
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

		// MergedSeriesList から検索
		var mergedSeries = this.mangaSeriesStore.GetMergedSeries();

		// デバッグ出力
		System.Diagnostics.Debug.WriteLine($"[MangaSeriesManager.Search] Input: {searchText}, Words: {string.Join(", ", words)}, MergedCount: {mergedSeries.Count}");

		// AND 検索：すべてのワードが Title OR Author OR Memo に含まれた作品のみヒット
		var results = mergedSeries
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

			// サムネイル JPEG を保存（存在する場合のみ）
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				await this.saveThumbnailAsync(series, thumbnailBytes);
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
			// サムネイル JPEG を保存（存在する場合のみ）
			if (thumbnailBytes != null && thumbnailBytes.Length > 0)
			{
				await this.saveThumbnailAsync(series, thumbnailBytes);
			}

			// DB へ反映
			await this.workMangaSeriesRepository.UpdateAsync(series);

			// Store へ即座に反映
			this.mangaSeriesStore.UpdateWorkSeries(series);

			return series.WorkId;
		}
	}

	/// <summary>
	/// サムネイル JPEG byte[] をファイルとして WorkThumbnail フォルダへ保存し、
	/// series.ThumbnailFileName と series.ThumbnailStatus を更新します。
	/// </summary>
	/// <param name="series">保存対象の作品。WorkId が設定されていること。</param>
	/// <param name="thumbnailBytes">JPEG byte[] データ。</param>
	private async ValueTask saveThumbnailAsync(MangaSeries series, byte[] thumbnailBytes)
	{
		try
		{
			// ファイル名を決定（WorkThumbnailFileNameBase を使用）
			var fileName = $"{series.WorkThumbnailFileNameBase}.jpg";

			// 保存先パスを取得
			var filePath = this.appSettings.GetWorkThumbnailFullPath(fileName);

			// JPEG を保存
			await File.WriteAllBytesAsync(filePath, thumbnailBytes);

			// series の ThumbnailFileName と ThumbnailStatus を更新
			series.ThumbnailFileName = fileName;
			series.ThumbnailStatus = ThumbnailStatus.Completed;
		}
		catch (Exception ex)
		{
			// ファイル保存失敗時はログとともに例外を投げる
			this.logger?.LogError(ex, "サムネイル JPEG 保存に失敗しました。WorkId={WorkId}", series.WorkId);
			throw;
		}
	}
}

