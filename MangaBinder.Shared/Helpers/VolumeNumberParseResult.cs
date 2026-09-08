namespace MangaBinder.Helpers;

/// <summary>
/// 巻番号解析結果を表します。
/// </summary>
public class VolumeNumberParseResult
{
	/// <summary>
	/// 解析結果の種別を取得します。
	/// </summary>
	public VolumeNumberParseKind Kind { get; init; }

	/// <summary>
	/// 単巻の場合の巻番号を取得します。
	/// Kind が <see cref="VolumeNumberParseKind.Single"/> の場合にのみ有効です。
	/// </summary>
	public decimal? SingleVolume { get; init; }

	/// <summary>
	/// 範囲の場合の開始巻を取得します。
	/// Kind が <see cref="VolumeNumberParseKind.Range"/> の場合にのみ有効です。
	/// </summary>
	public decimal? RangeStart { get; init; }

	/// <summary>
	/// 範囲の場合の終了巻を取得します。
	/// Kind が <see cref="VolumeNumberParseKind.Range"/> の場合にのみ有効です。
	/// </summary>
	public decimal? RangeEnd { get; init; }

	/// <summary>
	/// マッチしたパターンを識別できる情報を取得します。
	/// </summary>
	public string? MatchedPattern { get; init; }
}
