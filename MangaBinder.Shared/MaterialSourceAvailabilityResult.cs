namespace MangaBinder.Bindings;

/// <summary>
/// 素材フォルダの利用可否確認結果を保持するクラスです。
/// </summary>
public class MaterialSourceAvailabilityResult
{
	/// <summary>
	/// 確認結果のステータスを取得します。
	/// </summary>
	public MaterialFolderStatus Status { get; init; }

	/// <summary>
	/// 判定対象のパスを取得します。
	/// 素材Source が存在しない場合など、パスを取得できない場合は null です。
	/// </summary>
	public string? TargetPath { get; init; }

	/// <summary>
	/// 確認が成功したかどうかを取得します。
	/// </summary>
	public bool IsSuccess => this.Status == MaterialFolderStatus.Success;
}
