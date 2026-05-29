namespace MangaBinder.Tags;

/// <summary>
/// タグ追加の失敗理由を表します。
/// </summary>
public enum TagAddFailureReason
{
	/// <summary>タグ名が空または空白のみです。</summary>
	EmptyName,

	/// <summary>同名のタグが既に存在します。</summary>
	Duplicate,
}
