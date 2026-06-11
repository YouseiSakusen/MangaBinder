using MangaBinder.Settings;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 実フォルダからページを列挙する <see cref="IVolumeExtractor"/> 実装です。
/// </summary>
public sealed class FolderVolumeExtractor : IVolumeExtractor
{
	/// <inheritdoc />
	public async ValueTask ExtractPagesAsync(
		BindingSourceVolume volume,
		Func<BindingPageSource, ValueTask> onPageAsync,
		CancellationToken cancellationToken = default)
	{
		var files = Directory.GetFiles(volume.SourcePath)
			.Where(f => SupportedExtensionHelper.IsImage(Path.GetExtension(f)))
			.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

		foreach (var filePath in files)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var captured = filePath;
			var page = new BindingPageSource
			{
				SourceName = Path.GetFileName(captured),
				Extension = Path.GetExtension(captured),
				OpenStreamAsync = ct =>
				{
					var stream = new FileStream(captured, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
					return ValueTask.FromResult<Stream>(stream);
				},
			};

			await onPageAsync(page);
		}
	}
}
