using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 展開済み巻追加Dialogの選択一覧と一括操作コマンドを保持する ViewModel です。
/// </summary>
public class ExpandedVolumeSelectionViewModel
{
	/// <summary>表示対象の展開済み巻一覧を取得します。</summary>
	public IReadOnlyList<MaterialItem> Items { get; }

	/// <summary>すべての巻を選択するコマンドを取得します。</summary>
	public ReactiveCommand<Unit> SelectAllCommand { get; }

	/// <summary>すべての巻の選択を解除するコマンドを取得します。</summary>
	public ReactiveCommand<Unit> ClearAllCommand { get; }

	/// <summary>
	/// <see cref="ExpandedVolumeSelectionViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="items">表示対象の展開済み巻一覧。</param>
	public ExpandedVolumeSelectionViewModel(IReadOnlyList<MaterialItem> items)
	{
		this.Items = items;
		this.SelectAllCommand = new ReactiveCommand<Unit>(_ => this.setAllChecked(true));
		this.ClearAllCommand = new ReactiveCommand<Unit>(_ => this.setAllChecked(false));
	}

	private void setAllChecked(bool isChecked)
	{
		foreach (var item in this.Items)
		{
			item.IsChecked.Value = isChecked;
		}
	}
}
