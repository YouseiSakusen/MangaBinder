namespace MangaBinder.Bindings;

/// <summary>
/// 素材アイテムの種別を表す列挙型です。
/// UI/Worker 共通で使用されます。
/// </summary>
public enum MaterialItemType
{
	/// <summary>作品フォルダルート。</summary>
	Root = 0,

	/// <summary>実フォルダ、またはアーカイブ内部フォルダ。</summary>
	Folder = 1,

	/// <summary>zip / rar / cbz などのアーカイブファイル。</summary>
	Archive = 2,

	/// <summary>epub ファイル。</summary>
	Epub = 3,
}
