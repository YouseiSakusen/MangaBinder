using System.Windows.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 展開済み巻の追加に伴う既存巻の置換確認内容を表すUserControlです。
/// DataContext には <see cref="ExpandedVolumeReplacement"/> の一覧を設定します。
/// </summary>
public partial class ExpandedVolumeReplaceConfirmContent : UserControl
{
	/// <summary>
	/// <see cref="ExpandedVolumeReplaceConfirmContent"/> の新しいインスタンスを初期化します。
	/// </summary>
	public ExpandedVolumeReplaceConfirmContent()
	{
		this.InitializeComponent();
	}
}
