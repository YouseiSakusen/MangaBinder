using MangaBinder.Bindings;

namespace MangaBinder.Series;

/// <summary>
/// 素材ファイル一覧表示用の表示モデルです。
/// </summary>
public sealed class MaterialFileItemViewModel
{
	/// <summary>ファイル名を取得します。</summary>
	public string FileName { get; init; } = "";

	/// <summary>サイズ表示文字列を取得します。</summary>
	public string SizeText { get; init; } = "";

	/// <summary>素材アイテムの種別を取得します。</summary>
	public MaterialItemType ItemType { get; init; }
}
