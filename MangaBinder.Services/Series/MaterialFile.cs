using MangaBinder.Bindings;

namespace MangaBinder.Series;

/// <summary>
/// 素材移動用のDTO。
/// ViewModel から受け取る情報の最小化を目的としています。
/// </summary>
public sealed class MaterialFile
{
	/// <summary>実ファイルまたは実フォルダのフルパスを取得します。</summary>
	public required string FullPath { get; init; }

	/// <summary>素材アイテムの種別を取得します。</summary>
	public required MaterialItemType Type { get; init; }

	/// <summary>
	/// 移動対象かどうかを示します。
	/// true の場合は編集画面で今回追加された素材（移動対象）、
	/// false の場合は既存作品から読み込まれた既存素材（移動対象外）を表します。
	/// </summary>
	public required bool CanRemove { get; init; }
}
