namespace MangaBinder.Tags;

/// <summary>
/// タグ名変更の失敗理由を表します。
/// </summary>
public enum TagRenameFailureReason
{
	/// <summary>タグ名が空または空白のみです。</summary>
	EmptyName,

	/// <summary>同名のタグが既に存在します。</summary>
	DuplicateName,

	/// <summary>変更対象のタグが見つかりません。</summary>
	NotFound,
}
