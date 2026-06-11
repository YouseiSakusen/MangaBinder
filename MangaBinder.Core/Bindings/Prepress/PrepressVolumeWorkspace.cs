using MangaBinder.Bindings.Inspection;

namespace MangaBinder.Bindings.Prepress;

/// <summary>
/// 巻単位の Prepress 作業状態を保持するクラスです。
/// </summary>
public class PrepressVolumeWorkspace
{
	/// <summary>巻の検査結果を取得します。</summary>
	public VolumeInspectionResult VolumeInspectionResult { get; }

	/// <summary>巻内の全画像アイテム一覧を取得します。</summary>
	public List<PrepressImageItem> Images { get; } = [];

	/// <summary>見開き分割後のページ順を取得または設定します。</summary>
	public SpreadPageOrder SpreadPageOrder { get; set; } = SpreadPageOrder.RightToLeft;

	/// <summary>
	/// <see cref="PrepressVolumeWorkspace"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="volumeInspectionResult">対象巻の検査結果。</param>
	public PrepressVolumeWorkspace(VolumeInspectionResult volumeInspectionResult)
	{
		this.VolumeInspectionResult = volumeInspectionResult;
	}
}
