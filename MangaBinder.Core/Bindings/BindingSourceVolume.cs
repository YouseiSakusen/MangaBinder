namespace MangaBinder.Bindings;

/// <summary>
/// VolumeSelectionPage で確定した巻の情報を次工程へ渡すための DTO です。
/// </summary>
public class BindingSourceVolume
{
	/// <summary>表示名を取得します。</summary>
	public string DisplayName { get; init; } = string.Empty;

	/// <summary>巻番号を取得します。</summary>
	public decimal VolumeNumber { get; init; }

	/// <summary>ノード種別を取得します。</summary>
	public MaterialItemType NodeType { get; init; }

	/// <summary>
	/// Extractor 選択用の確定素材種別を取得します。
	/// Archive 内部フォルダを選択した場合は Archive、それ以外は NodeType と一致します。
	/// </summary>
	public MaterialItemType SourceType { get; init; }

	/// <summary>
	/// 解凍元の実パスを取得します。
	/// 実フォルダの場合はフォルダパス、
	/// Archive 内部フォルダの場合は Archive ファイルパス、
	/// Epub の場合は Epub ファイルパス。
	/// </summary>
	public string SourcePath { get; init; } = string.Empty;

	/// <summary>
	/// Archive 内部フォルダの場合のエントリ接頭辞を取得します。
	/// 実フォルダ・Epub では <see langword="null"/>。
	/// </summary>
	public string? ArchiveEntryPrefix { get; init; }

	/// <summary>ノードのフルパスを取得します（表示・デバッグ用）。</summary>
	public string FullPath { get; init; } = string.Empty;

	/// <summary>出力先フォルダ名を取得します。</summary>
	public string OutputVolumeFolderName { get; init; } = string.Empty;
}
