using MangaBinder.Helpers;
using SharpCompress.Archives;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// アーカイブファイルからページを列挙する <see cref="IVolumeExtractor"/> 実装です。
/// SharpCompress の同期処理をこのクラス内で Task.Run により非同期化し、
/// WorkFolderBuilder が SharpCompress の実装詳細を知らないようにカプセル化します。
/// </summary>
public sealed class ArchiveVolumeExtractor : IVolumeExtractor
{
	/// <inheritdoc />
	public async ValueTask ExtractPagesAsync(
		BindingSourceVolume volume,
		Func<BindingPageSource, ValueTask> onPageAsync,
		CancellationToken cancellationToken = default)
	{
		// SharpCompress の同期処理を Task.Run で非同期化
		// archive を open のまま保持し、ページごとに処理することで、
		// 全画像を事前にメモリに読み込まない構造
		await Task.Run(
			async () =>
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
					.ToList();

				// ページごとに処理（全画像を先読みしない）
				foreach (var item in entries)
				{
					cancellationToken.ThrowIfCancellationRequested();

					var captured = item;
					var page = new BindingPageSource
					{
						SourceName = Path.GetFileName(captured.Key),
						Extension = Path.GetExtension(captured.Key),
						OpenStreamAsync = async ct =>
						{
							// このメソッドは Task.Run 内で実行されるため、
							// archive はまだ open 状態であり、OpenEntryStream() が呼べる
							var ms = new MemoryStream();
							using var entryStream = captured.Entry.OpenEntryStream();
							await entryStream.CopyToAsync(ms, ct);
							ms.Position = 0;
							return ms;
						},
					};

					await onPageAsync(page);
				}
			},
			cancellationToken).ConfigureAwait(false);
	}
}
