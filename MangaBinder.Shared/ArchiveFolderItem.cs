namespace MangaBinder.Bindings;

/// <summary>
/// アーカイブ内部のフォルダを表すDTO。
/// TreeView表示対象のフォルダノードに必要な情報を保持します。
/// 画像ファイル1件1件のエントリは保持しません。
/// </summary>
public class ArchiveFolderItem
{
	/// <summary>
	/// アーカイブ内でのエントリパスを取得または設定します。
	/// 例: "vol01" または "vol01/chapter01"
	/// </summary>
	public string EntryPath { get; init; } = string.Empty;

	/// <summary>
	/// 親フォルダのエントリパスを取得または設定します。
	/// ルートフォルダの場合は空文字列です。
	/// </summary>
	public string ParentEntryPath { get; init; } = string.Empty;

	/// <summary>
	/// このフォルダ配下の画像ファイル数を取得または設定します。
	/// </summary>
	public int FileCount { get; init; }

	/// <summary>
	/// このフォルダが選択可能かどうかを取得または設定します。
	/// </summary>
	public bool IsSelectable { get; init; }

	/// <summary>
	/// 選択不可の場合の理由を取得または設定します。
	/// 選択可能な場合は空文字列です。
	/// </summary>
	public string SelectionDisabledReason { get; init; } = string.Empty;

	/// <summary>
	/// 子フォルダ一覧を取得します。
	/// </summary>
	public List<ArchiveFolderItem> Children { get; } = [];
}
