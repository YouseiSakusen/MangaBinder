using MangaBinder.Bindings;

namespace MangaBinder.Core.Series;

/// <summary>
/// 素材フォルダ直下のファイル・フォルダを表すDTO。
/// EditorPage の素材一覧表示用です。
/// </summary>
public class MaterialFileItem
{
	/// <summary>ファイル名またはフォルダ名を取得します。</summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>実ファイルまたは実フォルダのフルパスを取得します。</summary>
	public string FullPath { get; init; } = string.Empty;

	/// <summary>アイテムの種別を取得します。</summary>
	public MaterialItemType ItemType { get; init; }

	/// <summary>ファイルの場合はサイズ（バイト）。フォルダの場合は null。</summary>
	public long? SizeBytes { get; init; }

	/// <summary>削除可能かどうかを示します。既存素材フォルダ由来は false、D&D 追加素材は true（将来用）。</summary>
	public bool CanRemove { get; init; }
}
