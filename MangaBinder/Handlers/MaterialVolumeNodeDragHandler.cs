using GongSolutions.Wpf.DragDrop;
using MangaBinder.Bindings;
using MangaBinder.Bindings;
using System.Windows;

namespace MangaBinder.Handlers;

/// <summary>
/// 素材 TreeView からのドラッグ操作を処理する DragHandler です。
/// </summary>
public sealed class MaterialVolumeNodeDragHandler : DefaultDragHandler
{
	/// <inheritdoc/>
	public override void StartDrag(IDragInfo dragInfo)
	{
		if (dragInfo.SourceItem is not MaterialVolumeNode node)
		{
			dragInfo.Effects = DragDropEffects.None;
			return;
		}

		if (!isValidDragTarget(node))
		{
			dragInfo.Effects = DragDropEffects.None;
			return;
		}

		dragInfo.Data = node;
		dragInfo.Effects = DragDropEffects.Copy;
	}

	/// <inheritdoc/>
	public override bool CanStartDrag(IDragInfo dragInfo)
	{
		if (dragInfo.SourceItem is not MaterialVolumeNode node)
			return false;

		return isValidDragTarget(node);
	}

	private static bool isValidDragTarget(MaterialVolumeNode node)
		=> node.NodeType != MaterialItemType.Root
		&& node.NodeType != MaterialItemType.Archive
		&& node.CanCheck.Value;
}
