using MangaBinder.Settings;

namespace MangaBinder.Bindings;

/// <summary>
/// 素材フォルダの状態を解析するサービスです。
/// </summary>
public class SeriesMaterialFolderLoader
{
	private readonly MaterialArchiveExtractor archiveExtractor;

	private readonly MaterialArchiveRepository archiveRepository;

	/// <summary>
	/// <see cref="SeriesMaterialFolderLoader"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="archiveExtractor">Archive 内部構造を解析するサービス。</param>
	/// <param name="archiveRepository">Archive キャッシュを管理するリポジトリ。</param>
	public SeriesMaterialFolderLoader(
		MaterialArchiveExtractor archiveExtractor,
		MaterialArchiveRepository archiveRepository)
	{
		this.archiveExtractor = archiveExtractor ?? throw new ArgumentNullException(nameof(archiveExtractor));
		this.archiveRepository = archiveRepository ?? throw new ArgumentNullException(nameof(archiveRepository));
	}

	/// <summary>
	/// 指定された作品の素材フォルダ状態を取得します。
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>素材フォルダの解析結果。</returns>
	public ValueTask<MaterialFolderResult> GetMaterialsAsync(
		MangaSeries series,
		CancellationToken cancellationToken)
	{
		// Material ロールの所在情報を取得
		var materialSource = series.Sources.FirstOrDefault(s => s.Role == FolderRole.Material);

		if (materialSource == null)
		{
			return ValueTask.FromResult(new MaterialFolderResult
			{
				Status = MaterialFolderStatus.NoMaterialSource,
				TargetPath = string.Empty,
			});
		}

		var materialPath = materialSource.Path;

		// DriveInfo.IsReady をチェック
		var drive = new DriveInfo(materialPath);
		if (!drive.IsReady)
		{
			return ValueTask.FromResult(new MaterialFolderResult
			{
				Status = MaterialFolderStatus.DriveNotReady,
				TargetPath = materialPath,
			});
		}

		// パスが存在するかチェック
		if (!Directory.Exists(materialPath))
		{
			return ValueTask.FromResult(new MaterialFolderResult
			{
				Status = MaterialFolderStatus.MaterialSourceNotFound,
				TargetPath = materialPath,
			});
		}

		// 正常系: 素材ツリーを生成（バックグラウンドで実行）
		return new ValueTask<MaterialFolderResult>(
			Task.Run(
				async () =>
				{
					var result = await this.BuildMaterialTreeAsync(materialPath, series.SeriesId, materialSource.SourceId, cancellationToken);

					// HasNestedArchive を更新・保存
					if (result.Status == MaterialFolderStatus.Success)
					{
						series.HasNestedArchive = result.HasNestedArchive;
						await this.archiveRepository.UpdateMangaSeriesAsync(series, cancellationToken);
					}

					return result;
				},
				cancellationToken));
	}

	/// <summary>
	/// 素材フォルダツリーを構築します。
	/// </summary>
	private async Task<MaterialFolderResult> BuildMaterialTreeAsync(string materialPath, long seriesId, long sourceId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Archiveキャッシュを事前取得
		var archiveCache = await this.archiveRepository.GetArchivesBySourceIdAsync(sourceId, cancellationToken);

		// Root ノード相当の MaterialItem を生成
		var rootItem = new MaterialItem
		{
			ItemType = MaterialItemType.Root,
			Name = Path.GetFileName(materialPath),
			FullPath = materialPath,
			SourcePath = materialPath,
		};

		// フォルダ直下を走査して子を追加
		await this.PopulateFolderAsync(rootItem, materialPath, archiveCache, seriesId, sourceId, cancellationToken);

		// Nested Archive の集約判定：archiveCache 内に IsNestedArchive = true がある場合、HasNestedArchive を true に設定
		var hasNestedArchive = archiveCache.Values.Any(c => c.IsNestedArchive);

		// NestedArchive に該当する外側アーカイブファイル名を抽出
		var nestedArchiveFileNames = archiveCache.Values
			.Where(c => c.IsNestedArchive)
			.Select(c => Path.GetFileName(c.ArchivePath))
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(name => name)
			.ToList();

		return new MaterialFolderResult
		{
			Status = MaterialFolderStatus.Success,
			TargetPath = materialPath,
			Materials = [rootItem],
			HasNestedArchive = hasNestedArchive,
			NestedArchiveFileNames = nestedArchiveFileNames,
		};
	}

	/// <summary>
	/// フォルダを走査して子 MaterialItem を生成し、親に追加します。
	/// </summary>
	private async Task PopulateFolderAsync(
		MaterialItem parentItem,
		string folderPath,
		Dictionary<string, MaterialArchiveRepository.ArchiveCacheInfo> archiveCache,
		long seriesId,
		long sourceId,
		CancellationToken cancellationToken)
	{
		if (!Directory.Exists(folderPath))
			return;

		// サブフォルダを処理
		foreach (var dir in Directory.EnumerateDirectories(folderPath).OrderBy(d => d))
		{
			cancellationToken.ThrowIfCancellationRequested();

			var isSelectable = this.ContainsDirectImages(dir);
			var fileCount = this.CountDirectImages(dir);
			var folderItem = new MaterialItem
			{
				ItemType = MaterialItemType.Folder,
				Name = Path.GetFileName(dir),
				FullPath = dir,
				FileCount = fileCount,
				SourcePath = dir,
				IsSelectableByDefault = isSelectable,
				SelectionDisabledReason = isSelectable ? string.Empty : "直下に画像ファイルが存在しません",
			};

			// 再帰的にサブフォルダを走査
			await this.PopulateFolderAsync(folderItem, dir, archiveCache, seriesId, sourceId, cancellationToken);
			parentItem.Children.Add(folderItem);
		}

		// ファイルを処理
		foreach (var file in Directory.EnumerateFiles(folderPath).OrderBy(f => f))
		{
			cancellationToken.ThrowIfCancellationRequested();

			var ext = Path.GetExtension(file);
			var fileType = SupportedExtensionHelper.GetFileType(ext);

			if (fileType == FileType.Archive)
			{
				var fileInfo = new FileInfo(file);
				long bytes = fileInfo.Length;
				var sizeText = bytes >= 1024L * 1024 * 1024
					? $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
					: $"{bytes / (1024.0 * 1024):F1} MB";

				var archiveItem = new MaterialItem
				{
					ItemType = MaterialItemType.Archive,
					Name = Path.GetFileName(file),
					FullPath = file,
					FileSizeText = sizeText,
					SourcePath = file,
				};

				// Archive 内部構造を解析して子を追加
				await this.PopulateArchiveAsync(archiveItem, file, archiveCache, seriesId, sourceId, cancellationToken);
				parentItem.Children.Add(archiveItem);
			}
			else if (fileType == FileType.Epub)
			{
				var epubItem = new MaterialItem
				{
					ItemType = MaterialItemType.Epub,
					Name = Path.GetFileName(file),
					FullPath = file,
					SourcePath = file,
					IsSelectableByDefault = true,
				};
				parentItem.Children.Add(epubItem);
			}
		}
	}

	/// <summary>
	/// Archive ファイル内部のフォルダ構造を解析して、親 MaterialItem に子を追加します。
	/// キャッシュが一致する場合はDBから復元し、不一致の場合は解析→保存します。
	/// IsNestedArchive = false かつ MaterialArchiveEntries = 0件 の既存キャッシュは再スキャンします。
	/// NestedArchive対応前の旧キャッシュ（判定条件：IsNestedArchive=false、HasArchiveFile=false、終端の空フォルダ存在）も再スキャンします。
	/// 再スキャン結果はメモリ上の archiveCache にも反映されます。
	/// </summary>
	private async Task PopulateArchiveAsync(
		MaterialItem archiveItem,
		string archivePath,
		Dictionary<string, MaterialArchiveRepository.ArchiveCacheInfo> archiveCache,
		long seriesId,
		long sourceId,
		CancellationToken cancellationToken)
	{
		try
		{
			// キャッシュ一致確認
			if (archiveCache.TryGetValue(archivePath, out var cacheInfo))
			{
				var fileInfo = new FileInfo(archivePath);
				if (fileInfo.Length == cacheInfo.FileSize &&
					fileInfo.LastWriteTime == cacheInfo.LastWriteTime)
				{
					// キャッシュサイズ・更新日時が一致している場合
					// 再スキャン対象を判定
					var needsRescan = false;

					// 1. IsNestedArchive = false かつ MaterialArchiveEntries = 0件 の場合は、旧キャッシュまたは未判定キャッシュのため再スキャン
					if (cacheInfo.IsNestedArchive == false && cacheInfo.Entries.Count == 0)
					{
						needsRescan = true;
					}

					// 2. NestedArchive 対応前の旧キャッシュ判定
					if (!needsRescan && this.IsLegacyNestedArchiveCache(cacheInfo))
					{
						needsRescan = true;
					}

					if (!needsRescan)
					{
						// キャッシュ一致 → DBから復元
						await this.restoreArchiveFromCacheAsync(archiveItem, cacheInfo, archivePath, cancellationToken);
						return;
					}
				}
			}

			// キャッシュなし・不一致・または再スキャン必要 → Archive を解析
			var archiveFile = await this.archiveExtractor.ExtractAsync(archivePath, cancellationToken);

			// ArchiveFolderItem ツリーを MaterialItem ツリーに変換
			foreach (var folderItem in archiveFile.Folders)
			{
				var materialFolderItem = this.ConvertArchiveFolderToMaterialItem(folderItem, archivePath);
				archiveItem.Children.Add(materialFolderItem);
			}

			// DBに保存
			await this.archiveRepository.SaveArchiveAsync(seriesId, sourceId, archiveFile, cancellationToken);

			// メモリ上の archiveCache を更新（再スキャン結果を即時反映）
			var entryCache = this.ConvertFoldersToEntryCacheInfos(archiveFile.Folders);
			archiveCache[archivePath] = new MaterialArchiveRepository.ArchiveCacheInfo
			{
				MaterialArchiveId = cacheInfo?.MaterialArchiveId ?? 0,
				ArchivePath = archiveFile.ArchivePath,
				FileSize = archiveFile.FileSize,
				LastWriteTime = archiveFile.LastWriteTime,
				IsNestedArchive = archiveFile.IsNestedArchive,
				HasArchiveFile = entryCache.Any(e => e.HasArchiveFile),
				Entries = entryCache,
			};
		}
		catch
		{
			// Archive 解析エラーは無視（既存の MaterialFolderSeriesExtractor と同様）
		}
	}

	/// <summary>
	/// DBキャッシュから Archive の MaterialItem ツリーを復元します。
	/// </summary>
	private async ValueTask restoreArchiveFromCacheAsync(
		MaterialItem archiveItem,
		MaterialArchiveRepository.ArchiveCacheInfo cacheInfo,
		string archivePath,
		CancellationToken cancellationToken)
	{
		// EntryPath が null/empty のルートエントリは存在しないので、
		// cacheInfo.Entries から直接、ParentEntryPath が null のものをルートとして扱う
		var rootEntries = cacheInfo.Entries.Where(e => string.IsNullOrEmpty(e.ParentEntryPath)).ToList();

		foreach (var entry in rootEntries)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var materialItem = this.restoreArchiveEntryToMaterialItem(entry, cacheInfo.Entries, archivePath);
			archiveItem.Children.Add(materialItem);
		}

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// ArchiveEntry キャッシュから MaterialItem を復元します（再帰的）。
	/// </summary>
	private MaterialItem restoreArchiveEntryToMaterialItem(
		MaterialArchiveRepository.ArchiveEntryCacheInfo entry,
		List<MaterialArchiveRepository.ArchiveEntryCacheInfo> allEntries,
		string archivePath)
	{
		var displayName = GetArchiveEntryName(entry.EntryPath);

		var item = new MaterialItem
		{
			ItemType = MaterialItemType.Folder,
			Name = displayName,
			FullPath = $"{archivePath}/{entry.EntryPath}",
			FileCount = entry.FileCount,
			SourcePath = archivePath,
			ArchiveEntryPrefix = entry.EntryPath,
			IsSelectableByDefault = entry.IsSelectable,
			SelectionDisabledReason = entry.SelectionDisabledReason,
		};

		// 子エントリを再帰的に復元
		var childEntries = allEntries.Where(e => e.ParentEntryPath == entry.EntryPath).ToList();
		foreach (var childEntry in childEntries)
		{
			var childItem = this.restoreArchiveEntryToMaterialItem(childEntry, allEntries, archivePath);
			item.Children.Add(childItem);
		}

		return item;
	}

	/// <summary>
	/// ArchiveFolderItem を MaterialItem に変換します（再帰的）。
	/// </summary>
	private MaterialItem ConvertArchiveFolderToMaterialItem(ArchiveFolderItem archiveFolder, string archivePath)
	{
		// EntryPath から表示名を抽出（最後の区切り以降）
		var displayName = string.IsNullOrEmpty(archiveFolder.EntryPath)
			? Path.GetFileName(archivePath)
			: GetArchiveEntryName(archiveFolder.EntryPath);

		var item = new MaterialItem
		{
			ItemType = MaterialItemType.Folder,
			Name = displayName,
			FullPath = $"{archivePath}/{archiveFolder.EntryPath}",
			FileCount = archiveFolder.FileCount,
			SourcePath = archivePath,
			ArchiveEntryPrefix = archiveFolder.EntryPath,
			IsSelectableByDefault = archiveFolder.IsSelectable,
			SelectionDisabledReason = archiveFolder.SelectionDisabledReason,
		};

		// 子フォルダを再帰的に変換
		foreach (var childFolder in archiveFolder.Children)
		{
			var childItem = this.ConvertArchiveFolderToMaterialItem(childFolder, archivePath);
			item.Children.Add(childItem);
		}

		return item;
	}

	/// <summary>
	/// アーカイブ内部パスから表示名を抽出します。
	/// </summary>
	/// <param name="entryPath">アーカイブ内部パス（/ 区切り）。</param>
	/// <returns>表示名。</returns>
	private static string GetArchiveEntryName(string entryPath)
	{
		var normalized = entryPath.TrimEnd('/');
		var index = normalized.LastIndexOf('/');
		return index < 0 ? normalized : normalized[(index + 1)..];
	}

	/// <summary>
	/// 指定フォルダが直下に画像ファイルを含むかどうかを判定します。
	/// </summary>
	private bool ContainsDirectImages(string folderPath)
	{
		try
		{
			return Directory.EnumerateFiles(folderPath)
				.Any(f => SupportedExtensionHelper.GetFileType(Path.GetExtension(f)) == FileType.Image);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 指定フォルダの直下にある画像ファイル数をカウントします。
	/// </summary>
	private int CountDirectImages(string folderPath)
	{
		try
		{
			return Directory.EnumerateFiles(folderPath)
				.Count(f => SupportedExtensionHelper.GetFileType(Path.GetExtension(f)) == FileType.Image);
		}
		catch
		{
			return 0;
		}
	}

	/// <summary>
	/// ArchiveFolderItem リストから ArchiveEntryCacheInfo リストへ変換します（再帰的）。
	/// </summary>
	private List<MaterialArchiveRepository.ArchiveEntryCacheInfo> ConvertFoldersToEntryCacheInfos(
		List<ArchiveFolderItem> folders)
	{
		var result = new List<MaterialArchiveRepository.ArchiveEntryCacheInfo>();

		foreach (var folder in folders)
		{
			// フォルダエントリをキャッシュエントリに変換
			var entryCache = new MaterialArchiveRepository.ArchiveEntryCacheInfo
			{
				EntryPath = folder.EntryPath,
				ParentEntryPath = string.IsNullOrEmpty(folder.ParentEntryPath) ? null : folder.ParentEntryPath,
				FileCount = folder.FileCount,
				IsSelectable = folder.IsSelectable,
				SelectionDisabledReason = string.IsNullOrEmpty(folder.SelectionDisabledReason) ? string.Empty : folder.SelectionDisabledReason,
				HasArchiveFile = folder.HasArchiveFile,
			};
			result.Add(entryCache);

			// 子フォルダを再帰的に変換
			var childEntries = this.ConvertFoldersToEntryCacheInfos(folder.Children);
			result.AddRange(childEntries);
		}

		return result;
	}

	/// <summary>
	/// NestedArchive 対応前の旧キャッシュかどうかを判定します。
	/// 以下の条件を満たす場合は旧キャッシュとして再スキャン対象になります：
	/// - IsNestedArchive == false
	/// - かつ HasArchiveFile == false
	/// - かつ MaterialArchiveEntry の中に、FileCount == 0 で子フォルダを持たない終端フォルダが存在する
	/// </summary>
	private bool IsLegacyNestedArchiveCache(MaterialArchiveRepository.ArchiveCacheInfo cacheInfo)
	{
		// 最初の2つの条件をチェック
		if (cacheInfo.IsNestedArchive || cacheInfo.HasArchiveFile)
		{
			return false;
		}

		// MaterialArchiveEntry が空の場合は既に判定済みなので旧キャッシュではない
		if (cacheInfo.Entries.Count == 0)
		{
			return false;
		}

		// 終端フォルダで FileCount == 0 かつ子フォルダを持たないものを探す
		foreach (var entry in cacheInfo.Entries)
		{
			// FileCount == 0 でなければスキップ
			if (entry.FileCount != 0)
			{
				continue;
			}

			// 子フォルダの存在判定：このエントリを ParentEntryPath に持つレコードがあるか
			var hasChildren = cacheInfo.Entries.Any(e => e.ParentEntryPath == entry.EntryPath);
			if (!hasChildren)
			{
				// 子を持たない終端フォルダで FileCount == 0
				return true;
			}
		}

		return false;
	}
}
