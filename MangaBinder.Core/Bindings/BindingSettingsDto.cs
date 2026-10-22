namespace MangaBinder.Bindings;

/// <summary>
/// 製本設定全体を保存するDTOです。
/// </summary>
public class BindingSettingsDto
{
	/// <summary>Work巻フォルダの巻番号桁数を取得または設定します。</summary>
	public int VolumeFolderDigits { get; set; }

	/// <summary>製本対象として選択された巻設定一覧を取得または設定します。</summary>
	public List<BindingVolumeDto> Volumes { get; set; } = [];
}
