using MangaBinder.Tags;
using ObservableCollections;
using R3;

namespace MangaBinder;

/// <summary>
/// タグマスタの正本（<see cref="TagNotifier"/> 一覧）と、タグ変更された <see cref="MangaSeries"/> を保持する Singleton ストアです。
/// タグごとに1つの <see cref="TagNotifier"/> を所有し、各作品の <c>MangaSeriesNotifier.Tags</c> と同一インスタンスを共有します。
/// DB へのアクセスは行わず、Repository からの取得結果は Manager / Editor が反映します。
/// </summary>
public sealed class TagStore : IDisposable
{
	private readonly Dictionary<long, MangaSeries> dirtyMap = new();
	private readonly ObservableList<TagNotifier> tags = new();
	private DisposableBag disposableBag;

	/// <summary>
	/// タグマスタの正本コレクションを取得します。
	/// DisplayOrder 昇順、Name 昇順で並び、各要素の所有者はこのストアです。
	/// </summary>
	public ObservableList<TagNotifier> Tags => this.tags;

	/// <summary>登録タグ数を Reactive に取得します。<see cref="Tags"/> の件数変更に自動追従します。</summary>
	public BindableReactiveProperty<int> TagCount { get; }

	/// <summary>タグが0件かどうかを Reactive に取得します。</summary>
	public BindableReactiveProperty<bool> IsEmpty { get; }

	/// <summary>タグが1件以上あるかどうかを Reactive に取得します。</summary>
	public BindableReactiveProperty<bool> HasTags { get; }

	/// <summary>新規追加タグ名の入力値を取得します。</summary>
	public BindableReactiveProperty<string> NewTagName { get; }

	/// <summary>現在インライン編集中のタグを取得します。null の場合は編集中のタグがありません。</summary>
	public BindableReactiveProperty<TagNotifier?> EditingTag { get; }

	/// <summary>編集中のタグ名の入力値を取得します。</summary>
	public BindableReactiveProperty<string> EditingTagName { get; }

	/// <summary>
	/// <see cref="TagStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	public TagStore()
	{
		this.TagCount = new BindableReactiveProperty<int>(this.tags.Count)
			.AddTo(ref this.disposableBag);

		this.IsEmpty = new BindableReactiveProperty<bool>(this.tags.Count == 0)
			.AddTo(ref this.disposableBag);

		this.HasTags = new BindableReactiveProperty<bool>(this.tags.Count > 0)
			.AddTo(ref this.disposableBag);

		// 正本コレクションの件数変更から派生状態を更新
		this.tags.ObserveCountChanged()
			.Subscribe(count =>
			{
				this.TagCount.Value = count;
				this.IsEmpty.Value = count == 0;
				this.HasTags.Value = count > 0;
			})
			.AddTo(ref this.disposableBag);

		this.NewTagName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.EditingTag = new BindableReactiveProperty<TagNotifier?>(null)
			.AddTo(ref this.disposableBag);

		this.EditingTagName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// タグマスタを指定したタグ一覧で一括置換します。
	/// 初期ロード時に、作品の <c>MangaSeriesNotifier</c> を生成する前に呼び出してください。
	/// 旧 <see cref="TagNotifier"/> は一覧から取り除いた後に破棄されます。
	/// DisplayOrder 昇順、Name 昇順でソートされます。
	/// </summary>
	/// <param name="newTags">新しいタグ一覧。各 <see cref="MangaTag"/> が正本インスタンスとして保持されます。</param>
	public void ReplaceTags(IEnumerable<MangaTag> newTags)
	{
		var oldNotifiers = this.tags.ToList();

		this.EndEditing();
		this.tags.Clear();

		foreach (var oldNotifier in oldNotifiers)
		{
			oldNotifier.Dispose();
		}

		foreach (var tag in newTags)
		{
			this.tags.Add(new TagNotifier(tag));
		}

		this.sortTags();
	}

	/// <summary>
	/// タグマスタの <see cref="MangaTag"/> 一覧を取得します。
	/// 正本コレクションから都度スナップショットを生成して返し、別コレクションとしては保持しません。
	/// </summary>
	/// <returns>正本インスタンスの <see cref="MangaTag"/> 一覧。</returns>
	public IReadOnlyList<MangaTag> GetTags()
		=> this.tags.Select(x => x.Tag.Value).ToList();

	/// <summary>
	/// 指定したタグ ID の <see cref="TagNotifier"/> を検索します。
	/// </summary>
	/// <param name="tagId">検索するタグ ID。</param>
	/// <returns>見つかった場合は該当の <see cref="TagNotifier"/>、見つからない場合は null。</returns>
	public TagNotifier? FindTagNotifierById(long tagId)
		=> this.tags.FirstOrDefault(x => x.Tag.Value.TagId == tagId);

	/// <summary>
	/// 指定したタグ ID の <see cref="TagNotifier"/> を取得します。
	/// </summary>
	/// <param name="tagId">取得するタグ ID。</param>
	/// <returns>該当の <see cref="TagNotifier"/>。</returns>
	/// <exception cref="InvalidOperationException">タグマスタに存在しない場合。</exception>
	public TagNotifier GetTagNotifier(long tagId)
		=> this.FindTagNotifierById(tagId)
			?? throw new InvalidOperationException($"TagId {tagId} に対応する TagNotifier が TagStore に存在しません。");

	/// <summary>
	/// 指定したタグをタグマスタへ追加します。
	/// 渡された <see cref="MangaTag"/> が正本インスタンスとなります。
	/// DisplayOrder 昇順、Name 昇順が保たれます。
	/// </summary>
	/// <param name="tag">追加するタグ。</param>
	/// <exception cref="InvalidOperationException">同じ TagId のタグが既に存在する場合。</exception>
	public void AddTag(MangaTag tag)
	{
		if (this.FindTagNotifierById(tag.TagId) is not null)
		{
			throw new InvalidOperationException($"TagId {tag.TagId} のタグは既に TagStore に存在します。");
		}

		this.tags.Add(new TagNotifier(tag));
		this.sortTags();
	}

	/// <summary>
	/// 既存タグの内容を、指定したタグの内容で更新します。
	/// 正本の <see cref="MangaTag"/> インスタンスは置き換えず、同じインスタンスの値を更新して
	/// <see cref="TagNotifier.Tag"/> から変更を通知します。作品側のタグは置き換えません。
	/// 更新後、DisplayOrder 昇順、Name 昇順が保たれます。
	/// </summary>
	/// <param name="updated">更新内容を持つタグ。TagId で対象の正本を特定します。</param>
	/// <exception cref="InvalidOperationException">対象のタグが存在しない場合。</exception>
	public void UpdateTag(MangaTag updated)
	{
		var notifier = this.GetTagNotifier(updated.TagId);

		var canonical = notifier.Tag.Value;
		canonical.Name = updated.Name;
		canonical.DisplayOrder = updated.DisplayOrder;
		canonical.ShowOnSeriesCard = updated.ShowOnSeriesCard;

		notifier.Tag.ForceNotify();
		this.sortTags();
	}

	/// <summary>
	/// 指定したタグ ID のタグをタグマスタから削除し、<see cref="TagNotifier"/> を破棄します。
	/// 呼び出し元は、先にすべての作品から該当タグを取り除いておく必要があります。
	/// </summary>
	/// <param name="tagId">削除するタグの ID。</param>
	/// <exception cref="InvalidOperationException">対象のタグが存在しない場合。</exception>
	public void RemoveTag(long tagId)
	{
		var target = this.GetTagNotifier(tagId);

		// 編集中の行が削除対象の場合は編集状態を解除
		if (ReferenceEquals(this.EditingTag.Value, target))
		{
			this.EndEditing();
		}

		// 一覧から取り除いて View へ削除通知が流れた後で破棄する
		this.tags.Remove(target);
		target.Dispose();
	}

	/// <summary>
	/// 指定したタグのインライン編集を開始します。編集中のタグ名には現在のタグ名が設定されます。
	/// </summary>
	/// <param name="tag">編集対象のタグ。</param>
	public void BeginEditing(TagNotifier tag)
	{
		this.EditingTag.Value = tag;
		this.EditingTagName.Value = tag.Tag.Value.Name;
	}

	/// <summary>
	/// インライン編集を終了し、編集対象と編集中のタグ名をクリアします。
	/// </summary>
	public void EndEditing()
	{
		this.EditingTag.Value = null;
		this.EditingTagName.Value = string.Empty;
	}

	/// <summary>
	/// タグマスタを DisplayOrder 昇順、Name 昇順でソートします。
	/// 既存の <see cref="TagNotifier"/> インスタンスを維持したまま、位置が異なる要素だけを移動する差分方式です。
	/// </summary>
	private void sortTags()
	{
		var sorted = this.tags
			.OrderBy(x => x.Tag.Value.DisplayOrder)
			.ThenBy(x => x.Tag.Value.Name)
			.ToArray();

		for (var targetIndex = 0; targetIndex < sorted.Length; targetIndex++)
		{
			var currentIndex = this.tags.IndexOf(sorted[targetIndex]);

			if (currentIndex == targetIndex)
				continue;

			this.tags.Move(currentIndex, targetIndex);
		}
	}

	/// <summary>
	/// 指定した作品をタグ変更済みとしてマークします。
	/// 同一 SeriesId は重複登録しません。
	/// </summary>
	/// <param name="series">変更された作品。</param>
	public void MarkDirty(MangaSeries series)
		=> this.dirtyMap[series.SeriesId] = series;

	/// <summary>保存対象の <see cref="MangaSeries"/> 一覧を返します。</summary>
	public IReadOnlyCollection<MangaSeries> GetDirtyItems()
		=> this.dirtyMap.Values;

	/// <summary>変更がある場合 true を返します。</summary>
	public bool HasChanges => this.dirtyMap.Count > 0;

	/// <summary>ストアをクリアします。</summary>
	public void Clear()
		=> this.dirtyMap.Clear();

	/// <summary>
	/// 所有している <see cref="TagNotifier"/> と Reactive プロパティを破棄します。
	/// </summary>
	public void Dispose()
	{
		foreach (var notifier in this.tags)
		{
			notifier.Dispose();
		}

		this.disposableBag.Dispose();
	}
}
