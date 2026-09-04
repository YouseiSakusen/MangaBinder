using ObservableCollections;

namespace MangaBinder;

using MangaBinder.Series;
using MangaBinder.Tags;

/// <summary>
/// MangaSeries の正本リストを管理する Singleton ストアです。
/// アプリケーション全体で同一の MangaSeries インスタンスを参照するための中央集中管理を提供します。
/// また、タグマスタも保持します。
/// </summary>
public sealed class MangaSeriesStore
{
	/// <summary>タイトル比較用Comparer。NormalizedTitleInternal のソート・検索に使用します。</summary>
	private static readonly Comparer<string> titleComparer = Comparer<string>.Default;

	private readonly ObservableList<MangaTag> tags = new();
	private readonly ObservableList<MangaSeriesViewModel> workSeriesViewModels = new();
	private readonly ObservableList<MangaSeriesViewModel> allViewModels = new();

	/// <summary>
	/// <see cref="MangaSeriesStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	public MangaSeriesStore()
	{
	}

	/// <summary>
	/// Reactive な正式作品 ViewModel 一覧を取得します。
	/// 各 MangaSeriesViewModel が持つ Series プロパティは Store が所有する同一インスタンスを参照します。
	/// </summary>
	public ObservableList<MangaSeriesViewModel> All => this.allViewModels;

	/// <summary>
	/// Reactive な登録待ち作品 ViewModel 一覧を取得します。
	/// 各 MangaSeriesViewModel が持つ Series プロパティは Store が所有する同一インスタンスを参照します。
	/// </summary>
	public ObservableList<MangaSeriesViewModel> WorkSeries => this.workSeriesViewModels;

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
		var oldViewModels = this.allViewModels.ToList();

		// allViewModels を Clear（派生 View 削除通知を先に流す）
		this.allViewModels.Clear();

		// Clear によって HomeSeriesStore 等の派生 View がカード削除通知を受けてから、旧 ViewModel を Dispose
		foreach (var oldViewModel in oldViewModels)
		{
			oldViewModel.Dispose();
		}

		// ソート済み MangaSeries を MangaSeriesViewModel でラップして allViewModels へ追加
		foreach (var mangaSeries in sorted)
		{
			var viewModel = new MangaSeriesViewModel(mangaSeries);
			this.allViewModels.Add(viewModel);
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
		var oldViewModels = this.workSeriesViewModels.ToList();

		// workSeriesViewModels を Clear（派生 View 削除通知を先に流す）
		this.workSeriesViewModels.Clear();

		// Clear によって MaintenanceSeriesStore 等の派生 Card がカード削除通知を受けてから、旧 ViewModel を Dispose
		foreach (var oldViewModel in oldViewModels)
		{
			oldViewModel.Dispose();
		}

		// Repository から取得した順序をそのまま維持して MangaSeriesViewModel でラップして追加
		foreach (var mangaSeries in newWorkSeriesList)
		{
			var viewModel = new MangaSeriesViewModel(mangaSeries);
			this.workSeriesViewModels.Add(viewModel);
		}
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
		var existingViewModel = this.workSeriesViewModels.FirstOrDefault(vm => vm.Series.Value.WorkId == workSeries.WorkId);

		if (existingViewModel is not null)
		{
			// 既存する場合は、ViewModel が保持する Series インスタンスのプロパティを更新（インスタンス差し替えない）
			var existing = existingViewModel.Series.Value;

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

			// タグも更新
			existing.Tags.Clear();
			foreach (var tag in workSeries.Tags)
			{
				existing.Tags.Add(tag);
			}

			// 全プロパティ・Tags の更新が完了したので、共有 BindableReactiveProperty を ForceNotify
			existingViewModel.Series.ForceNotify();
		}
		else
		{
			// 存在しない場合は新規追加する
			var newViewModel = new MangaSeriesViewModel(workSeries);
			this.workSeriesViewModels.Add(newViewModel);
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
		if (this.allViewModels.Any(vm => vm.Series.Value.SeriesId == item.SeriesId))
		{
			return;
		}

		// Title → Author → SeriesId 昇順を保つため、挿入位置を検索
		var insertIndex = 0;
		for (var i = 0; i < this.allViewModels.Count; i++)
		{
			var existingSeries = this.allViewModels[i].Series.Value;
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

		// 新正本 All にのみ MangaSeriesViewModel を同じ位置へ追加
		var viewModel = new MangaSeriesViewModel(item);
		this.allViewModels.Insert(insertIndex, viewModel);
	}

	/// <summary>
	/// 指定した SeriesId で Reactive な MangaSeriesViewModel を検索します。
	/// 新しい All（allViewModels）から検索します。
	/// </summary>
	/// <param name="seriesId">検索する SeriesId。</param>
	/// <returns>見つかった場合は該当の MangaSeriesViewModel。見つからない場合は null。</returns>
	public MangaSeriesViewModel? FindViewModelById(long seriesId)
		=> this.allViewModels.FirstOrDefault(vm => vm.Series.Value.SeriesId == seriesId);

	/// <summary>
	/// 指定した SeriesId の共有 MangaSeriesViewModel.Series に変更通知を流します。
	/// Store 正本 MangaSeries の更新が完全に完了したあと、1回だけ呼び出してください。
	/// </summary>
	/// <param name="seriesId">通知対象の SeriesId。</param>
	/// <exception cref="InvalidOperationException">対象の MangaSeriesViewModel が見つからない場合。</exception>
	public void NotifySeriesChanged(long seriesId)
	{
		var viewModel = this.FindViewModelById(seriesId);
		if (viewModel is null)
		{
			throw new InvalidOperationException($"SeriesId {seriesId} に対応する MangaSeriesViewModel が見つかりません。");
		}

		viewModel.Series.ForceNotify();
	}

	/// <summary>
	/// 指定した SeriesId の MangaSeries を削除します。
	/// </summary>
	/// <param name="seriesId">削除対象の SeriesId。</param>
	public void Remove(long seriesId)
	{
		var target = this.allViewModels.FirstOrDefault(vm => vm.Series.Value.SeriesId == seriesId);
		if (target is not null)
		{
			// Collection から削除してから Dispose
			this.allViewModels.Remove(target);
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
		var target = this.workSeriesViewModels.FirstOrDefault(vm => vm.Series.Value.WorkId == workId);
		if (target is not null)
		{
			// ライフサイクル順序：
			// 1. workSeriesViewModels から MangaSeriesViewModel を Remove
			// 2. WorkSeries の変更通知により MaintenanceSeriesStore の CreateView 側で対応する MaintenanceSeriesCardViewModel が削除・Disposeされる
			// 3. その後、削除した MangaSeriesViewModel 自体を Dispose
			this.workSeriesViewModels.Remove(target);
			target.Dispose();
		}
	}

	/// <summary>
	/// 全てのタグを取得します。
	/// </summary>
	public IReadOnlyList<MangaTag> GetTags()
		=> this.tags.AsReadOnly();

	/// <summary>
	/// タグ一覧の ObservableList を取得します。
	/// 監視・バインディング用に使用されます。
	/// </summary>
	public ObservableList<MangaTag> Tags => this.tags;

	/// <summary>
	/// タグの一覧を指定したリストで一括置換します。
	/// 自動的に DisplayOrder 昇順、Name 昇順でソートされます。
	/// 正式作品・登録待ち作品のタグが Store の正本インスタンスと同期されます。
	/// </summary>
	/// <param name="newTags">新しいタグ一覧。</param>
	public void ReplaceTags(IEnumerable<MangaTag> newTags)
	{
		this.tags.Clear();
		this.tags.AddRange(newTags);
		this.SortTags();
		this.SynchronizeSeriesTagInstances();
	}

	/// <summary>
	/// 指定したタグ ID でタグを検索します。
	/// </summary>
	/// <param name="tagId">検索するタグ ID。</param>
	/// <returns>見つかった場合は該当のタグ、見つからない場合は null。</returns>
	public MangaTag? FindTagById(long tagId)
		=> this.tags.FirstOrDefault(x => x.TagId == tagId);

	/// <summary>
	/// 指定したタグをストアに追加します。
	/// 自動的に DisplayOrder 昇順、Name 昇順が保たれます。
	/// </summary>
	/// <param name="tag">追加するタグ。</param>
	public void AddTag(MangaTag tag)
	{
		if (this.tags.Any(x => x.TagId == tag.TagId))
			return;

		this.tags.Add(tag);
		this.SortTags();
	}

	/// <summary>
	/// 指定したタグをストア内で更新します。同じ TagId を持つタグを置き換えます。
	/// 更新後、自動的に DisplayOrder 昇順、Name 昇順が保たれます。
	/// 同時に、正式作品・登録待ち作品が同じ TagId のタグを保持している場合は、新しい正本インスタンスへ置き換えます。
	/// </summary>
	/// <param name="tag">更新するタグ。</param>
	public void UpdateTag(MangaTag tag)
	{
		var existing = this.tags.FirstOrDefault(x => x.TagId == tag.TagId);
		if (existing is not null)
		{
			var index = this.tags.IndexOf(existing);
			if (index >= 0)
			{
				this.tags[index] = tag;
				this.SortTags();

				// 更新されたタグのみを対象に各作品へ同期
				this.SynchronizeSeriesTagForId(tag.TagId);
			}
		}
	}

	/// <summary>
	/// 指定したタグ ID をストアから削除します。
	/// 同時に、正式作品・登録待ち作品が同じ TagId のタグを保持している場合は、それらから削除します。
	/// </summary>
	/// <param name="tagId">削除するタグの ID。</param>
	public void RemoveTag(long tagId)
	{
		var target = this.tags.FirstOrDefault(x => x.TagId == tagId);
		if (target is not null)
		{
			this.tags.Remove(target);

			// 各作品からも該当タグを削除（新 All + WorkSeries の共有 ViewModel から MangaSeries を取得）
			var allSeriesViewModels = this.allViewModels
				.Concat(this.workSeriesViewModels)
				.ToList();

			foreach (var viewModel in allSeriesViewModels)
			{
				var series = viewModel.Series.Value;
				var seriesTag = series.Tags.FirstOrDefault(t => t.TagId == tagId);
				if (seriesTag is not null)
					series.Tags.Remove(seriesTag);
			}
		}
	}

	/// <summary>
	/// タグ一覧を DisplayOrder 昇順、Name 昇順でソートします。
	/// ReplaceTags、AddTag、UpdateTag の後に呼び出され、タグの並び順を保証します。
	/// 既存の MangaTag インスタンスを維持したまま、必要な項目だけを移動する差分方式を使用します。
	/// </summary>
	private void SortTags()
	{
		var sorted = this.tags
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Name)
			.ToArray();

		// 正しい順序と現在の順序を比較し、位置が異なるタグだけを移動
		for (var targetIndex = 0; targetIndex < sorted.Length; targetIndex++)
		{
			var targetTag = sorted[targetIndex];
			var currentIndex = this.tags.IndexOf(targetTag);

			if (currentIndex < 0 || currentIndex == targetIndex)
				continue;

			// 位置が異なるタグだけを移動
			this.tags.RemoveAt(currentIndex);
			this.tags.Insert(targetIndex, targetTag);
		}
	}

	/// <summary>
	/// 正式作品・登録待ち作品が保持するタグを Store のタグマスタ正本インスタンスと同期します。
	/// 各作品の Tags 内のタグについて、Store.Tags に同じ TagId が存在する場合は正本インスタンスへ置き換えます。
	/// Store に存在しないタグは作品の Tags から削除します。
	/// </summary>
	private void SynchronizeSeriesTagInstances()
	{
		// 新 All + WorkSeries の共有 ViewModel から MangaSeries を取得
		var allSeriesViewModels = this.allViewModels.Concat(this.workSeriesViewModels).ToList();

		foreach (var viewModel in allSeriesViewModels)
		{
			var series = viewModel.Series.Value;

			// 逆順 for ループで Tags を処理（置換・削除時の列挙例外回避）
			for (var i = series.Tags.Count - 1; i >= 0; i--)
			{
				var seriesTag = series.Tags[i];

				// Store 内に同じ TagId のタグが存在するか検索
				var storeTag = this.tags.FirstOrDefault(t => t.TagId == seriesTag.TagId);

				if (storeTag is not null)
				{
					// Store のタグが見つかった場合は、そのインスタンスへ置き換え
					if (!ReferenceEquals(series.Tags[i], storeTag))
					{
						series.Tags[i] = storeTag;
					}
				}
				else
				{
					// Store に存在しないタグは削除
					series.Tags.RemoveAt(i);
				}
			}
		}
	}

	/// <summary>
	/// 指定した TagId を保持する作品のタグを Store の正本インスタンスと同期します。
	/// UpdateTag のように単一タグの更新後に使用される軽量同期です。
	/// </summary>
	/// <param name="tagId">同期対象のタグ ID。</param>
	private void SynchronizeSeriesTagForId(long tagId)
	{
		// 新 All + WorkSeries の共有 ViewModel から MangaSeries を取得
		var allSeriesViewModels = this.allViewModels.Concat(this.workSeriesViewModels).ToList();

		var storeTag = this.tags.FirstOrDefault(t => t.TagId == tagId);
		if (storeTag is null)
		{
			// Store に存在しないタグは全作品から削除
			foreach (var viewModel in allSeriesViewModels)
			{
				var series = viewModel.Series.Value;
				var toRemove = series.Tags.FirstOrDefault(t => t.TagId == tagId);
				if (toRemove is not null)
					series.Tags.Remove(toRemove);
			}
		}
		else
		{
			// Store のタグで全作品を同期
			foreach (var viewModel in allSeriesViewModels)
			{
				var series = viewModel.Series.Value;
				var seriesTag = series.Tags.FirstOrDefault(t => t.TagId == tagId);
				if (seriesTag is not null && !ReferenceEquals(seriesTag, storeTag))
				{
					var index = series.Tags.IndexOf(seriesTag);
					if (index >= 0)
						series.Tags[index] = storeTag;
				}
			}
		}
	}

}
