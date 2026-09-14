namespace MangaBinder.Bindings.Extraction;

/// <summary>
/// EPUB 素材の展開を実行する Extractor です。
/// EPUB ファイルから画像を抽出し、BindingVolume の Work フォルダに配置します。
/// </summary>
public class EpubMaterialExtractor : IMaterialExtractor
{
	/// <summary>
	/// <see cref="EpubMaterialExtractor"/> の新しいインスタンスを初期化します。
	/// </summary>
	public EpubMaterialExtractor()
	{
	}

	/// <summary>
	/// EPUB グループの素材を展開します。
	/// </summary>
	/// <param name="group">
	/// Material.SourcePath が同一の EPUB ファイルパスである BindingVolume グループ。
	/// 各 BindingVolume は ArchiveEntryPrefix を持たず、ItemType.Epub です。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public ValueTask ExtractAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default)
	{
		// 現段階では素材展開処理は未実装
		// 将来の実装段階で以下の処理を追加します：
		// - group.Key（EPUB ファイルパス）から EPUB を解析
		// - 画像リソースを列挙・抽出
		// - 抽出画像を各 BindingVolume の WorkFolderPath に配置
		// - BindingImage を生成・Images に追加
		// - 画像処理（変換・リサイズ等）を実行

		return default;
	}
}
