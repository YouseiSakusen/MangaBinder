using MangaBinder.Helpers;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 物理フォルダ素材の展開を実行する Extractor です。
/// フォルダ直下に存在する対応画像ファイルを列挙し、
/// group 内の各 BindingVolume に BindingImage を生成します。
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
	/// 物理フォルダグループの素材を展開し、BindingImage を生成します。
	/// group.Key（物理フォルダパス）直下に存在する画像ファイルを列挙し、
	/// group 内の各 BindingVolume に対して BindingImage を生成・追加します。
	/// </summary>
	/// <param name="group">
	/// EffectiveSourcePath が同一の物理フォルダパスである BindingVolume グループ。
	/// group.Key はフォルダの実ファイルシステムパスです。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public ValueTask PrepareAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var folderPath = group.Key;

		// ① フォルダ直下のサブフォルダ有無を確認（再帰検索はしない）
		var hasSubFolder = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly).Length > 0;

		// ② フォルダ直下に存在するファイルを列挙（再帰検索はしない）
		// SupportedExtensionHelper.IsImage() で対応画像だけを抽出
		var imageFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
			.Where(filePath => SupportedExtensionHelper.IsImage(Path.GetExtension(filePath)))
			.OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
			.ToList();

		// ③ group 内の各 BindingVolume について処理
		foreach (var volume in group)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// ④ HasSubFolder を設定（同じフォルダグループなので一度の判定で全て設定）
			volume.HasSubFolder = hasSubFolder;

			// ⑤ 各画像ファイルについて BindingImage を生成
			foreach (var imageFilePath in imageFiles)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var fileName = Path.GetFileName(imageFilePath);

				// ⑥ BindingVolume.AddImage() で BindingImage を生成・追加
				// sourceImagePath = 物理画像ファイルのフルパス
				// fileName = ファイル名
				volume.AddImage(imageFilePath, fileName);
			}
		}

		return default;
	}

	/// <summary>
	/// 1つの BindingImage を準備します。
	/// Folder 素材の場合は no-op です。CancellationToken の確認のみを行います。
	/// </summary>
	/// <param name="image">準備対象の BindingImage。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public ValueTask PrepareImageAsync(
		BindingImage image,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return default;
	}
}
