using System.IO;
using MangaBinder.Binding.Inspection;
using MangaBinder.Settings;

namespace MangaBinder.Binding.Prepress;

/// <summary>
/// 巻フォルダ内ファイルを列挙し、サムネイル一覧アイテムを生成するクラスです。
/// </summary>
public sealed class VolumeThumbnailLoader
{
	private readonly VolumeFileNameNormalizer normalizer;
	private readonly VolumeThumbnailImageProcessor imageProcessor;

	/// <summary>
	/// <see cref="VolumeThumbnailLoader"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="normalizer">ファイル名桁揃えリネーム担当。</param>
	/// <param name="imageProcessor">サムネイル生成担当。</param>
	public VolumeThumbnailLoader(
		VolumeFileNameNormalizer normalizer,
		VolumeThumbnailImageProcessor imageProcessor)
	{
		this.normalizer = normalizer;
		this.imageProcessor = imageProcessor;
	}

	/// <summary>
	/// ファイル名桁揃えリネーム前処理を実行します。
	/// </summary>
	/// <param name="volume">処理対象巻の検査結果。</param>
	/// <returns>リネーム結果の一覧。エラーがある場合はその情報を含みます。</returns>
	public IReadOnlyList<NormalizationResult> NormalizeFileNames(VolumeInspectionResult volume)
	{
		if (!volume.HasIrregularFileNameLength)
			return [];

		return this.normalizer.Normalize(volume.WorkVolumeFolderPath);
	}

	/// <summary>
	/// 巻フォルダ直下の全ファイルを列挙し、サムネイル一覧アイテムを生成します。
	/// </summary>
	/// <param name="folderPath">巻フォルダのパス。</param>
	/// <returns><see cref="VolumeThumbnailItem"/> の一覧。</returns>
	public IReadOnlyList<VolumeThumbnailItem> LoadItems(string folderPath)
	{
		if (!Directory.Exists(folderPath))
			return [];

		var files = Directory.GetFiles(folderPath)
			.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var items = new List<VolumeThumbnailItem>(files.Length);

		foreach (var filePath in files)
		{
			var ext = Path.GetExtension(filePath);
			var fileName = Path.GetFileName(filePath);
			var isSupported = SupportedExtensionHelper.IsImage(ext)
				&& !SupportedExtensionHelper.RequiresConversion(ext);

			if (isSupported)
			{
				var thumbnailBytes = this.imageProcessor.GenerateThumbnail(filePath);
				var hasError = thumbnailBytes is null;

				items.Add(new VolumeThumbnailItem
				{
					FilePath = filePath,
					FileName = fileName,
					ThumbnailBytes = thumbnailBytes,
					FallbackResourceKey = hasError ? VolumeThumbnailImageProcessor.LoadFailedResource : null,
					IsChecked = !hasError,
					HasError = hasError,
					IsUnsupported = false,
				});
			}
			else
			{
				items.Add(new VolumeThumbnailItem
				{
					FilePath = filePath,
					FileName = fileName,
					ThumbnailBytes = null,
					FallbackResourceKey = VolumeThumbnailImageProcessor.UnsupportedFormatResource,
					IsChecked = false,
					HasError = false,
					IsUnsupported = true,
				});
			}
		}

		return items;
	}
}
