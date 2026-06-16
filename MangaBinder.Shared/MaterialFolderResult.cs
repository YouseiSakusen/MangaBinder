namespace MangaBinder.Bindings;

/// <summary>
/// 素材フォルダ解析の結果を保持するクラスです。
/// </summary>
public class MaterialFolderResult
{
	/// <summary>
	/// 解析結果のステータスを取得します。
	/// </summary>
	public MaterialFolderStatus Status { get; init; }

	/// <summary>
	/// 判定対象のパスを取得します。
	/// </summary>
	public string TargetPath { get; init; } = string.Empty;

	/// <summary>
	/// 素材ツリーの情報を取得します。
	/// UI と Worker 両方で共通利用される共通形式です。
	/// </summary>
	public IReadOnlyList<MaterialItem> Materials { get; init; } = [];

	/// <summary>
	/// Nested Archive が含まれているかどうかを取得します。
	/// </summary>
	public bool HasNestedArchive { get; init; }
}
