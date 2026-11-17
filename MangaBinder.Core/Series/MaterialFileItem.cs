using MangaBinder.Bindings;

namespace MangaBinder.Series;

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

	/// <summary>素材のサイズ（バイト）。実フォルダのサイズ計算が完了するまでは null（サイズ未確定）。</summary>
	public long? SizeBytes { get; init; }

	/// <summary>サイズが未確定（計算中）かどうかを取得します。</summary>
	public bool IsSizePending => this.SizeBytes is null;

	/// <summary>今回の編集で追加した素材かどうかを示します。既存素材フォルダ由来は false、追加素材は true（削除可能）です。</summary>
	public bool CanRemove { get; init; }

	/// <summary>
	/// サイズを確定した新しいインスタンスを生成します。
	/// </summary>
	/// <param name="sizeBytes">確定したサイズ（バイト）。</param>
	/// <returns>サイズを確定した <see cref="MaterialFileItem"/>。</returns>
	public MaterialFileItem WithSizeBytes(long sizeBytes)
		=> new()
		{
			Name = this.Name,
			FullPath = this.FullPath,
			ItemType = this.ItemType,
			SizeBytes = sizeBytes,
			CanRemove = this.CanRemove,
		};
}
