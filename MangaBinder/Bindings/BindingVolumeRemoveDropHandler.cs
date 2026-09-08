using GongSolutions.Wpf.DragDrop;

namespace MangaBinder.Bindings;

/// <summary>
/// 選択巻一覧の除外用 Drop 領域の DropHandler です。
/// BindingVolumeViewModel を除外領域へドロップして選択を解除します。
/// </summary>
public sealed class BindingVolumeRemoveDropHandler : IDropTarget
{
	private readonly Action<BindingVolumeViewModel> onRemoveBindingVolume;

	/// <summary>
	/// <see cref="BindingVolumeRemoveDropHandler"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="onRemoveBindingVolume">除外対象の BindingVolumeViewModel を処理するコールバック。</param>
	public BindingVolumeRemoveDropHandler(Action<BindingVolumeViewModel> onRemoveBindingVolume)
	{
		this.onRemoveBindingVolume = onRemoveBindingVolume;
	}

	/// <inheritdoc/>
	public void DragOver(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not BindingVolumeViewModel)
		{
			dropInfo.Effects = System.Windows.DragDropEffects.None;
			return;
		}

		dropInfo.Effects = System.Windows.DragDropEffects.Move;
	}

	/// <inheritdoc/>
	public void Drop(IDropInfo dropInfo)
	{
		if (dropInfo.Data is not BindingVolumeViewModel item)
			return;

		// コールバックを呼び出して PageVM 側で Manager 操作を実施
		this.onRemoveBindingVolume?.Invoke(item);
	}
}
