using ObservableCollections;

namespace MangaBinder;

using MangaBinder.Tags;

/// <summary>
/// MangaSeries の正本リストを管理する Singleton ストアです。
/// アプリケーション全体で同一の MangaSeries インスタンスを参照するための中央集中管理を提供します。
/// また、タグマスタも保持します。
/// </summary>
public sealed class MangaSeriesStore
{
	private readonly ObservableList<MangaSeries> series = new();
	private readonly ObservableList<MangaSeries> workSeries = new();
	private readonly ObservableList<MangaSeries> mergedSeries = new();
	private readonly List<MangaTag> tags = new();

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
		var sorted = newSeries.OrderBy(s => s.NormalizedTitleInternal).ToList();
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
		var sorted = newWorkSeries.OrderBy(s => s.NormalizedTitleInternal).ToList();
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
		if (this.series.Any(x => x.SeriesId == item.SeriesId))
			return;

		// NormalizedTitleInternal 昇順を保つため、挿入位置を検索
		var insertIndex = 0;
		for (var i = 0; i < this.series.Count; i++)
		{
			if (string.Compare(item.NormalizedTitleInternal, this.series[i].NormalizedTitleInternal, StringComparison.Ordinal) < 0)
			{
				insertIndex = i;
				break;
			}
			insertIndex = i + 1;
		}

		this.series.Insert(insertIndex, item);
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
	/// 全てのタグを取得します。
	/// </summary>
	/// <returns>タグの読み取り専用リスト。</returns>
	public IReadOnlyList<MangaTag> GetTags()
		=> this.tags.AsReadOnly();

	/// <summary>
	/// タグの一覧を指定したリストで一括置換します。
	/// </summary>
	/// <param name="newTags">新しいタグ一覧。</param>
	public void ReplaceTags(IEnumerable<MangaTag> newTags)
	{
		this.tags.Clear();
		this.tags.AddRange(newTags);
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
	/// </summary>
	/// <param name="tag">追加するタグ。</param>
	public void AddTag(MangaTag tag)
	{
		if (this.tags.Any(x => x.TagId == tag.TagId))
			return;

		this.tags.Add(tag);
	}

	/// <summary>
	/// 指定したタグをストア内で更新します。同じ TagId を持つタグを置き換えます。
	/// </summary>
	/// <param name="tag">更新するタグ。</param>
	public void UpdateTag(MangaTag tag)
	{
		var index = this.tags.FindIndex(x => x.TagId == tag.TagId);
		if (index >= 0)
			this.tags[index] = tag;
	}

	/// <summary>
	/// 指定したタグ ID をストアから削除します。
	/// </summary>
	/// <param name="tagId">削除するタグの ID。</param>
	public void RemoveTag(long tagId)
	{
		var target = this.tags.FirstOrDefault(x => x.TagId == tagId);
		if (target is not null)
			this.tags.Remove(target);
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
}
