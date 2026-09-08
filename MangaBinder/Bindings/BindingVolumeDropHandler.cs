using GongSolutions.Wpf.DragDrop;

namespace MangaBinder.Bindings;

/// <summary>
/// 選択巻一覧（右側 ListView）の D&D 操作を処理する DropHandler です。
/// TreeView からの BindingVolume 追加と、同一一覧内での並び替え の両方を扱います。
/// </summary>
public sealed class BindingVolumeDropHandler : DefaultDropHandler
{
	private readonly Action<MaterialItemViewModel> onSelectMaterialFromDrop;
	private readonly Action<BindingVolumeViewModel, int> onMoveBindingVolumeFromDrop;

	/// <summary>
	/// <see cref="BindingVolumeDropHandler"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="onSelectMaterialFromDrop">TreeView から選択巻一覧へドロップされた MaterialItemViewModel を処理するコールバック。</param>
	/// <param name="onMoveBindingVolumeFromDrop">選択巻一覧内で並び替えされた BindingVolumeViewModel を処理するコールバック。引数は (item, newIndex)。</param>
	public BindingVolumeDropHandler(
		Action<MaterialItemViewModel> onSelectMaterialFromDrop,
		Action<BindingVolumeViewModel, int> onMoveBindingVolumeFromDrop)
	{
		this.onSelectMaterialFromDrop = onSelectMaterialFromDrop;
		this.onMoveBindingVolumeFromDrop = onMoveBindingVolumeFromDrop;
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

		if (isTreeViewMaterialDrop(dropInfo))
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

		if (isTreeViewMaterialDrop(dropInfo))
		{
			this.executeMaterialDrop(dropInfo);
			return;
		}
	}

	private void executeMoveInList(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not BindingVolumeViewModel item)
			return;

		// ドロップされた BindingVolumeViewModel の現在のインデックスを取得
		// ※ここでは、dropInfo.VisualTarget（ListView 内のコレクション）に直結していると仮定
		// 実際の一覧は BindingStore.BindingVolumes の派生ビューなので、
		// BindingVolumeViewModel を通じて検索する必要がある

		// dropInfo.InsertIndex はドロップ先のインデックス（削除前基準）
		var newIndex = dropInfo.InsertIndex;

		// DragSource にある BindingVolumeViewModel の現在位置を特定する難易度が高いため、
		// PageVM 側で GetCurrentIndex 的な操作を提供することも考えられるが、
		// ここでは、GongSolutions の標準パターンに従い、
		// DragInfo.SourceItem が BindingVolumeViewModel であることを利用する

		// InsertIndex は削除前の位置を基準にしているため、
		// 後ろへ移動する場合は -1 の補正が必要
		if (dropInfo.DragInfo is not null && dropInfo.DragInfo.SourceIndex >= 0)
		{
			var oldIndex = dropInfo.DragInfo.SourceIndex;

			// 後ろへ移動する場合は -1 補正
			if (newIndex > oldIndex)
				newIndex--;

			// 同じ位置への移動は何もしない
			if (oldIndex == newIndex)
				return;
		}

		// コールバックを呼び出して PageVM 側で Manager 操作を実施
		this.onMoveBindingVolumeFromDrop?.Invoke(item, newIndex);
	}

	private void executeMaterialDrop(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not MaterialItemViewModel materialItem)
			return;

		// コールバックを呼び出して PageVM 側で Manager 操作を実施
		this.onSelectMaterialFromDrop?.Invoke(materialItem);
	}

	private bool isSameListViewMove(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not BindingVolumeViewModel)
			return false;

		// ドラッグ元とドロップ先が同じ ListView であること
		return dropInfo.DragInfo?.VisualSource == dropInfo.VisualTarget;
	}

	private static bool isTreeViewMaterialDrop(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not MaterialItemViewModel)
			return false;

		return true;
	}
}
