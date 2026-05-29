namespace MangaBinder.Tags;

/// <summary>
/// タグ追加処理の結果を表します。
/// </summary>
public sealed class TagAddResult
{
	/// <summary>追加が成功したかどうかを取得します。</summary>
	public bool IsSuccess => this.AddedTag is not null;

	/// <summary>追加されたタグを取得します。失敗時は <c>null</c>。</summary>
	public MangaTag? AddedTag { get; }

	/// <summary>失敗理由を取得します。</summary>
	public TagAddFailureReason? FailureReason { get; }

	private TagAddResult(MangaTag tag)
	{
		this.AddedTag = tag;
	}

	private TagAddResult(TagAddFailureReason reason)
	{
		this.FailureReason = reason;
	}

	/// <summary>
	/// 成功結果を生成します。
	/// </summary>
	/// <param name="tag">追加されたタグ。</param>
	public static TagAddResult Success(MangaTag tag) => new(tag);

	/// <summary>
	/// 失敗結果を生成します。
	/// </summary>
	/// <param name="reason">失敗理由。</param>
	public static TagAddResult Failure(TagAddFailureReason reason) => new(reason);
}
