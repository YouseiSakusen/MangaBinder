using MangaBinder.Bindings;
using MangaBinder.Jobs.Contexts;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;
using NetVips;
using ZLogger;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 作品のサムネイル候補ファイルを特定するクラスです。
/// </summary>
public class ThumbnailCreator
{
	/// <summary>サムネイルファイルの拡張子。</summary>
	public const string ThumbnailExtension = ".jpg";

	/// <summary>Worker 実行コンテキスト。</summary>
	private readonly WorkerContext workerContext;

	/// <summary>エクストラクターファクトリ。</summary>
	private readonly SeriesExtractorFactory extractorFactory;

	/// <summary>画像プロセッサー。</summary>
	private readonly IThumbnailImageProcessor imageProcessor;

	/// <summary>ロガー。</summary>
	private readonly ILogger<ThumbnailCreator> logger;

	/// <summary>
	/// <see cref="ThumbnailCreator"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="workerContext">Worker 実行コンテキスト。</param>
	/// <param name="extractorFactory">エクストラクターファクトリ。</param>
	/// <param name="imageProcessor">画像プロセッサー。</param>
	/// <param name="logger">ロガー。</param>
	public ThumbnailCreator(WorkerContext workerContext, SeriesExtractorFactory extractorFactory, IThumbnailImageProcessor imageProcessor, ILogger<ThumbnailCreator> logger)
	{
		this.workerContext = workerContext;
		this.extractorFactory = extractorFactory;
		this.imageProcessor = imageProcessor;
		this.logger = logger;
	}

	/// <summary>
	/// 指定された作品のサムネイル候補ファイルを探索し、見つかった場合は
	/// <see cref="MangaSeries.ThumbnailFileName"/> にパスを設定します。
	/// <para>
	/// 探索順は <see cref="FolderRole.Binding"/> のソースを優先し、
	/// 見つからない場合は <see cref="FolderRole.Material"/> のソースを探索します。
	/// </para>
	/// </summary>
	/// <param name="series">探索対象の作品。</param>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>候補ファイルが見つかった場合は <c>true</c>。</returns>
	public async ValueTask<ThumbnailCreationResult> CreateAsync(MangaSeries series, bool skipThumbnailSizeLimit, CancellationToken ct = default)
	{
		// Binding を優先し、次に Material を探索する
		var orderedSources = series.Sources
			.OrderBy(s => s.Role == FolderRole.Binding ? 0 : 1);

		var anyLimitExceeded = false;
		var anyNestedArchive = false;

		foreach (var source in orderedSources)
		{
			var candidate = this.FindFirstFile(source.Path);
			if (candidate is null)
				continue;

			var fileSize = new FileInfo(candidate).Length;
			if (!skipThumbnailSizeLimit &&
				fileSize > this.workerContext.ThumbnailExtractLimitFileSizeBytes)
			{
				anyLimitExceeded = true;
				this.logger.ZLogWarning($"ファイルサイズがリミットを超えているためスキップします: {candidate} ({fileSize:N0} bytes)");
				continue;
			}

			try
			{
				var fileType = SupportedExtensionHelper.GetFileType(Path.GetExtension(candidate))!.Value;

				Stream? sourceStream = null;

				switch (fileType)
				{
					case FileType.Image:
						sourceStream = File.OpenRead(candidate);
						break;

					case FileType.Archive:
					case FileType.Epub:
						var extractor = this.extractorFactory.GetExtractor(fileType);
						var extractorResult = await extractor.GetThumbnailImageAsync(candidate, ct);
						if (extractorResult.Status == ExtractionStatus.NestedArchiveFound)
						{
							anyNestedArchive = true;
							this.logger.ZLogWarning($"圧縮ファイル内に画像ファイルが見つかりませんでした。対応アーカイブファイルのみ検出されたため、ArchiveInArchive として扱います: {candidate}");
							continue;
						}
						if (extractorResult.Status != ExtractionStatus.Success)
							continue;
						sourceStream = extractorResult.ImageStream;
						break;

					default:
						continue;
				}

				if (sourceStream is null)
					continue;

				await using (sourceStream)
				{
					var options = this.workerContext.ThumbnailOptions;

					// var sw = Stopwatch.StartNew();

					await using var thumbnailStream = await this.imageProcessor.ProcessThumbnailAsync(sourceStream, options, ct);

					var fileName = series.ThumbnailFileNameBase + ThumbnailExtension;
					var fullPath = ((IMangaBinderConfig)this.workerContext).GetThumbnailFullPath(fileName);

					Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
					await using var fileStream = File.Create(fullPath);
					await thumbnailStream.CopyToAsync(fileStream, ct);

					// sw.Stop();
					// series.ThumbnailProcessingTimeMs = sw.ElapsedMilliseconds;

					this.logger.ZLogInformation($"サムネイル候補を確定: {fullPath}");
					return new ThumbnailCreationResult(ThumbnailStatus.Completed, fileName);
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (VipsException ex)
			{
				this.logger.ZLogError(ex, $"サムネイル画像処理に失敗しました: {candidate}");
				return new ThumbnailCreationResult(ThumbnailStatus.Failed, string.Empty);
			}
			catch (IOException ex)
			{
				this.logger.ZLogError(ex, $"サムネイル抽出中にI/Oエラーが発生しました: {candidate}");
				return new ThumbnailCreationResult(ThumbnailStatus.Failed, string.Empty);
			}
			catch (UnauthorizedAccessException ex)
			{
				this.logger.ZLogError(ex, $"サムネイル抽出中にアクセス権エラーが発生しました: {candidate}");
				return new ThumbnailCreationResult(ThumbnailStatus.Failed, string.Empty);
			}
		}

		this.logger.ZLogWarning($"サムネイル候補が見つかりませんでした: {series.Title}");

		if (anyLimitExceeded)
			return new ThumbnailCreationResult(ThumbnailStatus.LimitExceeded, string.Empty);

		if (anyNestedArchive)
			return new ThumbnailCreationResult(ThumbnailStatus.ArchiveInArchive, string.Empty);

		return new ThumbnailCreationResult(ThumbnailStatus.Failed, string.Empty);
	}

	/// <summary>
	/// 指定パスがファイルならそのパスを返し、ディレクトリなら再帰探索して
	/// 最初に見つかった登録済み拡張子を持つファイルのフルパスを返します。
	/// サブフォルダを名前順昇順で優先し、見つからない場合に直下ファイルを評価します。
	/// </summary>
	/// <param name="path">探索起点のファイルまたはディレクトリパス。</param>
	/// <returns>最初に見つかった登録済みファイルのフルパス。見つからない場合は <c>null</c>。</returns>
	private string? FindFirstFile(string path)
	{
		if (File.Exists(path) && SupportedExtensionHelper.GetFileType(Path.GetExtension(path)) is not null)
			return path;

		if (!Directory.Exists(path))
			return null;

		var dir = new DirectoryInfo(path);

		foreach (var subDir in dir.GetDirectories().OrderBy(d => d.Name))
		{
			var result = this.FindFirstFile(subDir.FullName);
			if (result is not null)
				return result;
		}

		return dir.GetFiles()
			.OrderBy(f => f.Name)
			.FirstOrDefault(f => SupportedExtensionHelper.GetFileType(f.Extension) is not null)
			?.FullName;
	}
}