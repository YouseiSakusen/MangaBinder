namespace MangaBinder.Series;

/// <summary>
/// 素材削除処理の結果を表します。
/// </summary>
/// <param name="Removed">素材を削除したかどうか。</param>
/// <param name="OwnedMaxVolumeUpdated">素材の変更に伴う推定で、所持最大巻数を更新したかどうか。</param>
public sealed record MaterialRemoveResult(bool Removed, bool OwnedMaxVolumeUpdated);
