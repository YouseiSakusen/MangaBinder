using MangaBinder.Bindings;
using MangaBinder.Settings;
using SharpCompress.Archives;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// アーカイブファイルからページを列挙する <see cref="IVolumeExtractor"/> 実装です。
/// </summary>
public sealed class ArchiveVolumeExtractor : IVolumeExtractor
{
	/// <inheritdoc />
	public async ValueTask ExtractPagesAsync(
		BindingSourceVolume volume,
		Func<BindingPageSource, ValueTask> onPageAsync,
		CancellationToken cancellationToken = default)
	{
		using var archive = ArchiveFactory.OpenArchive(new FileInfo(volume.SourcePath));

		var prefix = (volume.ArchiveEntryPrefix ?? string.Empty)
			.Replace('\\', '/')
			.Trim('/');

		var entries = archive.Entries
			.Where(e => !e.IsDirectory)
			.Where(e => !ArchiveEntryHelper.IsIgnoredEntry(e.Key))
			.Where(e => e.Key is not null)
			.Select(e => new
			{
				Entry = e,
				Key = e.Key!.Replace('\\', '/').Trim('/'),
			})
			.Where(x =>
				string.IsNullOrEmpty(prefix)
				|| x.Key.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
				|| x.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase))
			.Where(x => SupportedExtensionHelper.IsImage(Path.GetExtension(x.Key)))
			.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
			.Select(x => x.Entry)
			.ToList();

		foreach (var entry in entries)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var captured = entry;
			var page = new BindingPageSource
			{
				SourceName = Path.GetFileName(captured.Key!),
				Extension = Path.GetExtension(captured.Key!),
				OpenStreamAsync = async ct =>
				{
					var ms = new MemoryStream();
					using var entryStream = captured.OpenEntryStream();
					await entryStream.CopyToAsync(ms, ct);
					ms.Position = 0;
					return ms;
				},
			};

			await onPageAsync(page);
		}
	}
}
