namespace MangaBinder.Tags;

/// <summary>
/// タグ追加・削除・リネームに関するルールを集約するクラスです。
/// DB 更新成功後にタグマスタの正本である <see cref="TagStore"/> へ反映します。
/// タグ削除時は、作品からのタグ除去を <see cref="MangaSeriesStore"/> へ依頼します。
/// </summary>
public sealed class TagEditor
{
	private readonly TagRepository repository;
	private readonly TagStore tagStore;
	private readonly MangaSeriesStore seriesStore;

	/// <summary>
	/// <see cref="TagEditor"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="repository">タグリポジトリ。</param>
	/// <param name="tagStore">タグマスタの正本ストア。</param>
	/// <param name="seriesStore">MangaSeries 正本ストア。タグ削除時に作品からタグを取り除くために使用します。</param>
	public TagEditor(TagRepository repository, TagStore tagStore, MangaSeriesStore seriesStore)
	{
		this.repository = repository;
		this.tagStore = tagStore;
		this.seriesStore = seriesStore;
	}

	/// <summary>
	/// 指定した名前のタグを追加します。
	/// DB に即座に挿入され、採番済み TagId を持つタグが返されます。
	/// </summary>
	/// <param name="name">追加するタグ名。</param>
	/// <returns>追加結果。成功時は採番済み MangaTag を含みます。</returns>
	public TagAddResult Add(string name)
	{
		var trimmed = name.Trim();

		if (string.IsNullOrEmpty(trimmed))
			return TagAddResult.Failure(TagAddFailureReason.EmptyName);

		// TagStore のタグ一覧から重複チェック
		var isDuplicate = this.tagStore.GetTags()
			.Any(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));

		if (isDuplicate)
			return TagAddResult.Failure(TagAddFailureReason.Duplicate);

		// DisplayOrder を計算（現在のタグ数が次の順序）
		var nextOrder = this.tagStore.Tags.Count;

		// DB に挿入、採番済みタグを取得
		var addedTag = this.repository.Insert(trimmed, nextOrder, showOnSeriesCard: true);

		// TagStore へ追加
		this.tagStore.AddTag(addedTag);

		return TagAddResult.Success(addedTag);
	}

	/// <summary>
	/// 指定した ID のタグを削除します。
	/// </summary>
	/// <param name="tagId">削除するタグの ID。</param>
	public void Delete(long tagId)
	{
		var exists = this.tagStore.FindTagNotifierById(tagId) is not null;
		if (!exists)
			return;

		// DB から削除
		this.repository.Delete(tagId);

		// 先に全作品（正式・登録待ち）から該当タグを取り除き、共有 TagNotifier が参照されない状態にしてから
		// TagStore から削除して TagNotifier を破棄する
		this.seriesStore.RemoveTagFromAllSeries(tagId);
		this.tagStore.RemoveTag(tagId);
	}

	/// <summary>
	/// 指定したタグのタグ名を変更します。
	/// </summary>
	/// <param name="target">変更対象のタグ。</param>
	/// <param name="newName">新しいタグ名。</param>
	/// <returns>変更結果。</returns>
	public TagRenameResult Rename(MangaTag target, string newName)
	{
		var trimmed = newName.Trim();

		if (string.IsNullOrEmpty(trimmed))
			return TagRenameResult.Failure(TagRenameFailureReason.EmptyName);

		// 重複チェック（自身以外）
		var isDuplicate = this.tagStore.GetTags()
			.Any(t => t.TagId != target.TagId
				&& string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));

		if (isDuplicate)
			return TagRenameResult.Failure(TagRenameFailureReason.DuplicateName);

		// DB 更新
		var updated = this.repository.Rename(target.TagId, trimmed);
		if (updated is null)
			return TagRenameResult.Failure(TagRenameFailureReason.NotFound);

		// TagStore 更新（正本の MangaTag インスタンスは置き換えず、同じインスタンスを更新して通知する）
		this.tagStore.UpdateTag(updated);

		return TagRenameResult.Success(this.tagStore.GetTagNotifier(updated.TagId).Tag.Value);
	}
}
