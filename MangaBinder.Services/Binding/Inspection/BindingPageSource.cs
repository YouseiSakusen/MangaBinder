namespace MangaBinder.Binding.Inspection;

/// <summary>
/// SeriesInspector へページ順画像情報を渡すための DTO です。
/// </summary>
public sealed class BindingPageSource
{
	/// <summary>元ページ名を取得します。</summary>
	public required string SourceName { get; init; }

	/// <summary>元拡張子（.jpg, .avif 等）を取得します。</summary>
	public required string Extension { get; init; }

	/// <summary>
	/// ページ画像 Stream を開きます。
	/// 呼び出し元が Dispose します。
	/// </summary>
	public required Func<CancellationToken, ValueTask<Stream>> OpenStreamAsync { get; init; }
}
