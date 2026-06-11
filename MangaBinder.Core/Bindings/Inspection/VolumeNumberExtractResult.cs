namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// <see cref="VolumeNumberExtractor"/> による巻番号抽出結果を表すクラスです。
/// </summary>
public sealed class VolumeNumberExtractResult
{
	/// <summary>抽出対象のフォルダ名（またはファイル名）を取得します。</summary>
	public required string SourceName { get; init; }

	/// <summary>抽出に成功したかどうかを取得します。</summary>
	public bool Success { get; init; }

	/// <summary>
	/// 抽出した巻番号を取得します。
	/// 抽出できなかった場合は <see langword="null"/> です。
	/// 1.5巻・0巻などの小数も表現できます。
	/// </summary>
	public decimal? VolumeNumber { get; init; }

	/// <summary>マッチしたパターン名を取得します。抽出できなかった場合は空文字です。</summary>
	public string PatternName { get; init; } = string.Empty;

	/// <summary>判断理由や補足メッセージを取得します。</summary>
	public string Message { get; init; } = string.Empty;
}
