namespace MangaBinder.Bindings.Extraction;

/// <summary>
/// 素材形式ごとの展開処理を実行する抽象化です。
/// Material.SourcePath 単位でグループ化された BindingVolume を受け取り、
/// 対応する素材形式に応じて展開・画像化処理を実行します。
/// </summary>
public interface IMaterialExtractor
{
	/// <summary>
	/// 指定されたグループに属する BindingVolume の素材を展開します。
	/// </summary>
	/// <param name="group">
	/// Material.SourcePath 単位でグループ化された BindingVolume の集合。
	/// 同一の SourcePath に属するボリュームが複数含まれます。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	ValueTask ExtractAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default);
}
