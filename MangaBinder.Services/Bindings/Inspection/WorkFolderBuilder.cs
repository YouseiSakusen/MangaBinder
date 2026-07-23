using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetVips;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// BindingSourceVolume の一覧を受け取り、ワークフォルダへ実体化して
/// VolumeInspectionResult を返します。
/// </summary>
public sealed class WorkFolderBuilder
{
	/// <summary>アプリケーション設定。</summary>
	private readonly AppSettings appSettings;

	/// <summary>処理スコープ内で Extractor を解決するためのファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>画像変換サービス。</summary>
	private readonly IVolumeImageProcessor imageProcessor;

	/// <summary>ロガー。</summary>
	private readonly ILogger<WorkFolderBuilder> logger;

	/// <summary>
	/// <see cref="WorkFolderBuilder"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	/// <param name="serviceScopeFactory">Extractor 解決用スコープファクトリー。</param>
	/// <param name="imageProcessor">画像変換サービス。</param>
	/// <param name="logger">ロガー。</param>
	public WorkFolderBuilder(
		AppSettings appSettings,
		IServiceScopeFactory serviceScopeFactory,
		IVolumeImageProcessor imageProcessor,
		ILogger<WorkFolderBuilder> logger)
	{
		this.appSettings = appSettings;
		this.serviceScopeFactory = serviceScopeFactory;
		this.imageProcessor = imageProcessor;
		this.logger = logger;
	}

	/// <summary>
	/// 指定された巻一覧をワークフォルダへ展開し、検査結果を返します。
	/// </summary>
	/// <param name="series">対象作品。</param>
	/// <param name="volumes">展開対象の巻一覧。</param>
	/// <param name="recreateWorkFolder">作品フォルダを作り直す場合は <see langword="true"/>。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>各巻の検査結果一覧。</returns>
	public async ValueTask<IReadOnlyList<VolumeInspectionResult>> BuildAsync(
		MangaSeries series,
		IReadOnlyList<BindingSourceVolume> volumes,
		bool recreateWorkFolder,
		CancellationToken cancellationToken = default)
	{
		var seriesFolderPath = this.appSettings.CreateWorkSeriesFolderPath(series.Title);

		if (recreateWorkFolder && Directory.Exists(seriesFolderPath))
			Directory.Delete(seriesFolderPath, recursive: true);

		Directory.CreateDirectory(seriesFolderPath);

		// SourcePath のバリデーション
		for (int i = 0; i < volumes.Count; i++)
		{
			if (string.IsNullOrEmpty(volumes[i].SourcePath))
			{
				throw new InvalidOperationException(
					$"BindingSourceVolume[{i}] の SourcePath が null または空文字です。これは上流処理のバグです。");
			}
		}

		// SourcePath 単位で volume をグループ化し、元のインデックスを保持
		var volumeGroups = volumes
			.Select((volume, index) => (volume, index))
			.GroupBy(x => x.volume.SourcePath, StringComparer.Ordinal)
			.ToList();

		// 最大4並列で処理用のグローバルセマフォ
		using var globalSemaphore = new SemaphoreSlim(4, 4);

		var groupTasks = new List<Task<List<(int Index, VolumeInspectionResult Result)>>>();

		foreach (var group in volumeGroups)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var groupTask = this.processSourcePathGroupAsync(
				globalSemaphore,
				group,
				seriesFolderPath,
				recreateWorkFolder,
				cancellationToken);

			groupTasks.Add(groupTask);
		}

		// すべてのグループタスクの完了を待機
		var allResults = await Task.WhenAll(groupTasks).ConfigureAwait(false);

		// グループの結果をフラット化し、元の順序で復元
		var flatResults = allResults
			.SelectMany(x => x)
			.OrderBy(x => x.Index)
			.Select(x => x.Result)
			.ToList();

		// ── メモリ調査：展開・検査処理完了直後 ──
		var currentProcess = Process.GetCurrentProcess();
		//this.logger.LogInformation(
		//	"[Memory Investigation] Before NetVips Cache.Max=0: " +
		//	"GC.Managed={ManagedMB}MB, " +
		//	"GC.Collect={GCCollectMB}MB, " +
		//	"WorkingSet={WorkingSetMB}MB, " +
		//	"PrivateMemory={PrivateMemoryMB}MB, " +
		//	"PagedMemory={PagedMemoryMB}MB",
		//	GC.GetTotalMemory(false) / (1024.0 * 1024),
		//	GC.GetTotalMemory(true) / (1024.0 * 1024),
		//	currentProcess.WorkingSet64 / (1024.0 * 1024),
		//	currentProcess.PrivateMemorySize64 / (1024.0 * 1024),
		//	currentProcess.PagedMemorySize64 / (1024.0 * 1024));

		// 製本前確認処理後は libvips の変換キャッシュを保持する必要がないため解放します。
		Cache.Max = 0;
		Cache.MaxFiles = 0;
		Cache.MaxMem = 0;

		// ── メモリ調査：NetVips キャッシュ設定後 ──
		currentProcess = Process.GetCurrentProcess();
		//this.logger.LogInformation(
		//	"[Memory Investigation] After NetVips Cache.Max=0: " +
		//	"GC.Managed={ManagedMB}MB, " +
		//	"GC.Collect={GCCollectMB}MB, " +
		//	"WorkingSet={WorkingSetMB}MB, " +
		//	"PrivateMemory={PrivateMemoryMB}MB, " +
		//	"PagedMemory={PagedMemoryMB}MB",
		//	GC.GetTotalMemory(false) / (1024.0 * 1024),
		//	GC.GetTotalMemory(true) / (1024.0 * 1024),
		//	currentProcess.WorkingSet64 / (1024.0 * 1024),
		//	currentProcess.PrivateMemorySize64 / (1024.0 * 1024),
		//	currentProcess.PagedMemorySize64 / (1024.0 * 1024));

		// メモリ調査用の切り分け処理：管理メモリとGC待ちを確認
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		// ── メモリ調査：GC実行後 ──
		currentProcess = Process.GetCurrentProcess();
		//this.logger.LogInformation(
		//	"[Memory Investigation] After GC.Collect/WaitForPendingFinalizers: " +
		//	"GC.Managed={ManagedMB}MB, " +
		//	"GC.Collect={GCCollectMB}MB, " +
		//	"WorkingSet={WorkingSetMB}MB, " +
		//	"PrivateMemory={PrivateMemoryMB}MB, " +
		//	"PagedMemory={PagedMemoryMB}MB",
		//	GC.GetTotalMemory(false) / (1024.0 * 1024),
		//	GC.GetTotalMemory(true) / (1024.0 * 1024),
		//	currentProcess.WorkingSet64 / (1024.0 * 1024),
		//	currentProcess.PrivateMemorySize64 / (1024.0 * 1024),
		//	currentProcess.PagedMemorySize64 / (1024.0 * 1024));

		return flatResults;
	}

	/// <summary>
	/// SourcePath ごとのグループを処理します。
	/// グループ内の volume は逐次処理され、グループ同士は最大4並列です。
	/// </summary>
	/// <param name="globalSemaphore">全体の並列実行数を制限するセマフォ。</param>
	/// <param name="sourcePathGroup">同一 SourcePath の volume グループ。</param>
	/// <param name="seriesFolderPath">作品フォルダパス。</param>
	/// <param name="recreateWorkFolder">中間フォルダを再作成する場合は <see langword="true"/>。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>グループ内の処理結果一覧（Index と Result のタプル）。</returns>
	private async Task<List<(int Index, VolumeInspectionResult Result)>> processSourcePathGroupAsync(
		SemaphoreSlim globalSemaphore,
		IGrouping<string, (BindingSourceVolume volume, int index)> sourcePathGroup,
		string seriesFolderPath,
		bool recreateWorkFolder,
		CancellationToken cancellationToken)
	{
		// グローバルセマフォを取得（最大4並列のグループ）
		await globalSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var groupResults = new List<(int Index, VolumeInspectionResult Result)>();

			// グループ内の volume を逐次処理
			foreach (var (volume, index) in sourcePathGroup)
			{
				cancellationToken.ThrowIfCancellationRequested();

				// 各 Extractor が内部で Task.Run により同期ライブラリを非同期化するため、
				// WorkFolderBuilder は単純に await するだけ
				var result = await this.buildVolumeAsync(seriesFolderPath, volume, recreateWorkFolder, cancellationToken).ConfigureAwait(false);

				groupResults.Add((index, result));
			}

			return groupResults;
		}
		finally
		{
			globalSemaphore.Release();
		}
	}

	/// <summary>
	/// 1巻分のワークフォルダへの展開と検査を行い、結果を返します。
	/// </summary>
	/// <param name="seriesFolderPath">作品フォルダパス。</param>
	/// <param name="volume">対象巻情報。</param>
	/// <param name="recreateWorkFolder">中間フォルダを再作成する場合は <see langword="true"/>。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>巻の検査結果。</returns>
	private async ValueTask<VolumeInspectionResult> buildVolumeAsync(
		string seriesFolderPath,
		BindingSourceVolume volume,
		bool recreateWorkFolder,
		CancellationToken cancellationToken)
	{
		var volumeFolderPath = Path.Combine(seriesFolderPath, volume.OutputVolumeFolderName);

		// 再作成不要かつ既存フォルダに画像が存在する場合は展開・変換をスキップして再利用する
		if (!recreateWorkFolder && Directory.Exists(volumeFolderPath))
		{
			var existingImages = Directory.GetFiles(volumeFolderPath, "*", SearchOption.TopDirectoryOnly)
				.Where(f => SupportedExtensionHelper.IsImage(Path.GetExtension(f)))
				.ToList();

			if (existingImages.Count > 0)
			{
				System.Diagnostics.Debug.WriteLine($"[WorkFolderBuilder] Reusing existing work folder: {volumeFolderPath}");
					return this.scanVolumeFolder(volume.OutputVolumeFolderName, volumeFolderPath, null, null);
			}
		}

		var tempFolderPath = Path.Combine(seriesFolderPath, $"__tmp_{volume.OutputVolumeFolderName}_{Guid.NewGuid():N}");
		Directory.CreateDirectory(tempFolderPath);

		var imageFileCount = 0;
		var inspectedImageSizeCount = 0;
		var landscapeCount = 0;

		try
		{
			// SourceType で Extractor を確定選択し、処理スコープ内で使い切る
			using var scope = this.serviceScopeFactory.CreateScope();
			var extractor = volume.SourceType switch
			{
				MaterialItemType.Archive =>
					(IVolumeExtractor)scope.ServiceProvider.GetRequiredService<ArchiveVolumeExtractor>(),
				MaterialItemType.Epub =>
					scope.ServiceProvider.GetRequiredService<EpubVolumeExtractor>(),
				_ =>
					scope.ServiceProvider.GetRequiredService<FolderVolumeExtractor>(),
			};

			// ベース名重複チェック用
			var baseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var hasDuplicate = false;

			await extractor.ExtractPagesAsync(volume, async page =>
			{
				cancellationToken.ThrowIfCancellationRequested();

				var needsConvert = SupportedExtensionHelper.RequiresConversion(page.Extension);
				var destFileName = needsConvert
					? Path.ChangeExtension(page.SourceName, ".jpg")
					: page.SourceName;

				var baseName = Path.GetFileNameWithoutExtension(destFileName);
				if (!baseNames.Add(baseName))
					hasDuplicate = true;

				var destPath = Path.Combine(tempFolderPath, destFileName);

				using var sourceStream = await page.OpenStreamAsync(cancellationToken);

				if (needsConvert)
				{
					var result = await this.imageProcessor.ConvertAsync(sourceStream, cancellationToken);
					using var converted = result.Stream;

					imageFileCount++;
					inspectedImageSizeCount++;

					if (result.IsLandscape)
						landscapeCount++;

					await using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
					await converted.CopyToAsync(dest, cancellationToken);
				}
				else
				{
					imageFileCount++;

					await using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
					await sourceStream.CopyToAsync(dest, cancellationToken);
				}
			}, cancellationToken);

			// 重複があればエラー
			if (hasDuplicate)
			{
				return new VolumeInspectionResult
				{
					VolumeName = volume.OutputVolumeFolderName,
					WorkVolumeFolderPath = volumeFolderPath,
					PlannedActions = [],
					HasDuplicateFileBaseName = true,
					HasError = true,
					ErrorMessage = "ファイル名重複あり",
				};
			}

			// 一時フォルダ → 巻フォルダへ反映
			if (Directory.Exists(volumeFolderPath))
				Directory.Delete(volumeFolderPath, recursive: true);

			Directory.Move(tempFolderPath, volumeFolderPath);
		}
		finally
		{
			if (Directory.Exists(tempFolderPath))
				Directory.Delete(tempFolderPath, recursive: true);
		}

		var knownLandscapeCount = inspectedImageSizeCount == imageFileCount
			? landscapeCount
			: (int?)null;

		return this.scanVolumeFolder(volume.OutputVolumeFolderName, volumeFolderPath, imageFileCount, knownLandscapeCount);
	}

	/// <summary>
	/// 巻フォルダをスキャンして VolumeInspectionResult を生成します。
	/// </summary>
	/// <param name="volumeName">巻名（表示用）。</param>
	/// <param name="volumeFolderPath">スキャン対象の巻フォルダパス。</param>
	/// <param name="knownImageFileCount">展開処理中に確定した画像ファイル数。未確定の場合は null。</param>
	/// <param name="knownLandscapeCount">展開処理中に確定した横長画像数。未確定の場合は null。</param>
	/// <returns>スキャン結果を反映した <see cref="VolumeInspectionResult"/>。</returns>
	private VolumeInspectionResult scanVolumeFolder(
		string volumeName,
		string volumeFolderPath,
		int? knownImageFileCount,
		int? knownLandscapeCount)
	{
		var files = Directory.GetFiles(volumeFolderPath, "*", SearchOption.TopDirectoryOnly)
			.Where(f => SupportedExtensionHelper.IsImage(Path.GetExtension(f)))
			.ToList();

		var subFolders = Directory.GetDirectories(volumeFolderPath);

		var extensions = files
			.Select(f => Path.GetExtension(f).ToLowerInvariant())
			.Distinct()
			.ToList();

		var hasMixedFormats = extensions.Count > 1;

		// ファイル名文字数不揃いチェック（拡張子除く）
		var nameLengths = files
			.Select(f => Path.GetFileNameWithoutExtension(f).Length)
			.Distinct()
			.ToList();
		var hasIrregularFileNameLength = nameLengths.Count > 1;

		var effectiveLandscapeCount = knownLandscapeCount;

		if (effectiveLandscapeCount is null)
		{
			var count = 0;

			foreach (var file in files)
			{
				try
				{
					using var img = NetVips.Image.NewFromFile(file, access: NetVips.Enums.Access.Sequential);
					if (img.Width > img.Height)
						count++;
				}
				catch
				{
					// 読み取り失敗は無視
				}
			}

			effectiveLandscapeCount = count;
		}

		var effectiveFileCount = knownImageFileCount ?? files.Count;
		var landscapeCount = effectiveLandscapeCount ?? 0;

		return new VolumeInspectionResult
		{
			VolumeName = volumeName,
			WorkVolumeFolderPath = volumeFolderPath,
			PlannedActions = [],
			ImageFileCount = files.Count,
			HasLandscapeImages = landscapeCount > 0,
			HasMixedFormats = hasMixedFormats,
			HasIrregularFileNameLength = hasIrregularFileNameLength,
			HasSubFolders = subFolders.Length > 0,
			AllLandscape = landscapeCount > 0 && landscapeCount == effectiveFileCount,
			RequiresSplit = false,
		};
	}
}
