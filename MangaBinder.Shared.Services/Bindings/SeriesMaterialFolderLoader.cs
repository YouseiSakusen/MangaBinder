using MangaBinder.Settings;

namespace MangaBinder.Bindings;

/// <summary>
/// 素材フォルダの状態を解析するサービスです。
/// </summary>
public class SeriesMaterialFolderLoader
{
	private readonly MaterialArchiveExtractor archiveExtractor;

	/// <summary>
	/// <see cref="SeriesMaterialFolderLoader"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="archiveExtractor">Archive 内部構造を解析するサービス。</param>
	public SeriesMaterialFolderLoader(MaterialArchiveExtractor archiveExtractor)
	{
		this.archiveExtractor = archiveExtractor ?? throw new ArgumentNullException(nameof(archiveExtractor));
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
				async () => await this.BuildMaterialTreeAsync(materialPath, cancellationToken),
				cancellationToken));
	}

	/// <summary>
	/// 素材フォルダツリーを構築します。
	/// </summary>
	private async Task<MaterialFolderResult> BuildMaterialTreeAsync(string materialPath, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Root ノード相当の MaterialItem を生成
		var rootItem = new MaterialItem
		{
			ItemType = MaterialItemType.Root,
			Name = Path.GetFileName(materialPath),
			FullPath = materialPath,
			SourcePath = materialPath,
		};

		// フォルダ直下を走査して子を追加
		await this.PopulateFolderAsync(rootItem, materialPath, cancellationToken);

		return new MaterialFolderResult
		{
			Status = MaterialFolderStatus.Success,
			TargetPath = materialPath,
			Materials = [rootItem],
		};
	}

	/// <summary>
	/// フォルダを走査して子 MaterialItem を生成し、親に追加します。
	/// </summary>
	private async Task PopulateFolderAsync(MaterialItem parentItem, string folderPath, CancellationToken cancellationToken)
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
			await this.PopulateFolderAsync(folderItem, dir, cancellationToken);
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
				await this.PopulateArchiveAsync(archiveItem, file, cancellationToken);
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
	/// </summary>
	private async Task PopulateArchiveAsync(MaterialItem archiveItem, string archivePath, CancellationToken cancellationToken)
	{
		try
		{
			// Archive を抽出
			var archiveFile = await this.archiveExtractor.ExtractAsync(archivePath, cancellationToken);

			// ArchiveFolderItem ツリーを MaterialItem ツリーに変換
			foreach (var folderItem in archiveFile.Folders)
			{
				var materialFolderItem = this.ConvertArchiveFolderToMaterialItem(folderItem, archivePath);
				archiveItem.Children.Add(materialFolderItem);
			}
		}
		catch
		{
			// Archive 解析エラーは無視（既存の MaterialFolderSeriesExtractor と同様）
		}
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
}
