namespace MangaBinder.Series;

/// <summary>
/// 素材追加処理の結果を表します。
/// </summary>
/// <param name="Added">1 件以上の素材を追加したかどうか。</param>
/// <param name="OwnedMaxVolumeUpdated">素材の変更に伴う推定で、所持最大巻数を更新したかどうか。</param>
/// <param name="AlreadyAddedFiles">既に追加済みのため追加しなかった素材名。</param>
/// <param name="SameNameFiles">同名の素材が存在するため追加しなかった素材名。</param>
public sealed record MaterialAddResult(
	bool Added,
	bool OwnedMaxVolumeUpdated,
	IReadOnlyList<string> AlreadyAddedFiles,
	IReadOnlyList<string> SameNameFiles);
