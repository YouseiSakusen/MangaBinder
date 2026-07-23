using MangaBinder.Helpers;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using System.Xml.Linq;
using ZLogger;

namespace MangaBinder.Bindings;

/// <summary>
/// EPUB 形式のファイルからサムネイル画像を抽出するクラスです。
/// </summary>
public class EpubThumbnailExtractor : IThumbnailExtractor
{
    private readonly ILogger<EpubThumbnailExtractor> logger;

    /// <summary>
    /// <see cref="EpubThumbnailExtractor"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="logger">ロガー。</param>
    public EpubThumbnailExtractor(ILogger<EpubThumbnailExtractor> logger)
        => this.logger = logger;

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
            this.logger.ZLogError($"アーカイブを開けませんでした: {path}");
            return new ThumbnailExtractionResult { Status = ExtractionStatus.UnsupportedFormat };
        }

        using (archive)
        {
            var entries = archive.Entries
                .Where(e => !e.IsDirectory)
                .Where(e => !ArchiveEntryHelper.IsIgnoredEntry(e.Key))
                .ToList();

            // Step 1: "cover" ファイルの直接検索
            this.logger.ZLogInformation($"Step 1 開始: ファイル名が \"cover\" の画像を検索します。");
            var coverEntry = entries.FirstOrDefault(e =>
                string.Equals(Path.GetFileNameWithoutExtension(e.Key), "cover", StringComparison.OrdinalIgnoreCase)
                && SupportedExtensionHelper.IsImage(Path.GetExtension(e.Key)));

            if (coverEntry is not null)
            {
                this.logger.ZLogInformation($"Step 1 成功: cover 画像を発見しました。エントリ={coverEntry.Key}");
                return await ExtractEntryAsync(coverEntry, ct);
            }

            this.logger.ZLogInformation($"Step 1 失敗: cover 画像が見つかりませんでした。Step 2 を開始します。");

            // Step 2: OPFファイルのパース
            this.logger.ZLogInformation($"Step 2 開始: OPF ファイルを検索してパースします。");
            var opfEntry = entries.FirstOrDefault(e =>
                string.Equals(Path.GetExtension(e.Key), ".opf", StringComparison.OrdinalIgnoreCase));

            if (opfEntry is not null)
            {
                this.logger.ZLogInformation($"Step 2: OPF ファイルを発見しました。エントリ={opfEntry.Key}");
                var coverHref = await ParseCoverHrefFromOpfAsync(opfEntry, ct);

                if (coverHref is not null)
                {
                    this.logger.ZLogInformation($"Step 2: OPF から cover の href を取得しました。href={coverHref}");
                    var hrefFileName = Path.GetFileName(coverHref);
                    var opfCoverEntry = entries.FirstOrDefault(e =>
                        (e.Key?.EndsWith(hrefFileName, StringComparison.OrdinalIgnoreCase) ?? false)
                        && SupportedExtensionHelper.IsImage(Path.GetExtension(e.Key)));

                    if (opfCoverEntry is not null)
                    {
                        this.logger.ZLogInformation($"Step 2 成功: OPF 参照の cover 画像を発見しました。エントリ={opfCoverEntry.Key}");
                        return await ExtractEntryAsync(opfCoverEntry, ct);
                    }

                    this.logger.ZLogWarning($"Step 2: href に対応するエントリが見つかりませんでした。href={coverHref}");
                }
                else
                {
                    this.logger.ZLogWarning($"Step 2: OPF ファイルに cover 情報が含まれていませんでした。");
                }
            }
            else
            {
                this.logger.ZLogWarning($"Step 2: OPF ファイルが見つかりませんでした。");
            }

            this.logger.ZLogInformation($"Step 2 失敗: Step 3 (フォールバック) を開始します。");

            // Step 3: フォールバック（名前順の最初の画像）
            this.logger.ZLogInformation($"Step 3 開始: 全画像エントリをキー昇順でソートし、最初の1枚を採用します。");
            var imageEntries = entries
                .Where(e => SupportedExtensionHelper.IsImage(Path.GetExtension(e.Key)))
                .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (imageEntries.Count > 0)
            {
                var firstImage = imageEntries[0];
                this.logger.ZLogInformation($"Step 3 成功: フォールバック画像を採用しました。エントリ={firstImage.Key}");
                return await ExtractEntryAsync(firstImage, ct);
            }

            // 画像が1枚もない場合は ArchiveExtractor に準拠してネストアーカイブ有無を確認
            var hasNestedArchive = entries.Any(e => SupportedExtensionHelper.IsArchive(Path.GetExtension(e.Key)));
            this.logger.ZLogWarning($"Step 3 失敗: 画像が見つかりませんでした。NestedArchive={hasNestedArchive}");
            return new ThumbnailExtractionResult
            {
                Status = hasNestedArchive ? ExtractionStatus.NestedArchiveFound : ExtractionStatus.NoImageFound,
            };
        }
    }

    /// <summary>
    /// OPF エントリから cover 画像の href を非同期で解析して返します。
    /// </summary>
    private static async Task<string?> ParseCoverHrefFromOpfAsync(SharpCompress.Archives.IArchiveEntry opfEntry, CancellationToken ct)
    {
        using var opfStream = opfEntry.OpenEntryStream();
        using var ms = new MemoryStream();
        await opfStream.CopyToAsync(ms, ct);
        ms.Position = 0;

        XDocument doc;
        try
        {
            doc = XDocument.Load(ms);
        }
        catch
        {
            return null;
        }

        // <item properties="cover-image"> を検索
        var coverItem = doc.Descendants()
            .FirstOrDefault(e =>
                e.Name.LocalName == "item"
                && (string?)e.Attribute("properties") == "cover-image");

        if (coverItem is not null)
            return (string?)coverItem.Attribute("href");

        // <meta name="cover" content="id"> を検索し、対応する <item> の href を取得
        var metaCover = doc.Descendants()
            .FirstOrDefault(e =>
                e.Name.LocalName == "meta"
                && (string?)e.Attribute("name") == "cover");

        if (metaCover is not null)
        {
            var coverId = (string?)metaCover.Attribute("content");
            if (coverId is not null)
            {
                var itemById = doc.Descendants()
                    .FirstOrDefault(e =>
                        e.Name.LocalName == "item"
                        && (string?)e.Attribute("id") == coverId);

                if (itemById is not null)
                    return (string?)itemById.Attribute("href");
            }
        }

        return null;
    }

    /// <summary>
    /// アーカイブエントリを <see cref="MemoryStream"/> にコピーして <see cref="ThumbnailExtractionResult"/> として返します。
    /// </summary>
    private async ValueTask<ThumbnailExtractionResult> ExtractEntryAsync(SharpCompress.Archives.IArchiveEntry entry, CancellationToken ct)
    {
        this.logger.ZLogInformation($"画像を抽出しています: {entry.Key}");
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
}
