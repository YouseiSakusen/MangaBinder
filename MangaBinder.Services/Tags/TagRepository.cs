using MangaBinder.Settings;

namespace MangaBinder.Tags;

/// <summary>
/// <see cref="AppSettings.Tags"/> のメモリ操作を担うリポジトリクラスです。
/// DB アクセスは行いません。
/// </summary>
public sealed class TagRepository
{
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="TagRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	public TagRepository(AppSettings appSettings)
	{
		this.appSettings = appSettings;
	}

	/// <summary>
	/// 登録済みタグ一覧を取得します。
	/// </summary>
	/// <returns>タグ定義の読み取り専用リスト。</returns>
	public IReadOnlyList<MangaTag> GetAll()
		=> this.appSettings.Tags;

	/// <summary>
	/// タグを <see cref="AppSettings.Tags"/> へ追加します。
	/// </summary>
	/// <param name="tag">追加するタグ定義。</param>
	public void Add(MangaTag tag)
		=> this.appSettings.Tags.Add(tag);

	/// <summary>
	/// 指定した ID のタグを <see cref="AppSettings.Tags"/> から削除します。
	/// </summary>
	/// <param name="tagId">削除するタグの ID。</param>
	public void Remove(long tagId)
	{
		var target = this.appSettings.Tags.FirstOrDefault(t => t.TagId == tagId);
		if (target is not null)
			this.appSettings.Tags.Remove(target);
	}

	/// <summary>
	/// 指定したタグのタグ名を変更します。
	/// <see cref="MangaTag"/> は init-only プロパティのため、差し替え方式で更新します。
	/// </summary>
	/// <param name="target">変更対象のタグ。</param>
	/// <param name="newName">新しいタグ名。</param>
	/// <returns>差し替え後の <see cref="MangaTag"/>。対象が見つからない場合は <c>null</c>。</returns>
	public MangaTag? Rename(MangaTag target, string newName)
	{
		var index = this.appSettings.Tags.IndexOf(target);
		if (index < 0)
			return null;

		var updated = new MangaTag
		{
			TagId = target.TagId,
			Name = newName,
			DisplayOrder = target.DisplayOrder,
			ShowOnSeriesCard = target.ShowOnSeriesCard,
		};

		this.appSettings.Tags[index] = updated;
		return updated;
	}
}
