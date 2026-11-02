namespace MangaBinder.Bindings;

/// <summary>
/// 展開済み巻の追加により置換される、元素材由来の既存巻の情報を表します。
/// </summary>
/// <param name="WorkVolume">追加する展開済み巻。</param>
/// <param name="ReplacedMaterials">置換対象となる、同じ巻番号の元素材一覧。</param>
public record ExpandedVolumeReplacement(MaterialItem WorkVolume, IReadOnlyList<MaterialItem> ReplacedMaterials);
