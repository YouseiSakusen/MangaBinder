namespace MangaBinder.Bindings;

/// <summary>
/// 製本設定全体を保存するDTOです。
/// </summary>
public class BindingSettingsDto
{
	/// <summary>製本設定のバージョンを取得または設定します。JSON に存在しない場合は初期バージョンの "1.0" です。</summary>
	public string Version { get; set; } = "1.0";

	/// <summary>Work巻フォルダの巻番号桁数</summary>
	public int VolumeFolderDigits { get; set; }

	/// <summary>製本対象として選択された巻設定一覧を取得または設定します。</summary>
	public List<BindingVolumeDto> Volumes { get; set; } = [];
}
