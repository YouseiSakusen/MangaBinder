namespace MangaBinder;

/// <summary>
/// 作品カードの「素材フォルダを開く」メニューで使用する表示用モデルです。
/// </summary>
public class MaterialSourceItem
{
	/// <summary>メニューに表示する名前を取得します。</summary>
	public string DisplayName { get; init; } = string.Empty;

	/// <summary>開く対象フォルダの物理フルパスを取得します。</summary>
	public string FolderPath { get; init; } = string.Empty;
}
