using NetVips;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 見開き分割処理で、BindingImage 1件の実画像を Trim → 左右分割 → 保存する処理クラスです。
/// ステージング領域の管理や失敗時のフォールバックは担当しません。
/// </summary>
public class SplitImageProcessor
{
	/// <summary>
	/// BindingImage 1件を分割し、出力ディレクトリへ保存した分割後 BindingImage 2件をページ順で返します。
	/// 入力 BindingImage.SplitSettings の値のみを使用します。
	/// </summary>
	/// <param name="image">分割対象の BindingImage。FilePath と SplitSettings が設定済みである必要があります。</param>
	/// <param name="outputDirectory">今回の出力先ディレクトリ。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>ページ順（-1 → -2）の分割後 BindingImage 2件。</returns>
	/// <exception cref="InvalidOperationException">FilePath / SplitSettings 未設定、または Trim / SplitOffset が画像に対して不正な場合。</exception>
	public async ValueTask<IReadOnlyList<BindingImage>> ProcessAsync(
		BindingImage image,
		string outputDirectory,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(image);
		ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrEmpty(image.FilePath))
		{
			throw new InvalidOperationException("BindingImage.FilePath が設定されていません。");
		}

		var settings = image.SplitSettings
			?? throw new InvalidOperationException("BindingImage.SplitSettings が設定されていません。");

		var trimLeft = settings.TrimLeft.Value;
		var trimTop = settings.TrimTop.Value;
		var trimRight = settings.TrimRight.Value;
		var trimBottom = settings.TrimBottom.Value;
		var splitOffset = settings.SplitOffset.Value;
		var sourcePath = image.FilePath;

		IReadOnlyList<BindingImage> splitImages = image.CreateSplitImages();
		var createdPaths = new List<string>();
		var succeeded = false;

		try
		{
			await Task.Run(() =>
			{
				using var source = Image.NewFromFile(sourcePath);

				var effectiveLeft = trimLeft;
				var effectiveTop = trimTop;
				var effectiveRight = source.Width - trimRight;
				var effectiveBottom = source.Height - trimBottom;
				var trimmedWidth = effectiveRight - effectiveLeft;
				var trimmedHeight = effectiveBottom - effectiveTop;

				if (trimLeft < 0 || trimTop < 0 || trimRight < 0 || trimBottom < 0
					|| trimmedWidth <= 0 || trimmedHeight <= 0)
				{
					throw new InvalidOperationException(
						$"Trim 設定が画像サイズ({source.Width}x{source.Height})に対して不正です: {image.FileName}");
				}

				var splitX = (trimmedWidth / 2) + splitOffset;
				if (splitX <= 0 || splitX >= trimmedWidth)
				{
					throw new InvalidOperationException(
						$"SplitOffset が Trim 後の画像範囲外です。trimmedWidth={trimmedWidth}, splitX={splitX}: {image.FileName}");
				}

				using var trimmed = source.Crop(effectiveLeft, effectiveTop, trimmedWidth, trimmedHeight);

				foreach (var splitImage in splitImages)
				{
					cancellationToken.ThrowIfCancellationRequested();

					var isLeft = splitImage.SplitSide == SplitSide.Left;
					var x = isLeft ? 0 : splitX;
					var width = isLeft ? splitX : trimmedWidth - splitX;
					var outputPath = Path.Combine(outputDirectory, splitImage.FileName);

					if (File.Exists(outputPath))
					{
						throw new IOException($"出力先ファイルが既に存在します: {outputPath}");
					}

					using var part = trimmed.Crop(x, 0, width, trimmedHeight);
					createdPaths.Add(outputPath);
					part.WriteToFile(outputPath);

					splitImage.FilePath = outputPath;
					splitImage.Width = width;
					splitImage.Height = trimmedHeight;
					splitImage.ProcessStatus = BindingImageProcessStatus.Succeeded;
					splitImage.ProcessErrorMessage = null;
				}
			}, cancellationToken);

			succeeded = true;
			return splitImages;
		}
		finally
		{
			if (!succeeded)
			{
				foreach (var path in createdPaths)
				{
					try
					{
						if (File.Exists(path))
						{
							File.Delete(path);
						}
					}
					catch (IOException)
					{
					}
					catch (UnauthorizedAccessException)
					{
					}
				}

				foreach (var splitImage in splitImages)
				{
					splitImage.Dispose();
				}
			}
		}
	}
}
