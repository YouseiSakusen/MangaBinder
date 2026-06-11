namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 巻ごとの検査結果を保持するクラスです。
/// </summary>
public class VolumeInspectionResult
{
	/// <summary>巻名を取得します。</summary>
	public string VolumeName { get; init; } = string.Empty;

	/// <summary>ワークフォルダ内の展開済み巻フォルダのフルパスを取得します。</summary>
	public string WorkVolumeFolderPath { get; init; } = string.Empty;

	/// <summary>予定処理の一覧を取得します。</summary>
	public IReadOnlyList<string> PlannedActions { get; init; } = [];

	/// <summary>見開き分割が必要かどうかを取得します。</summary>
	public bool RequiresSplit { get; init; }

	/// <summary>画像ファイル数を取得します。</summary>
	public int ImageFileCount { get; init; }

	/// <summary>横長画像が存在するかどうかを取得します。</summary>
	public bool HasLandscapeImages { get; init; }

	/// <summary>全て横長画像かどうかを取得します。</summary>
	public bool AllLandscape { get; init; }

	/// <summary>ファイル形式が混在しているかどうかを取得します。</summary>
	public bool HasMixedFormats { get; init; }

	/// <summary>ファイル名文字数が不揃いかどうかを取得します。</summary>
	public bool HasIrregularFileNameLength { get; init; }

	/// <summary>サブフォルダが存在するかどうかを取得します。</summary>
	public bool HasSubFolders { get; init; }

	/// <summary>サブフォルダ展開方式の選択インデックスを取得または設定します。</summary>
	public int SubFolderModeIndex { get; set; }

	/// <summary>同一ベース名のファイルが複数存在するかどうかを取得します。</summary>
	public bool HasDuplicateFileBaseName { get; init; }

	/// <summary>巻の処理中にエラーが発生したかどうかを取得します。</summary>
	public bool HasError { get; init; }

	/// <summary>エラーメッセージを取得します。エラーがない場合は <see langword="null"/>。</summary>
	public string? ErrorMessage { get; init; }
}
