using GongSolutions.Wpf.DragDrop;
using System.Windows;

namespace MangaBinder.Bindings;

/// <summary>
/// 素材 TreeView からのドラッグ操作を処理する DragHandler です。
/// Reactive側の MaterialItemViewModel をドラッグ可能にします。
/// </summary>
public sealed class MaterialItemDragHandler : DefaultDragHandler
{
	/// <inheritdoc/>
	public override void StartDrag(IDragInfo dragInfo)
	{
		if (dragInfo.SourceItem is not MaterialItemViewModel item)
		{
			dragInfo.Effects = DragDropEffects.None;
			return;
		}

		if (!isValidDragTarget(item))
		{
			dragInfo.Effects = DragDropEffects.None;
			return;
		}

		dragInfo.Data = item;
		dragInfo.Effects = DragDropEffects.Copy;
	}

	/// <inheritdoc/>
	public override bool CanStartDrag(IDragInfo dragInfo)
	{
		if (dragInfo.SourceItem is not MaterialItemViewModel item)
			return false;

		return isValidDragTarget(item);
	}

	private static bool isValidDragTarget(MaterialItemViewModel item)
		=> item.CanCheck.CurrentValue;
}
