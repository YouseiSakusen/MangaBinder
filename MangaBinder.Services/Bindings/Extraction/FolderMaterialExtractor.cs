namespace MangaBinder.Bindings.Extraction;

/// <summary>
/// フォルダ素材の展開を実行する Extractor です。
/// フォルダから画像を列挙し、BindingVolume の Work フォルダにコピーします。
/// </summary>
public class FolderMaterialExtractor : IMaterialExtractor
{
	/// <summary>
	/// <see cref="FolderMaterialExtractor"/> の新しいインスタンスを初期化します。
	/// </summary>
	public FolderMaterialExtractor()
	{
	}

	/// <summary>
	/// フォルダグループの素材を展開します。
	/// </summary>
	/// <param name="group">
	/// Material.SourcePath が同一のフォルダパスである BindingVolume グループ。
	/// 各 BindingVolume は ArchiveEntryPrefix を持たず、ItemType.Folder です。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public ValueTask ExtractAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default)
	{
		// 現段階では素材展開処理は未実装
		// 将来の実装段階で以下の処理を追加します：
		// - group.Key（フォルダパス）から画像ファイルを列挙
		// - 画像を各 BindingVolume の WorkFolderPath にコピー
		// - BindingImage を生成・Images に追加
		// - 画像処理（変換・リサイズ等）を実行

		return default;
	}
}
