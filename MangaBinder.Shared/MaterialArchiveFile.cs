namespace MangaBinder.Bindings;

/// <summary>
/// アーカイブファイルの情報と内部フォルダ構造を保持するDTO。
/// 将来的に MaterialArchives テーブルへ保存する前提の情報を持ちます。
/// </summary>
public class MaterialArchiveFile
{
	/// <summary>
	/// アーカイブファイルの絶対パスを取得または設定します。
	/// </summary>
	public string ArchivePath { get; init; } = string.Empty;

	/// <summary>
	/// アーカイブファイルのサイズ（バイト）を取得または設定します。
	/// </summary>
	public long FileSize { get; init; }

	/// <summary>
	/// アーカイブファイルの最終更新日時を取得または設定します。
	/// </summary>
	public DateTime LastWriteTime { get; init; }

	/// <summary>
	/// アーカイブ内のフォルダ一覧を取得します。
	/// </summary>
	public List<ArchiveFolderItem> Folders { get; } = [];

	/// <summary>
	/// このアーカイブ内に、MangaBinder が対応する圧縮ファイルが1件以上含まれているかどうかを示します。
	/// </summary>
	public bool IsNestedArchive { get; init; }

	// TODO: 将来的に素材ツリー情報を保持する予定です。
}
