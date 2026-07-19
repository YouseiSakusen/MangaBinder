using MangaBinder.Bindings;

namespace MangaBinder.Series;

/// <summary>
/// 素材候補ファイル/フォルダを表すDTO。
/// MaterialFileItemViewModel に変換する前のデータ転送用です。
/// </summary>
public sealed class MaterialFileCandidate
{
	/// <summary>実ファイルまたは実フォルダのフルパスを取得します。</summary>
	public required string FullPath { get; init; }

	/// <summary>ファイル名またはフォルダ名を取得します。</summary>
	public required string FileName { get; init; }

	/// <summary>ファイルサイズ（バイト）。フォルダの場合は null。</summary>
	public long? Size { get; init; }

	/// <summary>素材アイテムの種別を取得します。</summary>
	public required MaterialItemType Type { get; init; }
}
