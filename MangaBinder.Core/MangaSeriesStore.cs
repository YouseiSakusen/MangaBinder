using ObservableCollections;
using R3;

namespace MangaBinder;

using MangaBinder.Series;
using MangaBinder.Tags;

/// <summary>
/// MangaSeries の正本リストを管理する Singleton ストアです。
/// アプリケーション全体で同一の MangaSeries インスタンスを参照するための中央集中管理を提供します。
/// タグマスタの正本は <see cref="TagStore"/> が管理し、このストアは作品の正本管理に専念します。
/// </summary>
public sealed class MangaSeriesStore : IDisposable
{
	/// <summary>タイトル比較用Comparer。NormalizedTitleInternal のソート・検索に使用します。</summary>
	private static readonly Comparer<string> titleComparer = Comparer<string>.Default;

	private DisposableBag disposableBag;
	private readonly TagStore tagStore;
	private readonly ObservableList<MangaSeriesNotifier> workSeriesNotifiers = new();
	private readonly ObservableList<MangaSeriesNotifier> allSeriesNotifiers = new();

	/// <summary>
	/// MangaSeriesManager による初期ロード一式が正常完了済みであるかを示す値を取得します。
	/// true：DB からの初期ロードと BindingQueue 復元がすべて正常完了済み。
	/// false：初期ロードがまだ実施されていない、または失敗状態。
	/// 作品件数が0件であることとの区別が重要です。
	/// </summary>
	public bool IsInitialized { get; private set; }

	/// <summary>
	/// 登録済み正式作品の件数を Reactive に取得します。
	/// MangaSeriesStore.All の件数変更に自動追従します。
	/// </summary>
	public BindableReactiveProperty<int> RegisteredSeriesCount { get; }

	/// <summary>
	/// 登録待ち作品の件数を Reactive に取得します。
	/// MangaSeriesStore.WorkSeries の件数変更に自動追従します。
	/// </summary>
	public BindableReactiveProperty<int> WorkSeriesCount { get; }

	/// <summary>
	/// 作品管理画面の検索ボックスの入力文字列を取得します。
	/// 複数ワードを含む入力文字列をそのまま保持します。
	/// </summary>
	public BindableReactiveProperty<string> MaintenanceSearchWords { get; }

	/// <summary>
	/// 作品管理画面に現在表示する作品の元リストを取得します。
	/// All / WorkSeries に存在する共有 Notifier を参照し、Manager が内容を更新します。
	/// </summary>
	public ObservableList<MangaSeriesNotifier> MaintenanceDisplaySeries { get; } = new();

	/// <summary>
	/// 作品管理画面で検索結果を表示中であるかを取得します。
	/// </summary>
	public BindableReactiveProperty<bool> IsMaintenanceSearchResultsShown { get; }

	private readonly Subject<int> maintenanceSearchCompleted = new();

	/// <summary>
	/// 作品管理画面の検索が完了するたびに、検索結果件数を通知します。
	/// 件数が前回と同じでも毎回発火します。
	/// </summary>
	public Observable<int> MaintenanceSearchCompleted => this.maintenanceSearchCompleted;

	/// <summary>
	/// 作品管理画面の検索完了を通知します。
	/// </summary>
	/// <param name="resultCount">検索結果件数。</param>
	public void NotifyMaintenanceSearchCompleted(int resultCount)
	{
		this.maintenanceSearchCompleted.OnNext(resultCount);
	}

	/// <summary>
	/// <see cref="MangaSeriesStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="tagStore">MangaSeriesNotifier が共有 TagNotifier を参照するためのタグストア。</param>
	public MangaSeriesStore(TagStore tagStore)
	{
		this.tagStore = tagStore;

		this.MaintenanceSearchWords = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.IsMaintenanceSearchResultsShown = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.maintenanceSearchCompleted.AddTo(ref this.disposableBag);

		// RegisteredSeriesCount を初期化
		this.RegisteredSeriesCount = new BindableReactiveProperty<int>(this.allSeriesNotifiers.Count)
			.AddTo(ref this.disposableBag);

		// All の件数変更を監視
		this.allSeriesNotifiers.ObserveCountChanged()
			.Subscribe(count =>
			{
				this.RegisteredSeriesCount.Value = count;
			})
			.AddTo(ref this.disposableBag);

		// WorkSeriesCount を初期化
		this.WorkSeriesCount = new BindableReactiveProperty<int>(this.workSeriesNotifiers.Count)
			.AddTo(ref this.disposableBag);

		// WorkSeries の件数変更を監視
		this.workSeriesNotifiers.ObserveCountChanged()
			.Subscribe(count =>
			{
				this.WorkSeriesCount.Value = count;
			})
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// Reactive な正式作品 ViewModel 一覧を取得します。
	/// 各 MangaSeriesNotifier が持つ Series プロパティは Store が所有する同一インスタンスを参照します。
	/// </summary>
	public ObservableList<MangaSeriesNotifier> All => this.allSeriesNotifiers;

	/// <summary>
	/// Reactive な登録待ち作品 ViewModel 一覧を取得します。
	/// 各 MangaSeriesNotifier が持つ Series プロパティは Store が所有する同一インスタンスを参照します。
	/// </summary>
	public ObservableList<MangaSeriesNotifier> WorkSeries => this.workSeriesNotifiers;

	/// <summary>
	/// MangaSeries の一覧を指定したリストで一括置換します。
	/// 自動的に Title → Author → SeriesId の昇順でソートされます。
	/// </summary>
	/// <param name="newSeries">新しい MangaSeries 一覧。</param>
	public void ReplaceAll(IEnumerable<MangaSeries> newSeries)
	{
		// 入力を先に materialize する（入力元の破壊を防ぐ）
		var sorted = newSeries
			.OrderBy(s => s.Title, titleComparer)
			.ThenBy(s => s.Author, titleComparer)
			.ThenBy(s => s.SeriesId)
			.ToList();

		// 現在の allViewModels を保持
		var oldNotifiers = this.allSeriesNotifiers.ToList();

		// 破棄予定の Notifier を参照しないよう、表示対象リストを先にクリア（画面表示時に Manager が再構築する）
		this.MaintenanceDisplaySeries.Clear();

		// allViewModels を Clear（派生 View 削除通知を先に流す）
		this.allSeriesNotifiers.Clear();

		// Clear によって HomeSeriesViewModelStore 等の派生 View がカード削除通知を受けてから、旧 ViewModel を Dispose
		foreach (var oldViewModel in oldNotifiers)
		{
			oldViewModel.Dispose();
		}

		// ソート済み MangaSeries を MangaSeriesNotifier でラップして allViewModels へ追加
		foreach (var mangaSeries in sorted)
		{
			var viewModel = new MangaSeriesNotifier(mangaSeries, this.tagStore);
			this.allSeriesNotifiers.Add(viewModel);
		}
	}

	/// <summary>
	/// 登録待ち作品の MangaSeries 一覧を指定したリストで一括置換します。
	/// Repository から取得した WorkId 昇順を維持したまま格納します。
	/// </summary>
	/// <param name="newWorkSeries">新しい登録待ち MangaSeries 一覧。</param>
	public void ReplaceWorkSeries(IEnumerable<MangaSeries> newWorkSeries)
	{
		// 入力を先に materialize する（入力元の破壊を防ぐ）
		var newWorkSeriesList = newWorkSeries.ToList();

		// 現在の workSeriesViewModels を保持
		var oldViewModels = this.workSeriesNotifiers.ToList();

		// 破棄予定の Notifier を参照しないよう、表示対象リストを先にクリア（画面表示時に Manager が再構築する）
		this.MaintenanceDisplaySeries.Clear();

		// workSeriesViewModels を Clear（派生 View 削除通知を先に流す）
		this.workSeriesNotifiers.Clear();

		// Clear によって MaintenanceSeriesViewModelStore 等の派生 Card がカード削除通知を受けてから、旧 ViewModel を Dispose
		foreach (var oldViewModel in oldViewModels)
		{
			oldViewModel.Dispose();
		}

		// Repository から取得した順序をそのまま維持して MangaSeriesNotifier でラップして追加
		foreach (var mangaSeries in newWorkSeriesList)
		{
			var viewModel = new MangaSeriesNotifier(mangaSeries, this.tagStore);
			this.workSeriesNotifiers.Add(viewModel);
		}
	}

	/// <summary>
	/// MangaSeriesManager による初期ロード一式が正常完了したことを設定します。
	/// このメソッドは、DB からの取得・BindingQueue 復元などすべての初期化処理が成功した後のみ呼ばれるべきです。
	/// 初期化に失敗した場合は呼ばないでください。次回の EnsureInitializedAsync() で再試行が可能となります。
	/// </summary>
	public void MarkInitialized()
	{
		this.IsInitialized = true;
	}

	/// <summary>
	/// 登録待ち作品として、指定された MangaSeries を更新します。
	/// 同じ WorkId を持つ登録待ち作品が既に存在する場合は、そのインスタンスのプロパティを更新します。
	/// 存在しない場合は新規追加します。
	/// WorkId が 0 の場合は受け付けません。
	/// </summary>
	/// <param name="workSeries">追加または更新する登録待ち MangaSeries。WorkId != 0 である必要があります。</param>
	public void UpdateWorkSeries(MangaSeries workSeries)
	{
		// WorkId == 0 の場合は受け付けない
		if (workSeries.WorkId == 0)
			return;

		// 既存の登録待ち作品を workSeriesViewModels から検索
		var existingNotifier = this.workSeriesNotifiers.FirstOrDefault(vm => vm.Series.Value.WorkId == workSeries.WorkId);

		if (existingNotifier is not null)
		{
			// 既存する場合は、ViewModel が保持する Series インスタンスのプロパティを更新（インスタンス差し替えない）
			var existing = existingNotifier.Series.Value;

			// WorkMangaSeries UPDATE 対象のプロパティをコピー
			existing.Title = workSeries.Title;
			existing.ThumbnailFileName = workSeries.ThumbnailFileName;
			existing.Author = workSeries.Author;
			existing.Description = workSeries.Description;
			existing.SeriesCompleted = workSeries.SeriesCompleted;
			existing.IsOwnedCompleted = workSeries.IsOwnedCompleted;
			existing.IsSourceMissing = workSeries.IsSourceMissing;
			existing.StartVolume = workSeries.StartVolume;
			existing.EndVolume = workSeries.EndVolume;
			existing.BoundEndVolume = workSeries.BoundEndVolume;
			existing.OwnedMaxVolume = workSeries.OwnedMaxVolume;
			existing.ThumbnailStatus = workSeries.ThumbnailStatus;
			existing.Publisher = workSeries.Publisher;
			existing.GoogleBooksImportStatus = workSeries.GoogleBooksImportStatus;
			existing.GoogleBooksImportedAt = workSeries.GoogleBooksImportedAt;
			existing.GoogleBooksImportMessage = workSeries.GoogleBooksImportMessage;
			existing.DescriptionSource = workSeries.DescriptionSource;
			existing.DescriptionSourceTitle = workSeries.DescriptionSourceTitle;
			existing.HasNestedArchive = workSeries.HasNestedArchive;
			existing.Memo = workSeries.Memo;
			existing.IsIncomplete = workSeries.IsIncomplete;

			// タグも更新（workSeries が existing と同一インスタンスの場合に備え、Clear 前に正本インスタンスへ解決して退避）
			var canonicalTags = workSeries.Tags
				.Select(tag => this.tagStore.GetTagNotifier(tag.TagId).Tag.Value)
				.ToList();

			existing.Tags.Clear();
			foreach (var tag in canonicalTags)
			{
				existing.Tags.Add(tag);
			}

			// 全プロパティ・Tags の更新が完了したので、共有 BindableReactiveProperty を ForceNotify
			existingNotifier.Series.ForceNotify();
		}
		else
		{
			// 存在しない場合は新規追加する
			var newNotifier = new MangaSeriesNotifier(workSeries, this.tagStore);
			this.workSeriesNotifiers.Add(newNotifier);
		}
	}

	/// <summary>
	/// 指定した MangaSeries を追加します。
	/// 同一 SeriesId は重複登録しません。
	/// 自動的に Title → Author → SeriesId の昇順が保たれます。
	/// </summary>
	/// <param name="item">追加する MangaSeries。</param>
	public void Add(MangaSeries item)
	{
		if (this.allSeriesNotifiers.Any(vm => vm.Series.Value.SeriesId == item.SeriesId))
		{
			return;
		}

		// Title → Author → SeriesId 昇順を保つため、挿入位置を検索
		var insertIndex = 0;
		for (var i = 0; i < this.allSeriesNotifiers.Count; i++)
		{
			var existingSeries = this.allSeriesNotifiers[i].Series.Value;
			var titleCompare = titleComparer.Compare(item.Title, existingSeries.Title);
			if (titleCompare < 0)
			{
				insertIndex = i;
				break;
			}
			else if (titleCompare == 0)
			{
				// Title が同じ場合は Author で比較
				var authorCompare = titleComparer.Compare(item.Author, existingSeries.Author);
				if (authorCompare < 0)
				{
					insertIndex = i;
					break;
				}
				else if (authorCompare == 0)
				{
					// Title と Author が同じ場合は SeriesId で比較
					if (item.SeriesId < existingSeries.SeriesId)
					{
						insertIndex = i;
						break;
					}
				}
			}
			insertIndex = i + 1;
		}

		// 新正本 All にのみ MangaSeriesNotifier を同じ位置へ追加
		var viewModel = new MangaSeriesNotifier(item, this.tagStore);
		this.allSeriesNotifiers.Insert(insertIndex, viewModel);
	}

	/// <summary>
	/// 指定した SeriesId で Reactive な MangaSeriesNotifier を検索します。
	/// 新しい All（allViewModels）から検索します。
	/// </summary>
	/// <param name="seriesId">検索する SeriesId。</param>
	/// <returns>見つかった場合は該当の MangaSeriesNotifier。見つからない場合は null。</returns>
	public MangaSeriesNotifier? FindNotifierById(long seriesId)
		=> this.allSeriesNotifiers.FirstOrDefault(vm => vm.Series.Value.SeriesId == seriesId);

	/// <summary>
	/// 指定した SeriesId の共有 MangaSeriesNotifier.Series に変更通知を流します。
	/// Store 正本 MangaSeries の更新が完全に完了したあと、1回だけ呼び出してください。
	/// </summary>
	/// <param name="seriesId">通知対象の SeriesId。</param>
	/// <exception cref="InvalidOperationException">対象の MangaSeriesNotifier が見つからない場合。</exception>
	public void NotifySeriesChanged(long seriesId)
	{
		var viewModel = this.FindNotifierById(seriesId);
		if (viewModel is null)
		{
			throw new InvalidOperationException($"SeriesId {seriesId} に対応する MangaSeriesNotifier が見つかりません。");
		}

		viewModel.Series.ForceNotify();
	}

	/// <summary>
	/// 指定した SeriesId の MangaSeries を削除します。
	/// </summary>
	/// <param name="seriesId">削除対象の SeriesId。</param>
	public void Remove(long seriesId)
	{
		var target = this.allSeriesNotifiers.FirstOrDefault(vm => vm.Series.Value.SeriesId == seriesId);
		if (target is not null)
		{
			// Collection から削除してから Dispose
			this.MaintenanceDisplaySeries.Remove(target);
			this.allSeriesNotifiers.Remove(target);
			target.Dispose();
		}
	}

	/// <summary>
	/// 指定した WorkId の登録待ち作品をストアから削除します。
	/// 正式作品一覧には影響を与えません。
	/// </summary>
	/// <param name="workId">削除対象の WorkId。</param>
	public void RemoveWorkSeries(int workId)
	{
		var target = this.workSeriesNotifiers.FirstOrDefault(vm => vm.Series.Value.WorkId == workId);
		if (target is not null)
		{
			// ライフサイクル順序：
			// 1. workSeriesViewModels から MangaSeriesNotifier を Remove
			// 2. WorkSeries の変更通知により MaintenanceSeriesViewModelStore の CreateView 側で対応する MaintenanceSeriesCardViewModel が削除・Disposeされる
			// 3. その後、削除した MangaSeriesNotifier 自体を Dispose
			this.MaintenanceDisplaySeries.Remove(target);
			this.workSeriesNotifiers.Remove(target);
			target.Dispose();
		}
	}

	/// <summary>
	/// 指定したタグ ID のタグを、正式作品・登録待ち作品のすべてから取り除きます。
	/// タグマスタの正本である <see cref="TagStore"/> は変更しません。
	/// 先にこのメソッドで作品から取り除いてから、<see cref="TagStore.RemoveTag(long)"/> を呼び出してください。
	/// </summary>
	/// <param name="tagId">取り除くタグの ID。</param>
	public void RemoveTagFromAllSeries(long tagId)
	{
		var allNotifiers = this.allSeriesNotifiers
			.Concat(this.workSeriesNotifiers)
			.ToList();

		foreach (var notifier in allNotifiers)
		{
			var series = notifier.Series.Value;
			var seriesTag = series.Tags.FirstOrDefault(t => t.TagId == tagId);
			if (seriesTag is not null)
				series.Tags.Remove(seriesTag);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}

}
