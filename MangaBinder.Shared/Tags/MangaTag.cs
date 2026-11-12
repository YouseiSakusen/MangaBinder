namespace MangaBinder.Tags;

/// <summary>
/// タグ定義を表すエンティティです。
/// TagStore が所有する同一インスタンスを各作品・各画面が共有するため、変更対象プロパティは mutable です。
/// 変更通知は <c>TagNotifier</c> が担当し、このクラス自体は通知機構を持ちません。
/// </summary>
public sealed class MangaTag
{
	/// <summary>タグ ID を取得します。</summary>
	public long TagId { get; init; }

	/// <summary>タグ名を取得します。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>表示順を取得します。</summary>
	public int DisplayOrder { get; set; }

	/// <summary>作品カードに表示するかどうかを取得します。</summary>
	public bool ShowOnSeriesCard { get; set; }
}
