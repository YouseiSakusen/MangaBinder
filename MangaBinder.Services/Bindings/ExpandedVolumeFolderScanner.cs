using System.IO;
using MangaBinder.Helpers;

namespace MangaBinder.Bindings;

/// <summary>
/// 展開先の巻フォルダを走査した結果を表します。
/// </summary>
/// <param name="Path">巻フォルダのフルパス。</param>
/// <param name="Name">巻フォルダ名。</param>
/// <param name="FileCount">直下の対応画像ファイル数。</param>
/// <param name="TotalBytes">直下の対応画像ファイルの合計サイズ（バイト）。</param>
/// <param name="VolumeNumber">フォルダ名から取得した巻番号。</param>
public sealed record ScannedExpandedFolder(string Path, string Name, int FileCount, long TotalBytes, decimal VolumeNumber);

/// <summary>
/// 展開先作品フォルダ直下の巻フォルダを走査し、<see cref="ExpandedVolume"/> に登録できる形の情報を取得します。
/// 初期化時・展開済み巻追加Dialog表示前・製本前確認の展開完了後で同じ走査規則を使用します。
/// </summary>
public static class ExpandedVolumeFolderScanner
{
	/// <summary>
	/// 作品フォルダ直下の巻フォルダをすべて走査します。
	/// 巻番号が単一で取得できないフォルダ、および対応画像が0件のフォルダは含みません。
	/// </summary>
	/// <param name="seriesFolderPath">展開先作品フォルダのパス。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>走査結果。作品フォルダが存在しない場合は空。</returns>
	public static List<ScannedExpandedFolder> ScanSeriesFolder(
		string seriesFolderPath,
		CancellationToken cancellationToken = default)
	{
		var folders = new List<ScannedExpandedFolder>();
		if (!Directory.Exists(seriesFolderPath))
		{
			return folders;
		}

		foreach (var folderPath in Directory.GetDirectories(seriesFolderPath))
		{
			cancellationToken.ThrowIfCancellationRequested();

			var scanned = ScanFolder(folderPath);
			if (scanned is not null)
			{
				folders.Add(scanned);
			}
		}

		return folders;
	}

	/// <summary>
	/// 1つの巻フォルダを走査します。
	/// </summary>
	/// <param name="folderPath">巻フォルダのパス。</param>
	/// <returns>走査結果。巻番号が単一で取得できない、または対応画像が0件の場合は null。</returns>
	public static ScannedExpandedFolder? ScanFolder(string folderPath)
	{
		var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath));
		var parseResult = VolumeNumberHelper.Parse(name, VolumeNumberSourceType.Folder);
		if (parseResult.Kind != VolumeNumberParseKind.Single || !parseResult.SingleVolume.HasValue)
		{
			return null;
		}

		if (!Directory.Exists(folderPath))
		{
			return null;
		}

		var fileCount = 0;
		var totalBytes = 0L;
		foreach (var file in new DirectoryInfo(folderPath).EnumerateFiles("*", SearchOption.TopDirectoryOnly))
		{
			if (SupportedExtensionHelper.IsImage(file.Extension))
			{
				fileCount++;
				totalBytes += file.Length;
			}
		}

		if (fileCount == 0)
		{
			return null;
		}

		return new ScannedExpandedFolder(folderPath, name, fileCount, totalBytes, parseResult.SingleVolume.Value);
	}
}
