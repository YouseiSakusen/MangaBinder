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
	/// <param name="editingSeries">編集中の作品。</param>
	/// <param name="originalSeries">編集開始時の DeepCopy。</param>
	/// <param name="materialFiles">追加された素材ファイル。</param>
	/// <param name="selectedMaterialSourceFolder">素材の移動先フォルダ。</param>
	/// <param name="thumbnailBytes">新しいサムネイルのバイト列。</param>
	/// <returns>保存後の作品インスタンス。</returns>
	ValueTask<MangaSeries> SaveAsync(
		MangaSeries editingSeries,
		MangaSeries? originalSeries,
		IReadOnlyList<MaterialFile> materialFiles,
		SourceFolder? selectedMaterialSourceFolder,
		byte[]? thumbnailBytes);
}
