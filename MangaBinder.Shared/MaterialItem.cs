namespace MangaBinder.Bindings;

/// <summary>
/// 素材フォルダ配下の表示対象ノードを表すDTO。
/// UI と Worker 両方で共通利用される型で、MaterialVolumeNode に依存しません。
/// </summary>
public class MaterialItem
{
	/// <summary>
	/// 素材アイテムの種別を取得または設定します。
	/// </summary>
	public MaterialItemType ItemType { get; init; }

	/// <summary>
	/// ノードの表示名を取得または設定します。
	/// </summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>
	/// ノードのフルパスを取得または設定します。
	/// </summary>
	public string FullPath { get; init; } = string.Empty;

	/// <summary>
	/// Archive ノードのファイルサイズ表示用テキストを取得または設定します（例：「1.2 GB」）。
	/// Archive 以外は空文字。
	/// </summary>
	public string FileSizeText { get; init; } = string.Empty;

	/// <summary>
	/// 製本対象としてカウントするファイル数を取得または設定します。
	/// </summary>
	public int FileCount { get; init; }

	/// <summary>
	/// 解凍元の実パスを取得または設定します。
	/// 実フォルダの場合はフォルダパス、Archive 内部フォルダの場合は Archive ファイルパス、Epub の場合は Epub ファイルパス。
	/// </summary>
	public string SourcePath { get; init; } = string.Empty;

	/// <summary>
	/// Archive 内部フォルダの場合のエントリ接頭辞を取得または設定します。
	/// 実フォルダ・Epub では空文字。
	/// </summary>
	public string ArchiveEntryPrefix { get; init; } = string.Empty;

	/// <summary>
	/// デフォルトで選択可能なノードかどうかを取得または設定します。
	/// </summary>
	public bool IsSelectableByDefault { get; init; }

	/// <summary>
	/// 選択不可の理由を取得または設定します。
	/// 選択可能なノードの場合は空文字。
	/// </summary>
	public string SelectionDisabledReason { get; init; } = string.Empty;

	/// <summary>
	/// 子ノード一覧を取得します。
	/// </summary>
	public List<MaterialItem> Children { get; } = [];
}
