namespace MangaBinder.Tags;

/// <summary>
/// タグ追加・削除・リネームに関するルールを集約するクラスです。
/// </summary>
public sealed class TagEditor
{
	private readonly TagRepository repository;
	private readonly MangaSeriesStore store;

	/// <summary>
	/// <see cref="TagEditor"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="repository">タグリポジトリ。</param>
	/// <param name="store">MangaSeries 正本ストア。</param>
	public TagEditor(TagRepository repository, MangaSeriesStore store)
	{
		this.repository = repository;
		this.store = store;
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

		// Store のタグ一覧から重複チェック
		var isDuplicate = this.store.GetTags()
			.Any(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));

		if (isDuplicate)
			return TagAddResult.Failure(TagAddFailureReason.Duplicate);

		// DisplayOrder を計算（現在のタグ数が次の順序）
		var nextOrder = this.store.GetTags().Count;

		// DB に挿入、採番済みタグを取得
		var addedTag = this.repository.Insert(trimmed, nextOrder, showOnSeriesCard: true);

		// Store へ追加
		this.store.AddTag(addedTag);

		return TagAddResult.Success(addedTag);
	}

	/// <summary>
	/// 指定した ID のタグを削除します。
	/// </summary>
	/// <param name="tagId">削除するタグの ID。</param>
	public void Delete(long tagId)
	{
		var exists = this.store.GetTags().Any(t => t.TagId == tagId);
		if (!exists)
			return;

		// DB から削除
		this.repository.Delete(tagId);

		// Store から削除
		this.store.RemoveTag(tagId);

		// 各 MangaSeries から該当タグを削除
		foreach (var series in this.store.GetAll())
		{
			var targetTag = series.Tags.FirstOrDefault(t => t.TagId == tagId);
			if (targetTag is not null)
				series.Tags.Remove(targetTag);
		}
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
		var isDuplicate = this.store.GetTags()
			.Any(t => t.TagId != target.TagId
				&& string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));

		if (isDuplicate)
			return TagRenameResult.Failure(TagRenameFailureReason.DuplicateName);

		// DB 更新
		var updated = this.repository.Rename(target.TagId, trimmed);
		if (updated is null)
			return TagRenameResult.Failure(TagRenameFailureReason.NotFound);

		// Store 更新
		this.store.UpdateTag(updated);

		return TagRenameResult.Success(updated);
	}
}
