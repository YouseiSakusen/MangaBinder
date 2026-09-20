namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 素材形式ごとの展開処理を実行する抽象化です。
/// Material.SourcePath 単位でグループ化された BindingVolume を受け取り、
/// 対応する素材形式に応じて2段階で展開・画像化処理を実行します。
/// </summary>
public interface IMaterialExtractor
{
	/// <summary>
	/// 素材グループを解析して BindingImage 一覧を生成します。
	/// 2段階処理の第1段階。素材分析と BindingImage 生成までを担当します。
	/// Archive の場合でも画像本体の MemoryStream 化は行いません。
	/// Folder / WorkFolder / Epub の場合も同じく MemoryStream 化は行いません。
	/// </summary>
	/// <param name="group">
	/// Material.SourcePath 単位でグループ化された BindingVolume の集合。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	ValueTask PrepareAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// 1つの BindingImage を BindingImageProcessor が処理できる状態へ準備します。
	/// 2段階処理の第2段階。
	/// Archive の場合は対象Entry を MemoryStream へコピーして image.TemporaryImageStream へ設定します。
	/// Folder / WorkFolder / Epub の場合は no-op です（CancellationToken確認のみ）。
	/// </summary>
	/// <param name="image">準備対象の BindingImage。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	ValueTask PrepareImageAsync(
		BindingImage image,
		CancellationToken cancellationToken = default);
}
