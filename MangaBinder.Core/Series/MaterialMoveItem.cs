using MangaBinder.Bindings;

namespace MangaBinder.Series;

/// <summary>
/// 移動結果に含まれる素材アイテムを表すDTO。
/// </summary>
public sealed class MaterialMoveItem
{
	/// <summary>移動元のフルパスを取得します。</summary>
	public required string SourcePath { get; init; }

	/// <summary>移動先のフルパスを取得します。</summary>
	public required string DestinationPath { get; init; }

	/// <summary>素材アイテムの種別を取得します。</summary>
	public required MaterialItemType Type { get; init; }
}
