using MangaBinder.Settings;

namespace MangaBinder.Series;

/// <summary>
/// 作品の保存処理を実行するマネージャーのインターフェースです。
/// 新規作品保存と既存作品更新の処理を分離し、将来的な処理の詳細化に対応します。
/// </summary>
public interface ISeriesSaveManager
{
	/// <summary>
	/// 作品の保存処理を実行します。
	/// 新規作品保存と既存作品更新の処理を共通インターフェースで実行するため、
	/// 実装クラスで <see cref="MangaSeries.SeriesId"/> や <see cref="MangaSeries.IsWork"/> 値から処理内容を判定してください。
	/// </summary>
	/// <param name="editingSeries">編集中の作品（編集用クローン）。既存正式作品の保存では、このインスタンスを作業対象とします。</param>
	/// <param name="originalSeries">編集開始時の正本。既存正式作品の保存でのみ、比較用・参照用の読み取り専用として使用します。</param>
	/// <param name="materialFiles">追加された素材ファイル。</param>
	/// <param name="selectedMaterialSourceFolder">素材の移動先フォルダ。</param>
	/// <param name="thumbnailBytes">新しいサムネイルのバイト列。</param>
	/// <returns>保存処理の結果（作品情報と移動失敗素材を含む）。</returns>
	ValueTask<SeriesSaveResult> SaveAsync(
		MangaSeries editingSeries,
		MangaSeries? originalSeries,
		IReadOnlyList<MaterialFile> materialFiles,
		SourceFolder? selectedMaterialSourceFolder,
		byte[]? thumbnailBytes);
}
