using MangaBinder.Helpers;
using SharpCompress.Archives;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// Archive（ZIP / RAR / CBZ 等）素材の展開を実行する Extractor です。
/// SharpCompress を使用してアーカイブから画像を抽出し、
/// BindingVolume の TemporaryImageStream に MemoryStream として設定します。
/// 
/// Scoped インスタンスとして使用され、単一の Archive グループを処理する間、
/// 開かれた IArchive インスタンスと BindingImage ↔ IArchiveEntry の対応を保持します。
/// </summary>
public class ArchiveMaterialExtractor : IMaterialExtractor, IDisposable
{
	/// <summary>
	/// 現在処理中の Archive インスタンス（PrepareAsync で開かれたもの）。
	/// Scoped インスタンスの寿命内でのみ保持されます。
	/// </summary>
	private IArchive? currentArchive;

	/// <summary>
	/// BindingImage と IArchiveEntry の対応を保持するディクショナリー。
	/// キーは BindingImage インスタンス、値は対応する IArchiveEntry です。
	/// PrepareAsync で構築され、PrepareImageAsync で参照されます。
	/// </summary>
	private readonly Dictionary<BindingImage, IArchiveEntry> imageEntryMap =
		new Dictionary<BindingImage, IArchiveEntry>();

	/// <summary>
	/// <see cref="ArchiveMaterialExtractor"/> の新しいインスタンスを初期化します。
	/// </summary>
	public ArchiveMaterialExtractor()
	{
	}

	/// <summary>
	/// 素材グループを解析して BindingImage 一覧を生成します。
	/// group.Key のArchiveを1回だけOpen し、各BindingVolumeに属する画像Entry を処理して、
	/// BindingImage を生成し、内部の対応マップへ保存します。
	/// 
	/// この段階ではMemoryStream化を行いません。
	/// PrepareAsync() 完了時点で、Archive由来の全 BindingImage.TemporaryImageStream が null となります。
	/// </summary>
	/// <param name="group">
	/// Material.SourcePath が同一の Archive ファイルパスである BindingVolume グループ。
	/// </param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	public async ValueTask PrepareAsync(
		IGrouping<string, BindingVolume> group,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// group.Key（物理Archiveファイルパス）からArchiveを開く
		// ArchiveFactory.OpenArchive() は同期API なので Task.Run で非同期化
		var archivePath = group.Key;
		this.currentArchive = await Task.Run(
			() => ArchiveFactory.OpenArchive(new FileInfo(archivePath)),
			cancellationToken).ConfigureAwait(false);

		cancellationToken.ThrowIfCancellationRequested();

		// Archive全体のEntry一覧を取得・フィルタリング・ソート
		var allEntries = this.currentArchive.Entries
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

		// group内の各BindingVolumeについてループ
		foreach (var volume in group)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// BindingVolume.Material.ArchiveEntryPrefix で対象Entry を抽出
			var prefix = (volume.Material.ArchiveEntryPrefix ?? string.Empty)
				.Replace('\\', '/')
				.Trim('/');

			var targetEntries = allEntries
				.Where(x =>
					string.IsNullOrEmpty(prefix)
					|| x.Key.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
					|| x.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase))
				.ToList();

			// 対象Entry ごとに処理
			foreach (var item in targetEntries)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var entryKey = item.Key;
				var fileName = Path.GetFileName(entryKey);

				// BindingVolume.AddImage() で BindingImage を生成
				var bindingImage = volume.AddImage(entryKey, fileName);

				// BindingImage と IArchiveEntry の対応を保存
				this.imageEntryMap[bindingImage] = item.Entry;

				// この段階では TemporaryImageStream は null のまま
			}
		}
	}

	/// <summary>
	/// 1つの BindingImage を BindingImageProcessor が処理できる状態へ準備します。
	/// 対応するArchiveEntryをMemoryStreamへコピーし、image.TemporaryImageStreamへ設定します。
	/// 
	/// SkipImageProcessing == true の場合は何も行いません。
	/// </summary>
	/// <param name="image">準備対象の BindingImage。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	/// <exception cref="InvalidOperationException">対応するArchiveEntryが見つからない場合。</exception>
	public async ValueTask PrepareImageAsync(
		BindingImage image,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// SkipImageProcessing == true なら何もしない
		if (image.SkipImageProcessing)
		{
			return;
		}

		// 対応するArchiveEntryを取得
		if (!this.imageEntryMap.TryGetValue(image, out var entry))
		{
			throw new InvalidOperationException(
				$"BindingImage に対応する ArchiveEntry が見つかりません。" +
				$"FileName: {image.FileName}");
		}

		// MemoryStream へコピー
		var memoryStream = new MemoryStream();
		try
		{
			using var entryStream = await entry.OpenEntryStreamAsync(cancellationToken);
			await entryStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

			// Position をリセット
			memoryStream.Position = 0;

			// image.TemporaryImageStream へ設定（所有権移譲）
			image.TemporaryImageStream = memoryStream;
		}
		catch
		{
			// 例外が発生した場合は MemoryStream をDisposeしてリークを防ぐ
			memoryStream.Dispose();
			throw;
		}
	}

	/// <summary>
	/// リソースを解放します。
	/// 保持している Archive と対応マップをクリアします。
	/// </summary>
	public void Dispose()
	{
		// 保持している Archive を Dispose
		this.currentArchive?.Dispose();
		this.currentArchive = null;

		// 対応マップをクリア
		this.imageEntryMap.Clear();
	}
}
