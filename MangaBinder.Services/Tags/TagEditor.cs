namespace MangaBinder.Tags;

/// <summary>
/// タグ追加に関するルールを集約するクラスです。
/// </summary>
public sealed class TagEditor
{
	private readonly TagRepository repository;

	/// <summary>
	/// <see cref="TagEditor"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="repository">タグリポジトリ。</param>
	public TagEditor(TagRepository repository)
	{
		this.repository = repository;
	}

	/// <summary>
	/// 指定した名前のタグを追加します。
	/// </summary>
	/// <param name="name">追加するタグ名。</param>
	/// <returns>追加結果。</returns>
	public TagAddResult Add(string name)
	{
		var trimmed = name.Trim();

		if (string.IsNullOrEmpty(trimmed))
			return TagAddResult.Failure(TagAddFailureReason.EmptyName);

		var isDuplicate = this.repository.GetAll()
			.Any(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));

		if (isDuplicate)
			return TagAddResult.Failure(TagAddFailureReason.Duplicate);

		var nextOrder = this.repository.GetAll().Count;

		var tag = new MangaTag
		{
			TagId = 0,
			Name = trimmed,
			DisplayOrder = nextOrder,
			ShowOnSeriesCard = true,
		};

		this.repository.Add(tag);
		return TagAddResult.Success(tag);
	}

	/// <summary>
	/// 指定した ID のタグを削除します。
	/// 対象が存在しない場合は何もしません。
	/// </summary>
	/// <param name="tagId">削除するタグの ID。</param>
	public void Delete(long tagId)
	{
		var exists = this.repository.GetAll().Any(t => t.TagId == tagId);
		if (!exists)
			return;

		this.repository.Remove(tagId);
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

		var isDuplicate = this.repository.GetAll()
			.Any(t => !ReferenceEquals(t, target)
				&& string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase));

		if (isDuplicate)
			return TagRenameResult.Failure(TagRenameFailureReason.DuplicateName);

		var updated = this.repository.Rename(target, trimmed);
		if (updated is null)
			return TagRenameResult.Failure(TagRenameFailureReason.NotFound);

		return TagRenameResult.Success(updated);
	}
}
