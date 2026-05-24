using GongSolutions.Wpf.DragDrop;

namespace MangaBinder.Binding;

/// <summary>
/// 製本キュー除外用 Drop 領域の DropHandler です。
/// </summary>
public sealed class BindingQueueRemoveDropHandler : IDropTarget
{
	/// <inheritdoc/>
	public void DragOver(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not BindingQueueItem)
		{
			dropInfo.Effects = System.Windows.DragDropEffects.None;
			return;
		}

		dropInfo.Effects = System.Windows.DragDropEffects.Move;
	}

	/// <inheritdoc/>
	public void Drop(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not BindingQueueItem item)
			return;

		item.SourceNode.IsChecked.Value = false;
	}
}
