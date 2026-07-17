using Microsoft.Extensions.Logging;
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

	private readonly ILogger<MangaSeriesStore> logger;
	private readonly ObservableList<MangaSeries> series = new();
	private readonly ObservableList<MangaSeries> workSeries = new();
	private readonly ObservableList<MangaSeries> mergedSeries = new();
	private readonly ObservableList<MangaTag> tags = new();

	/// <summary>
	/// <see cref="MangaSeriesStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="logger">ログを出力するロガー。</param>
	public MangaSeriesStore(ILogger<MangaSeriesStore> logger)
	{
		this.logger = logger;
	}

	/// <summary>
	/// 正式作品一覧を取得します。
	/// </summary>
	public ObservableList<MangaSeries> All => this.series;

	/// <summary>
	/// 全ての MangaSeries を取得します。
	/// </summary>
	/// <returns>MangaSeries の読み取り専用リスト。</returns>
	public IReadOnlyList<MangaSeries> GetAll()
		=> this.series.AsReadOnly();

	/// <summary>
	/// 登録待ち作品一覧を取得します。
	/// </summary>
	public ObservableList<MangaSeries> WorkSeries => this.workSeries;

	/// <summary>
	/// 統合作品一覧（正式作品＋登録待ち作品）を取得します。
	/// </summary>
	public ObservableList<MangaSeries> Merged => this.mergedSeries;

	/// <summary>
	/// 検索用に統合した MangaSeries 一覧を取得します。
	/// 正式作品と登録待ち作品をまとめた一覧を返します。
	/// </summary>
	/// <returns>統合 MangaSeries の読み取り専用リスト。</returns>
	public IReadOnlyList<MangaSeries> GetMergedSeries()
		=> this.mergedSeries.AsReadOnly();

	/// <summary>
	/// 登録待ち作品の MangaSeries 一覧を取得します。
	/// 作品管理画面の通常表示用。登録待ち一覧の正本を返します。
	/// </summary>
	/// <returns>登録待ち作品 MangaSeries の読み取り専用リスト。</returns>
	public IReadOnlyList<MangaSeries> GetWorkSeries()
		=> this.workSeries.AsReadOnly();

	/// <summary>
	/// MangaSeries の一覧を指定したリストで一括置換します。
	/// 自動的に NormalizedTitleInternal 昇順でソートされます。
	/// </summary>
	/// <param name="newSeries">新しい MangaSeries 一覧。</param>
	public void ReplaceAll(IEnumerable<MangaSeries> newSeries)
	{
		this.series.Clear();
		var sorted = newSeries.OrderBy(s => s.NormalizedTitleInternal, titleComparer).ToList();
		this.series.AddRange(sorted);
		this.RebuildMergedSeries();
	}

	/// <summary>
	/// 登録待ち作品の MangaSeries 一覧を指定したリストで一括置換します。
	/// </summary>
	/// <param name="newWorkSeries">新しい登録待ち MangaSeries 一覧。</param>
	public void ReplaceWorkSeries(IEnumerable<MangaSeries> newWorkSeries)
	{
		this.workSeries.Clear();
		var sorted = newWorkSeries.OrderBy(s => s.NormalizedTitleInternal, titleComparer).ToList();
		this.workSeries.AddRange(sorted);
		this.RebuildMergedSeries();
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

		// 既存の登録待ち作品を検索
		var existing = this.workSeries.FirstOrDefault(x => x.WorkId == workSeries.WorkId);

		if (existing is not null)
		{
			// 既存する場合は、そのインスタンスのプロパティを更新（差し替えない）
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

			// タグも更新
			existing.Tags.Clear();
			foreach (var tag in workSeries.Tags)
			{
				existing.Tags.Add(tag);
			}
		}
		else
		{
			// 存在しない場合は追加する
			this.workSeries.Add(workSeries);
		}

		// mergedSeries を再構築
		this.RebuildMergedSeries();
	}

	/// <summary>
	/// 指定した MangaSeries を追加します。
	/// 同一 SeriesId は重複登録しません。
	/// 自動的に NormalizedTitleInternal 昇順が保たれます。
	/// </summary>
	/// <param name="item">追加する MangaSeries。</param>
	public void Add(MangaSeries item)
	{
		// [NewSeriesHomeSync] Store.Add開始ログ
		if (NewSeriesHomeSyncTrace.IsTracking(item.SeriesId))
		{
			this.logger.LogInformation(
				"[NewSeriesHomeSync] Store.Add開始 SeriesId={SeriesId} Title={Title} NormalizedTitleInternal={NormalizedTitleInternal} 追加前Store件数={Count}",
				item.SeriesId, item.Title, item.NormalizedTitleInternal, this.series.Count);
		}

		if (this.series.Any(x => x.SeriesId == item.SeriesId))
		{
			// [NewSeriesHomeSync] Store.Addスキップログ
			if (NewSeriesHomeSyncTrace.IsTracking(item.SeriesId))
			{
				this.logger.LogWarning(
					"[NewSeriesHomeSync] Store.Addスキップ Reason=SameSeriesId SeriesId={SeriesId} Title={Title} NormalizedTitleInternal={NormalizedTitleInternal} 現在Store件数={Count}",
					item.SeriesId, item.Title, item.NormalizedTitleInternal, this.series.Count);
			}
			return;
		}

		// NormalizedTitleInternal 昇順を保つため、挿入位置を検索
		var insertIndex = 0;
		for (var i = 0; i < this.series.Count; i++)
		{
			if (titleComparer.Compare(item.NormalizedTitleInternal, this.series[i].NormalizedTitleInternal) < 0)
			{
				insertIndex = i;
				break;
			}
			insertIndex = i + 1;
		}

		// [NewSeriesHomeSync] Store挿入位置決定ログ
		if (NewSeriesHomeSyncTrace.IsTracking(item.SeriesId))
		{
			var previousSeries = insertIndex > 0 ? this.series[insertIndex - 1] : null;
			var nextSeries = insertIndex < this.series.Count ? this.series[insertIndex] : null;

			this.logger.LogInformation(
				"[NewSeriesHomeSync] Store挿入位置決定 SeriesId={SeriesId} Title={Title} NormalizedTitleInternal={NormalizedTitleInternal} InsertIndex={InsertIndex} 追加前Store件数={Count} PreviousSeriesId={PreviousSeriesId} PreviousTitle={PreviousTitle} PreviousNormalizedTitleInternal={PreviousNormalizedTitleInternal} NextSeriesId={NextSeriesId} NextTitle={NextTitle} NextNormalizedTitleInternal={NextNormalizedTitleInternal}",
				item.SeriesId, item.Title, item.NormalizedTitleInternal, insertIndex, this.series.Count,
				previousSeries?.SeriesId ?? 0, previousSeries?.Title ?? "<none>", previousSeries?.NormalizedTitleInternal ?? "<none>",
				nextSeries?.SeriesId ?? 0, nextSeries?.Title ?? "<none>", nextSeries?.NormalizedTitleInternal ?? "<none>");
		}

		this.series.Insert(insertIndex, item);

		// [NewSeriesHomeSync] Store.Insert完了ログ
		if (NewSeriesHomeSyncTrace.IsTracking(item.SeriesId))
		{
			this.logger.LogInformation(
				"[NewSeriesHomeSync] Store.Insert完了 SeriesId={SeriesId} Title={Title} NormalizedTitleInternal={NormalizedTitleInternal} InsertIndex={InsertIndex} 追加後Store件数={Count}",
				item.SeriesId, item.Title, item.NormalizedTitleInternal, insertIndex, this.series.Count);
		}

		this.RebuildMergedSeries();
	}

	/// <summary>
	/// 指定した SeriesId で MangaSeries を検索します。
	/// </summary>
	/// <param name="seriesId">検索する SeriesId。</param>
	/// <returns>見つかった場合は該当の MangaSeries、見つからない場合は null。</returns>
	public MangaSeries? FindById(long seriesId)
		=> this.series.FirstOrDefault(x => x.SeriesId == seriesId);

	/// <summary>
	/// 指定した SeriesId の MangaSeries を削除します。
	/// </summary>
	/// <param name="seriesId">削除対象の SeriesId。</param>
	public void Remove(long seriesId)
	{
		var target = this.series.FirstOrDefault(x => x.SeriesId == seriesId);
		if (target is not null)
		{
			this.series.Remove(target);
			this.RebuildMergedSeries();
		}
	}

	/// <summary>
	/// ストア内の全ての MangaSeries をクリアします。
	/// </summary>
	public void Clear()
	{
		this.series.Clear();
		this.RebuildMergedSeries();
	}

	/// <summary>
	/// 指定した WorkId の登録待ち作品をストアから削除します。
	/// 同時に MergedSeries を再構築します。
	/// 正式作品一覧には影響を与えません。
	/// </summary>
	/// <param name="workId">削除対象の WorkId。</param>
	public void RemoveWorkSeries(int workId)
	{
		var target = this.workSeries.FirstOrDefault(x => x.WorkId == workId);
		if (target is not null)
		{
			this.workSeries.Remove(target);
			this.RebuildMergedSeries();
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
	/// ストア内の全てのタグをクリアします。
	/// </summary>
	public void ClearTags()
		=> this.tags.Clear();

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

			// 各作品からも該当タグを削除（作品一覧をスナップショット化）
			var allSeries = this.series
				.Concat(this.workSeries)
				.ToList();

			foreach (var series in allSeries)
			{
				var seriesTag = series.Tags.FirstOrDefault(t => t.TagId == tagId);
				if (seriesTag is not null)
					series.Tags.Remove(seriesTag);
			}
		}
	}

	/// <summary>
	/// 正式作品と登録待ち作品をまとめた mergedSeries を再構築します。
	/// ReplaceAll() と ReplaceWorkSeries() から呼び出されます。
	/// </summary>
	private void RebuildMergedSeries()
	{
		this.mergedSeries.Clear();
		this.mergedSeries.AddRange(this.series);
		this.mergedSeries.AddRange(this.workSeries);
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
		// 正式作品と登録待ち作品をスナップショット化して走査（MergedSeries は派生一覧なので走査しない）
		var allSeries = this.series.Concat(this.workSeries).ToList();

		foreach (var series in allSeries)
		{
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
		// 正式作品と登録待ち作品をスナップショット化して走査
		var allSeries = this.series.Concat(this.workSeries).ToList();

		var storeTag = this.tags.FirstOrDefault(t => t.TagId == tagId);
		if (storeTag is null)
		{
			// Store に存在しないタグは全作品から削除
			foreach (var series in allSeries)
			{
				var toRemove = series.Tags.FirstOrDefault(t => t.TagId == tagId);
				if (toRemove is not null)
					series.Tags.Remove(toRemove);
			}
		}
		else
		{
			// Store のタグで全作品を同期
			foreach (var series in allSeries)
			{
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
