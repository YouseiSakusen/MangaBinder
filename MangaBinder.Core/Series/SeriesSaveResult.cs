namespace MangaBinder.Series;

/// <summary>
/// 作品保存処理の結果を表すDTO。
/// 保存後の作品情報と、素材移動に失敗した項目を保持します。
/// </summary>
public sealed class SeriesSaveResult
{
	/// <summary>
	/// 保存後の作品インスタンスを取得します。
	/// 素材移動が全件失敗して正式登録が成立しなかった場合は null となります。
	/// </summary>
	public required MangaSeries? Series { get; init; }

	/// <summary>
	/// 素材移動に失敗したアイテム一覧を取得します。
	/// IOException または UnauthorizedAccessException により移動に失敗した素材のみが含まれます。
	/// 移動元のファイル・フォルダは削除されていません。
	/// 未指定の場合は空一覧となります。
	/// </summary>
	public required IReadOnlyList<MaterialMoveItem> FailedItems { get; init; }

	/// <summary>
	/// 保存処理が成功したかどうかを取得します。
	/// <see cref="Series"/> が null ではない場合に true を返します。
	/// </summary>
	public bool IsSuccess => this.Series != null;
}
