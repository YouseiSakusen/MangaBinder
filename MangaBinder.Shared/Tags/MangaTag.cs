namespace MangaBinder.Tags;

/// <summary>
/// タグ定義を表すエンティティです。
/// </summary>
public sealed class MangaTag
{
	/// <summary>タグ ID を取得します。</summary>
	public long TagId { get; init; }

	/// <summary>タグ名を取得します。</summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>表示順を取得します。</summary>
	public int DisplayOrder { get; init; }

	/// <summary>作品カードに表示するかどうかを取得します。</summary>
	public bool ShowOnSeriesCard { get; init; }
}
