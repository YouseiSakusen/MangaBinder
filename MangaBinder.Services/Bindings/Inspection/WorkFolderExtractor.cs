using MangaBinder.Helpers;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 既存 Work 巻フォルダを入力元として扱う Extractor です。
/// Work フォルダ直下に存在する対応画像ファイルを列挙し、
/// group 内の各 BindingVolume に BindingImage を生成します。
/// </summary>
public class WorkFolderExtractor : IMaterialExtractor
{
	/// <summary>
	/// <see cref="WorkFolderExtractor"/> の新しいインスタンスを初期化します。
	/// </summary>
	public WorkFolderExtractor()
	{
	}

	/// <summary>
	/// Work 巻フォルダグループの素材を展開し、BindingImage を生成します。
	/// group.Key（Work フォルダパス）直下に存在する画像ファイルを列挙し、
	/// group 内の各 BindingVolume に対して BindingImage を生成・追加します。
	/// </summary>
	/// <param name="group">
	/// EffectiveSourcePath が同一の Work フォルダパスである BindingVolume グループ。
	/// group.Key は Work フォルダの実ファイルシステムパスです。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public ValueTask PrepareAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var folderPath = group.Key;

		// ① フォルダ直下に存在するファイルを列挙（再帰検索はしない）
		// SupportedExtensionHelper.IsImage() で対応画像だけを抽出
		var imageFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
			.Where(filePath => SupportedExtensionHelper.IsImage(Path.GetExtension(filePath)))
			.OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
			.ToList();

		// ② group 内の各 BindingVolume について処理
		foreach (var volume in group)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// ③ 各画像ファイルについて BindingImage を生成
			foreach (var imageFilePath in imageFiles)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var fileName = Path.GetFileName(imageFilePath);

				// ④ BindingVolume.AddImage() で BindingImage を生成・追加
				// sourceImagePath = Work 上に存在する画像ファイルのフルパス
				// fileName = ファイル名
				volume.AddImage(imageFilePath, fileName);
			}
		}

		return default;
	}

	/// <summary>
	/// 1つの BindingImage を準備します。
	/// WorkFolder 素材の場合は no-op です。CancellationToken の確認のみを行います。
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
