namespace MangaBinder.Bindings;

/// <summary>
/// 圧縮ファイル比較結果Treeのノードです。
/// Left / Right は BindingStore が保持している既存 MaterialItem への参照で、
/// 対応するノードが存在しない側は null です。
/// </summary>
public class ArchiveCompareNode
{
	/// <summary>左側の MaterialItem を取得します。存在しない場合は null。</summary>
	public MaterialItem? Left { get; }

	/// <summary>右側の MaterialItem を取得します。存在しない場合は null。</summary>
	public MaterialItem? Right { get; }

	/// <summary>子ノードの一覧を取得します。</summary>
	public IReadOnlyList<ArchiveCompareNode> Children { get; }

	/// <summary>
	/// <see cref="ArchiveCompareNode"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="left">左側の MaterialItem。</param>
	/// <param name="right">右側の MaterialItem。</param>
	/// <param name="children">子ノードの一覧。</param>
	public ArchiveCompareNode(MaterialItem? left, MaterialItem? right, IReadOnlyList<ArchiveCompareNode> children)
	{
		this.Left = left;
		this.Right = right;
		this.Children = children;
	}
}
