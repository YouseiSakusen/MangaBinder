namespace MangaBinder.Bindings;

/// <summary>
/// 巻選択工程の初期化処理の結果を保持するクラスです。
/// Manager から ViewModel へ返す処理結果のみを表します。
/// 素材ツリーの正本は BindingStore.Materials に格納されます。
/// </summary>
public class VolumeSelectionInitializeResult
{
	/// <summary>
	/// 初期化処理のステータスを取得します。
	/// </summary>
	public MaterialFolderStatus Status { get; init; }

	/// <summary>
	/// 対象とした素材フォルダのパスを取得します。
	/// </summary>
	public string TargetPath { get; init; } = string.Empty;

	/// <summary>
	/// Nested Archive が含まれているかどうかを取得します。
	/// </summary>
	public bool HasNestedArchive { get; init; }

	/// <summary>
	/// Nested Archive に該当する外側アーカイブファイル名の一覧を取得します。
	/// </summary>
	public IReadOnlyList<string> NestedArchiveFileNames { get; init; } = [];

	/// <summary>
	/// 初期化が成功したかどうかを取得します。
	/// </summary>
	public bool IsSuccess => this.Status == MaterialFolderStatus.Success;
}
