namespace MangaBinder.Tags;

/// <summary>
/// タグ名変更処理の結果を表します。
/// </summary>
public sealed class TagRenameResult
{
	/// <summary>変更が成功したかどうかを取得します。</summary>
	public bool IsSuccess => this.FailureReason is null;

	/// <summary>失敗理由を取得します。成功時は <c>null</c>。</summary>
	public TagRenameFailureReason? FailureReason { get; }

	/// <summary>変更後の <see cref="MangaTag"/>。成功時のみ値を持ちます。</summary>
	public MangaTag? RenamedTag { get; }

	private TagRenameResult() { }

	private TagRenameResult(MangaTag renamedTag)
	{
		this.RenamedTag = renamedTag;
	}

	private TagRenameResult(TagRenameFailureReason reason)
	{
		this.FailureReason = reason;
	}

	/// <summary>成功結果を生成します。</summary>
	/// <param name="renamedTag">変更後のタグ定義。</param>
	public static TagRenameResult Success(MangaTag renamedTag) => new(renamedTag);

	/// <summary>
	/// 失敗結果を生成します。
	/// </summary>
	/// <param name="reason">失敗理由。</param>
	public static TagRenameResult Failure(TagRenameFailureReason reason) => new(reason);
}
