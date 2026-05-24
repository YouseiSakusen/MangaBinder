using GongSolutions.Wpf.DragDrop;
using ObservableCollections;

namespace MangaBinder.Binding;

/// <summary>
/// 製本キュー ListView 内の D&amp;D 並び替えを処理する DropHandler です。
/// </summary>
public sealed class BindingQueueDropHandler : DefaultDropHandler
{
	private readonly ObservableList<BindingQueueItem> bindingQueueItems;
	private readonly Action onDropped;

	/// <summary>
	/// <see cref="BindingQueueDropHandler"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingQueueItems">並び替え対象のリスト。</param>
	/// <param name="onDropped">D&amp;D 成功後に呼び出すコールバック。</param>
	public BindingQueueDropHandler(ObservableList<BindingQueueItem> bindingQueueItems, Action onDropped)
	{
		this.bindingQueueItems = bindingQueueItems;
		this.onDropped = onDropped;
	}

	/// <inheritdoc/>
	public override void DragOver(IDropInfo dropInfo)
	{
		if (this.isSameListViewMove(dropInfo))
		{
			dropInfo.Effects = System.Windows.DragDropEffects.Move;
			dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
			return;
		}

		if (isTreeViewNodeDrop(dropInfo))
		{
			dropInfo.Effects = System.Windows.DragDropEffects.Copy;
			dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
			return;
		}

		dropInfo.Effects = System.Windows.DragDropEffects.None;
	}

	/// <inheritdoc/>
	public override void Drop(IDropInfo dropInfo)
	{
		if (this.isSameListViewMove(dropInfo))
		{
			this.executeMoveInList(dropInfo);
			return;
		}

		if (isTreeViewNodeDrop(dropInfo))
		{
			this.executeNodeDrop(dropInfo);
			return;
		}
	}

	private void executeMoveInList(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not BindingQueueItem item)
			return;

		var oldIndex = this.bindingQueueItems.IndexOf(item);
		if (oldIndex < 0)
			return;

		var newIndex = dropInfo.InsertIndex;

		// InsertIndex はドロップ先インデックス（削除前）なので、
		// 後ろへ移動する場合は -1 補正が必要
		if (newIndex > oldIndex)
			newIndex--;

		if (oldIndex == newIndex)
			return;

		this.bindingQueueItems.RemoveAt(oldIndex);
		this.bindingQueueItems.Insert(newIndex, item);

		this.onDropped();
	}

	private void executeNodeDrop(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not MaterialVolumeNode node)
			return;

		// 既にチェック済みなら何もしない
		if (node.IsChecked.Value)
			return;

		node.IsChecked.Value = true;
	}

	private bool isSameListViewMove(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not BindingQueueItem)
			return false;

		// ドラッグ元とドロップ先が同じ ListView であること
		return dropInfo.DragInfo?.VisualSource == dropInfo.VisualTarget;
	}

	private static bool isTreeViewNodeDrop(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not MaterialVolumeNode node)
			return false;

		return node.NodeType != MaterialVolumeNodeType.Root
			&& node.NodeType != MaterialVolumeNodeType.Archive
			&& node.CanCheck.Value;
	}
}
