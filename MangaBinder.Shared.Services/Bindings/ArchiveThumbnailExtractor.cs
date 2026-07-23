using MangaBinder.Helpers;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace MangaBinder.Bindings;

/// <summary>
/// SharpCompress を使用してアーカイブからサムネイル画像を抽出するクラスです。
/// </summary>
public class ArchiveThumbnailExtractor : IThumbnailExtractor
{
    /// <inheritdoc/>
    public async ValueTask<ThumbnailExtractionResult> GetThumbnailImageAsync(string path, CancellationToken ct)
    {
        IArchive? archive = null;
        try
        {
            archive = ArchiveFactory.OpenArchive(new FileInfo(path));
        }
        catch
        {
            archive?.Dispose();
            return new ThumbnailExtractionResult { Status = ExtractionStatus.UnsupportedFormat };
        }

        using (archive)
        {
            try
            {
                var entries = archive.Entries
                    .Where(e => !e.IsDirectory)
                    .Where(e => !ArchiveEntryHelper.IsIgnoredEntry(e.Key))
                    .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase);

                var hasNestedArchive = false;

                foreach (var entry in entries)
                {
                    var ext = Path.GetExtension(entry.Key);
                    if (SupportedExtensionHelper.IsImage(ext))
                    {
                        var ms = new MemoryStream();
                        using var entryStream = entry.OpenEntryStream();
                        await entryStream.CopyToAsync(ms, ct);
                        ms.Position = 0;
                        return new ThumbnailExtractionResult
                        {
                            Status = ExtractionStatus.Success,
                            ImageStream = ms,
                        };
                    }

                    if (!hasNestedArchive && SupportedExtensionHelper.IsArchive(ext))
                        hasNestedArchive = true;
                }

                return new ThumbnailExtractionResult
                {
                    Status = hasNestedArchive ? ExtractionStatus.NestedArchiveFound : ExtractionStatus.NoImageFound,
                };
            }
            catch (InvalidFormatException)
            {
                return new ThumbnailExtractionResult { Status = ExtractionStatus.UnsupportedFormat };
            }
        }
    }
}
