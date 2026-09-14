using MangaBinder.Helpers;
using SharpCompress.Archives;

namespace MangaBinder.Bindings.Extraction;

/// <summary>
/// Archive（ZIP / RAR / CBZ 等）素材の展開を実行する Extractor です。
/// SharpCompress を使用してアーカイブから画像を抽出し、
/// BindingVolume の TemporaryImageStream に MemoryStream として設定します。
/// </summary>
public class ArchiveMaterialExtractor : IMaterialExtractor
{
	/// <summary>
	/// <see cref="ArchiveMaterialExtractor"/> の新しいインスタンスを初期化します。
	/// </summary>
	public ArchiveMaterialExtractor()
	{
	}

	/// <summary>
	/// Archive グループの素材を展開します。
	/// group.Key（同一物理Archiveファイルパス）のArchiveを1回Openし、
	/// group内の各BindingVolumeに属する画像Entryを抽出して
	/// MemoryStreamに展開し、BindingImage.TemporaryImageStreamに設定します。
	/// </summary>
	/// <param name="group">
	/// Material.SourcePath が同一の Archive ファイルパスである BindingVolume グループ。
	/// 各 BindingVolume は ArchiveEntryPrefix を持ちます。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public async ValueTask ExtractAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// ① group.Key（物理Archiveファイルパス）からArchiveを開く
		using var archive = ArchiveFactory.OpenArchive(new FileInfo(group.Key));

		// ② Archive全体のEntry一覧を取得・フィルタリング・ソート
		var allEntries = archive.Entries
			.Where(e => !e.IsDirectory)
			.Where(e => !ArchiveEntryHelper.IsIgnoredEntry(e.Key))
			.Where(e => e.Key is not null)
			.Select(e => new
			{
				Entry = e,
				Key = e.Key!.Replace('\\', '/').Trim('/'),
			})
			.Where(x => SupportedExtensionHelper.IsImage(Path.GetExtension(x.Key)))
			.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
			.ToList();

		// ③ group内の各BindingVolumeについてループ
		foreach (var volume in group)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// ④ BindingVolume.Material.ArchiveEntryPrefix で対象Entry を抽出
			var prefix = (volume.Material.ArchiveEntryPrefix ?? string.Empty)
				.Replace('\\', '/')
				.Trim('/');

			var targetEntries = allEntries
				.Where(x =>
					string.IsNullOrEmpty(prefix)
					|| x.Key.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
					|| x.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase))
				.ToList();

			// ⑤ 対象Entry ごとに展開処理
			foreach (var item in targetEntries)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var entryKey = item.Key;
				var fileName = Path.GetFileName(entryKey);

				// Entry Stream を開いて MemoryStream へコピー
				MemoryStream memoryStream = new MemoryStream();
				try
				{
					using var entryStream = await item.Entry.OpenEntryStreamAsync(cancellationToken);
					await entryStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

					// Position をリセット
					memoryStream.Position = 0;

					// ⑥ BindingVolume.AddImage() で BindingImage を生成
					var bindingImage = volume.AddImage(entryKey, fileName);

					// ⑦ TemporaryImageStream に MemoryStream を設定（所有権移譲）
					bindingImage.TemporaryImageStream = memoryStream;
				}
				catch
				{
					// 例外が発生した場合は MemoryStream をDisposeしてリークを防ぐ
					memoryStream.Dispose();
					throw;
				}
			}
		}

		// ⑧ Archive は using で自動的にDispose
	}
}
