namespace MangaBinder.Series;

/// <summary>
/// 素材移動の結果を表すDTO。
/// 後続の補償処理で利用できるように、移動した素材と移動不要だった素材を保持します。
/// </summary>
public sealed class MaterialMoveResult
{
	/// <summary>作成または利用した作品フォルダのパスを取得します。</summary>
	public required string SeriesFolderPath { get; init; }

	/// <summary>作品フォルダを今回作成したかどうかを取得します。false の場合は既存フォルダを利用しました。</summary>
	public required bool CreatedSeriesFolder { get; init; }

	/// <summary>実際に移動した素材アイテム一覧を取得します。</summary>
	public required IReadOnlyList<MaterialMoveItem> MovedItems { get; init; }

	/// <summary>移動不要だった素材アイテム一覧を取得します（既存素材または移動元と登録先が同一の場合など）。</summary>
	public required IReadOnlyList<MaterialMoveItem> SkippedItems { get; init; }
}
