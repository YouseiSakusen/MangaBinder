namespace MangaBinder.Series;

/// <summary>
/// 作品編集画面でユーザーが入力した値を、画面から Manager へ渡すための入力値です。
/// 画面は入力値をここへ詰めて渡すだけで、<see cref="MangaSeries"/> への反映は Manager が行います。
/// </summary>
public sealed record EditorInputValues
{
	/// <summary>タイトルを取得します。</summary>
	public string? Title { get; init; }

	/// <summary>作者を取得します。</summary>
	public string? Author { get; init; }

	/// <summary>出版社を取得します。</summary>
	public string? Publisher { get; init; }

	/// <summary>あらすじを取得します。</summary>
	public string? Description { get; init; }

	/// <summary>メモを取得します。</summary>
	public string? Memo { get; init; }

	/// <summary>未完（巻抜けあり）かどうかを取得します。</summary>
	public bool IsIncomplete { get; init; }

	/// <summary>開始巻を取得します。</summary>
	public double StartVolume { get; init; }

	/// <summary>完結巻を取得します。未入力の場合は null です。</summary>
	public double? EndVolume { get; init; }

	/// <summary>所持最大巻を取得します。未入力の場合は null です。</summary>
	public double? OwnedMaxVolume { get; init; }

	/// <summary>全巻所持かどうかを取得します。</summary>
	public bool IsOwnedCompleted { get; init; }
}
