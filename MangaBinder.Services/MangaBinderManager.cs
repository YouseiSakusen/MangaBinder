using System.Data.SQLite;
using HalationGhost.Utilities;
using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Series;
using MangaBinder.Settings;
using MangaBinder.Tags;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MangaBinder;

/// <summary>
/// Home 画面の初期化と BindingQueue 復元を統一する Manager クラスです。
/// また、作品の検索・重複判定・削除を提供します。
/// </summary>
public class MangaBinderManager
{
	/// <summary>MangaSeries の取得を担う Repository。</summary>
	private readonly MangaRepository mangaRepository;

	/// <summary>登録待ち作品の取得を担う Repository。</summary>
	private readonly WorkMangaSeriesRepository workMangaSeriesRepository;

	/// <summary>製本待ちの復元を担う Manager。</summary>
	private readonly BindingQueueManager bindingQueueManager;

	/// <summary>製本開始状態 Dispatcher。作品編集の保存時に使用する移行期間中の旧経路です。</summary>
	private readonly BindingQueueDispatcher bindingQueueDispatcher;

	/// <summary>MangaSeries の正本リストを管理するストア。</summary>
	private readonly MangaSeriesStore mangaSeriesStore;

	/// <summary>製本待ち作品を管理するストア。</summary>
	private readonly BindingQueueStore bindingQueueStore;

	/// <summary>タグマスタの読み込みと TagStore への反映を担う Manager。</summary>
	private readonly TagManager tagManager;

	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>ログ出力用の Logger。</summary>
	private readonly ILogger<MangaBinderManager>? logger;

	/// <summary>DI スコープを作成するファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>
	/// <see cref="MangaBinderManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="mangaRepository">MangaSeries の取得を担う Repository。</param>
	/// <param name="workMangaSeriesRepository">登録待ち作品の取得を担う Repository。</param>
	/// <param name="bindingQueueManager">製本待ちの復元を担う Manager。</param>
	/// <param name="bindingQueueDispatcher">製本開始状態 Dispatcher。</param>
	/// <param name="mangaSeriesStore">MangaSeries の正本リストを管理するストア。</param>
	/// <param name="bindingQueueStore">製本待ち作品を管理するストア。</param>
	/// <param name="tagManager">タグマスタの読み込みと TagStore への反映を担う Manager。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="serviceScopeFactory">DI スコープを作成するファクトリー。</param>
	/// <param name="logger">ログ出力用の Logger。オプション。</param>
	public MangaBinderManager(
		MangaRepository mangaRepository,
		WorkMangaSeriesRepository workMangaSeriesRepository,
		BindingQueueManager bindingQueueManager,
		BindingQueueDispatcher bindingQueueDispatcher,
		MangaSeriesStore mangaSeriesStore,
		BindingQueueStore bindingQueueStore,
		TagManager tagManager,
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory,
		ILogger<MangaBinderManager>? logger = null)
	{
		this.mangaRepository = mangaRepository;
		this.workMangaSeriesRepository = workMangaSeriesRepository;
		this.bindingQueueManager = bindingQueueManager;
		this.bindingQueueDispatcher = bindingQueueDispatcher;
		this.mangaSeriesStore = mangaSeriesStore;
		this.bindingQueueStore = bindingQueueStore;
		this.tagManager = tagManager;
		this.appSettings = appSettings;
		this.serviceScopeFactory = serviceScopeFactory;
		this.logger = logger;
	}

	/// <summary>
	/// MangaSeriesStore が初期化済みでない場合だけ、全 MangaSeries の取得と BindingQueue の復元を実施します。
	/// 既に初期化済みの場合は何もせず return します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	public async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
	{
		if (this.mangaSeriesStore.IsInitialized)
		{
			return;
		}

		await this.GetAllSeriesAsync(cancellationToken);
	}

	/// <summary>
	/// Home 画面初期化時にタグマスタと全 MangaSeries を取得し、BindingQueue を復元します。
	/// 先にタグマスタを TagStore へ読み込み、MangaSeriesNotifier 生成時点で共有 TagNotifier が存在するようにします。
	/// DB から取得した MangaSeries インスタンスを MangaSeriesStore に格納し、
	/// Store から取得した参照を BindingQueue や呼び出し元に返すことで、
	/// アプリケーション全体で同一のインスタンスを参照するようにします。
	/// 同時に、登録待ち作品（WorkMangaSeries）も取得して Store に格納します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>MangaSeriesStore に格納された MangaSeries のリスト。</returns>
	public async ValueTask<List<MangaSeries>> GetAllSeriesAsync(CancellationToken cancellationToken = default)
	{
		// 1. TagManager からタグマスタを読み込み、TagStore を初期化する（MangaSeriesNotifier 生成前に必要）
		this.tagManager.LoadTags();

		// 2. MangaRepository から MangaSeries 一覧を取得する
		var allSeriesReadOnly = await this.mangaRepository.GetAllSeriesAsync();
		var allSeries = allSeriesReadOnly.ToList();

		// 3. MangaSeriesStore に DB から取得した MangaSeries を格納する
		this.mangaSeriesStore.ReplaceAll(allSeries);

		// 4. WorkMangaSeriesRepository から登録待ち作品を取得する
		var allWorkSeriesReadOnly = await this.workMangaSeriesRepository.GetAllAsync();
		var allWorkSeries = allWorkSeriesReadOnly.ToList();

		// 5. MangaSeriesStore に登録待ち作品を格納する
		this.mangaSeriesStore.ReplaceWorkSeries(allWorkSeries);

		// 6. BindingQueueManager が DB の製本待ちを復元し、共有 Notifier から BindingSeries を構築して BindingStore.Queue へ反映する
		await this.bindingQueueManager.RestoreAsync(cancellationToken);

		// 7. Store の初期化を完了とマーク
		this.mangaSeriesStore.MarkInitialized();

		// 8. MangaSeriesStore から取得した MangaSeries 一覧を返す
		return this.mangaSeriesStore.All.Select(vm => vm.Series.Value).ToList();
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
		// 共通検索クラスで検索条件を構築
		var matcher = new MangaSeriesSearchMatcher(searchText);

		// 検索条件が無効な場合は空リストを返す
		if (!matcher.IsValid)
			return new List<MangaSeries>();

		// デバッグ出力
		System.Diagnostics.Debug.WriteLine($"[MangaSeriesManager.Search] Input: {searchText}, TargetCount: {targetSeries.Count}");

		// AND 検索：共通マッチャーを使用して検索
		var results = targetSeries
			.Where(series => matcher.IsMatch(series))
			.ToList();

		System.Diagnostics.Debug.WriteLine($"[MangaSeriesManager.Search] Results: {results.Count}");

		return results;
	}

	/// <summary>
	/// 指定されたタイトルと同じタイトルを持つ作品を検索します。
	/// 対象は Store.All と Store.WorkSeries（正式作品＋登録待ち作品の新 Reactive Collection）です。
	/// 検索は正規化タイトルの完全一致で行います。
	/// </summary>
	/// <param name="title">検索するタイトル。</param>
	/// <returns>同一タイトルの作品リスト。タイトルが null または空白の場合は空リスト。</returns>

	/// <summary>
	/// 2つの作品が同一作品であるかを判定します。
	/// NormalizedTitleInternal と Author の両方が一致する場合、同一作品と判定します。
	/// Author の比較は前後の空白を Trim した完全一致です。
	/// </summary>
	/// <param name="series1">比較対象の作品1。</param>
	/// <param name="series2">比較対象の作品2。</param>
	/// <returns>同一作品の場合は true、そうでない場合は false。</returns>
	private bool IsSameSeries(MangaSeries series1, MangaSeries series2)
	{
		// NormalizedTitleInternal が一致しない場合は別作品
		if (series1.NormalizedTitleInternal != series2.NormalizedTitleInternal)
			return false;

		// Author を Trim して比較（前後の空白のみ除去）
		var author1 = series1.Author?.Trim() ?? string.Empty;
		var author2 = series2.Author?.Trim() ?? string.Empty;

		// Author も一致する場合は同一作品
		return author1 == author2;
	}

	/// <summary>
	/// 編集対象の作品と重複する別作品を検索します。
	/// 対象は Store.All と Store.WorkSeries（正式作品＋登録待ち作品の新 Reactive Collection）です。
	/// 同じ NormalizedTitleInternal かつ同じ Author を持つ作品を検索し、編集対象自身は除外します。
	/// </summary>
	/// <remarks>
	/// 除外対象：
	/// - 既存正式作品の場合：同じ SeriesId の作品
	/// - 登録待ち作品の場合：同じ WorkId の作品
	/// - 新規作品の場合：除外対象なし
	/// </remarks>
	/// <param name="editingSeries">編集対象の作品。</param>
	/// <returns>重複する別作品のリスト。重複なし、または入力が不正な場合は空リスト。</returns>
	public IReadOnlyList<MangaSeries> FindDuplicateSeries(MangaSeries editingSeries)
	{
		ArgumentNullException.ThrowIfNull(editingSeries);

		// タイトルが入力されていない場合は空リスト
		if (string.IsNullOrWhiteSpace(editingSeries.Title))
			return new List<MangaSeries>();

		// 候補検索
		var candidates = this.FindSameTitle(editingSeries.Title);

		// 編集対象自身を除外
		var results = candidates
			.Where(candidate =>
			{
				// 既存正式作品の場合、SeriesId で判定
				if (editingSeries.SeriesId != 0 && !editingSeries.IsWork)
				{
					return candidate.SeriesId != editingSeries.SeriesId;
				}

				// 登録待ち作品の場合、WorkId で判定
				if (editingSeries.WorkId != 0)
				{
					return candidate.WorkId != editingSeries.WorkId;
				}

				// 新規作品の場合は除外対象なし（すべてが対象）
				return true;
			})
			// さらに IsSameSeries で Author を確認
			.Where(candidate => this.IsSameSeries(editingSeries, candidate))
			.ToList();

		return results;
	}

	/// <summary>
	/// 指定されたタイトルで候補検索を実行します。
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

		// All + WorkSeries から検索（正規化タイトルの完全一致）
		// 新 Reactive All/WorkSeries の共有 MangaSeriesNotifier から MangaSeries を抽出
		var results = this.mangaSeriesStore.All
			.Concat(this.mangaSeriesStore.WorkSeries)
			.Select(vm => vm.Series.Value)
			.Where(series => series.NormalizedTitleInternal == normalizedTitle)
			.ToList();

		return results;
	}

	/// <summary>
	/// 正式登録済み作品を削除します。
	/// 作品情報と関連データ、および選択した削除方法に応じて素材フォルダと正式サムネイルを削除します。
	/// </summary>
	/// <remarks>
	/// 削除処理の流れ：
	/// 1. 入力検証（SeriesId == 0 または WorkId != 0 の場合は例外）
	/// 2. deleteMethod が InfoAndFolder の場合は素材フォルダを削除
	/// 3. 正式サムネイルを削除
	/// 4. DB から MangaSeries を削除
	/// 5. MangaSeriesStore からも削除
	/// 
	/// 削除対象：
	/// - MaterialArchiveEntries, MaterialArchives, MangaSeriesTags, MangaSources, MangaSeries（DB側）
	/// - 素材フォルダ（deleteMethod が InfoAndFolder の場合）
	/// - 正式サムネイルファイル
	/// </remarks>
	/// <param name="series">削除対象の正式作品。</param>
	/// <param name="deleteMethod">削除方法（InfoOnly: 情報のみ削除、InfoAndFolder: 情報と素材フォルダ削除）。</param>
	/// <returns>完了を表す ValueTask。</returns>
	/// <exception cref="ArgumentException">series が新規作品（SeriesId == 0）または登録待ち作品（WorkId != 0）の場合にスローされます。</exception>
	/// <exception cref="ArgumentNullException">series が null の場合にスローされます。</exception>
	public async ValueTask DeleteExistingSeriesAsync(
		MangaSeries series,
		SeriesDeleteMethod deleteMethod)
	{
		ArgumentNullException.ThrowIfNull(series);

		// 正式作品のみが対象
		if (series.SeriesId == 0 || series.WorkId != 0)
		{
			throw new ArgumentException("削除対象は正式登録済み作品のみです。新規作品や登録待ち作品は削除できません。");
		}

		// Scope を生成して MaterialManager と ThumbnailManager を取得
		using var scope = this.serviceScopeFactory.CreateScope();
		var materialManager = scope.ServiceProvider.GetRequiredService<MaterialManager>();
		var thumbnailManager = scope.ServiceProvider.GetRequiredService<ThumbnailManager>();

		// deleteMethod が InfoAndFolder の場合は素材フォルダを削除
		if (deleteMethod == SeriesDeleteMethod.InfoAndFolder)
		{
			await materialManager.DeleteMaterialFoldersAsync(series.MaterialSources);
		}

		// 正式サムネイルを削除
		if (!string.IsNullOrEmpty(series.ThumbnailFileName))
		{
			await thumbnailManager.DeleteThumbnailIfExistsAsync(series.ThumbnailFileName);
		}

		// DB から MangaSeries を削除
		await this.mangaRepository.DeleteSeriesAsync(series.SeriesId);

		// MangaSeriesStore からも削除
		this.mangaSeriesStore.Remove(series.SeriesId);
	}

	/// <summary>
	/// 登録待ち作品（WorkMangaSeries）を削除します。
	/// 対象は WorkId で特定します。呼び出し元は、編集用クローンではなく Store の正本を渡してください。
	/// </summary>
	/// <remarks>
	/// 削除処理の流れ：
	/// 1. DB トランザクション内で WorkMangaSeriesTags、WorkMangaSeries を削除して Commit
	/// 2. Work サムネイルを削除
	/// 3. MangaSeriesStore から登録待ち作品を削除
	/// 登録待ち作品には素材フォルダ（MangaSources）がないため、素材フォルダの削除は行いません。
	/// </remarks>
	/// <param name="series">削除対象の登録待ち作品。</param>
	/// <returns>完了を表す ValueTask。</returns>
	/// <exception cref="ArgumentException">series が登録待ち作品（WorkId != 0 かつ SeriesId == 0）でない場合にスローされます。</exception>
	/// <exception cref="ArgumentNullException">series が null の場合にスローされます。</exception>
	public async ValueTask DeleteWorkSeriesAsync(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		if (series.WorkId == 0 || series.SeriesId != 0)
		{
			throw new ArgumentException("削除対象は登録待ち作品のみです。正式登録済み作品や新規作品は削除できません。");
		}

		var workId = series.WorkId;
		var workThumbnailFileName = $"{series.WorkThumbnailFileNameBase}.jpg";

		using (var connection = new SQLiteConnection(this.appSettings.ConnectionString))
		{
			await connection.OpenAsync();
			using var tx = connection.BeginTransaction();

			try
			{
				await this.workMangaSeriesRepository.DeleteWorkSeriesTagsByIdInTransactionAsync(connection, tx, workId);
				await this.workMangaSeriesRepository.DeleteWorkSeriesByIdInTransactionAsync(connection, tx, workId);
				tx.Commit();
			}
			catch (Exception ex)
			{
				this.logger?.LogError(ex, "登録待ち作品の削除に失敗しました。WorkId={WorkId}", workId);
				tx.Rollback();
				throw;
			}
		}

		// Commit 成功後に Work サムネイルを削除する
		using var scope = this.serviceScopeFactory.CreateScope();
		scope.ServiceProvider.GetRequiredService<ThumbnailManager>().DeleteWorkThumbnailIfExists(workThumbnailFileName);

		this.mangaSeriesStore.RemoveWorkSeries(workId);
	}

	/// <summary>
	/// 指定した SeriesId が BindingQueueDispatcher に登録されているかどうかを判定します。
	/// </summary>
	/// <param name="seriesId">判定対象の SeriesId。</param>
	/// <returns>SeriesId が製本待ちに登録されている場合は true、それ以外は false。</returns>
	public bool IsBindingQueued(long seriesId)
	{
		return this.bindingQueueDispatcher.Contains(seriesId);
	}

	/// <summary>
	/// 作品管理画面の検索を実行します。
	/// MangaSeriesStore.MaintenanceSearchWords を参照し、All + WorkSeries から検索した結果を MaintenanceDisplaySeries へ反映します。
	/// 検索文字列が空または空白のみの場合は登録待ち表示へ戻します。
	/// </summary>
	public void SearchMaintenanceSeries()
	{
		var searchWords = this.mangaSeriesStore.MaintenanceSearchWords.Value?.Trim() ?? string.Empty;

		if (string.IsNullOrEmpty(searchWords))
		{
			this.ShowMaintenanceWorkSeries();
			return;
		}

		var results = this.SearchMangaSeries(searchWords, MangaSeriesSearchTarget.AllAndWorkSeries);

		this.replaceMaintenanceDisplaySeries(results);
		this.mangaSeriesStore.IsMaintenanceSearchResultsShown.Value = true;
		this.mangaSeriesStore.NotifyMaintenanceSearchCompleted(results.Count);
	}

	/// <summary>
	/// 作品管理画面を登録待ち表示にします。
	/// 検索文字列をクリアし、MaintenanceDisplaySeries を現在の WorkSeries の内容で更新し、検索結果表示状態を解除します。
	/// 画面の初期表示・再表示時にも使用します。
	/// </summary>
	public void ShowMaintenanceWorkSeries()
	{
		this.mangaSeriesStore.MaintenanceSearchWords.Value = string.Empty;
		this.replaceMaintenanceDisplaySeries(this.mangaSeriesStore.WorkSeries);
		this.mangaSeriesStore.IsMaintenanceSearchResultsShown.Value = false;
	}

	/// <summary>
	/// MaintenanceDisplaySeries の内容を、共有 Notifier の指定一覧で置き換えます。
	/// </summary>
	/// <param name="notifiers">表示対象の共有 Notifier。</param>
	private void replaceMaintenanceDisplaySeries(IEnumerable<MangaSeriesNotifier> notifiers)
	{
		// 元リストを列挙中に Clear しないよう、先に materialize する
		var snapshot = notifiers.ToList();

		this.mangaSeriesStore.MaintenanceDisplaySeries.Clear();
		this.mangaSeriesStore.MaintenanceDisplaySeries.AddRange(snapshot);
	}

	/// <summary>
	/// 指定した検索文字列と検索対象に基づき、一般検索を実行します。
	/// 検索結果として MangaSeriesStore が所有している共有 MangaSeriesNotifier を返します。
	/// </summary>
	/// <param name="searchText">検索文字列。</param>
	/// <param name="target">検索対象。</param>
	/// <returns>検索条件に一致した MangaSeriesNotifier のリスト。</returns>
	public IReadOnlyList<MangaSeriesNotifier> SearchMangaSeries(string searchText, MangaSeriesSearchTarget target)
	{
		// 検索Matcher を生成
		var matcher = new MangaSeriesSearchMatcher(searchText);

		// 検索条件が無効な場合は空のリストを返す
		if (!matcher.IsValid)
			return new List<MangaSeriesNotifier>();

		switch (target)
		{
			case MangaSeriesSearchTarget.All:
				// MangaSeriesStore.All の中から一致した MangaSeriesNotifier を返す
				return this.mangaSeriesStore.All
					.Where(vm => matcher.IsMatch(vm.Series.Value))
					.ToList();

			case MangaSeriesSearchTarget.AllAndWorkSeries:
				// MangaSeriesStore.All + WorkSeries の中から一致した MangaSeriesNotifier を返す
				// 列挙順は All → WorkSeries の順を維持する
				return this.mangaSeriesStore.All
					.Concat(this.mangaSeriesStore.WorkSeries)
					.Where(vm => matcher.IsMatch(vm.Series.Value))
					.ToList();

			case MangaSeriesSearchTarget.BindingQueue:
				// BindingQueueStore.Queue に現在含まれている作品から一致した MangaSeriesNotifier を返す
				// BindingQueue の並び順を維持する
				var result = new List<MangaSeriesNotifier>();
				foreach (var bindingSeries in this.bindingQueueStore.Queue)
				{
					// BindingQueue の各作品の SeriesId に対応する MangaSeriesNotifier を All から取得
					var viewModel = this.mangaSeriesStore.All
						.FirstOrDefault(vm => vm.Series.Value.SeriesId == bindingSeries.Series.SeriesId);

					// All に対応する作品が見つかり、かつ検索条件に一致した場合のみ追加
					if (viewModel is not null && matcher.IsMatch(viewModel.Series.Value))
					{
						result.Add(viewModel);
					}
				}
				return result;

			default:
				// 予期しない値の場合は空のリストを返す
				return new List<MangaSeriesNotifier>();
		}
	}
}
