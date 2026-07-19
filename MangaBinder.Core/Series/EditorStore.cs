using MangaBinder.Settings;

namespace MangaBinder.Core.Series;

/// <summary>
/// EditorPage 専用の編集状態を保持する一時的なストアです。
/// EditorPageViewModel と同じライフサイクルで使用されます。
/// </summary>
public class EditorStore
{
	/// <summary>
	/// 編集中の作品を取得または設定します。
	/// </summary>
	public MangaSeries? EditingSeries { get; set; }

	/// <summary>
	/// 編集開始時の作品を取得または設定します。
	/// 既存作品編集時は編集前の DeepCopy が格納されます。
	/// </summary>
	public MangaSeries? OriginalSeries { get; set; }

	/// <summary>
	/// 保存先として選択された素材フォルダを取得または設定します。
	/// </summary>
	public SourceFolder? SelectedMaterialSourceFolder { get; set; }

	/// <summary>
	/// 別ドライブ移動が確認済みであるかを取得または設定します。
	/// </summary>
	public bool DifferentDriveConfirmed { get; set; }
}
