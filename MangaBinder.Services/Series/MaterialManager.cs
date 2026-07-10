using MangaBinder.Bindings;
using MangaBinder.Settings;

namespace MangaBinder.Series;

/// <summary>
/// 素材ファイル・フォルダのパス解析を担当するマネージャーです。
/// </summary>
public sealed class MaterialManager
{
	/// <summary>
	/// 指定された素材ファイル/フォルダを、登録先の作品フォルダへ移動します。
	/// CanRemove == true のもの（編集画面で追加された素材）のみを移動対象にします。
	/// 移動元フォルダと登録先作品フォルダが同一の場合は移動を行いません。
	/// </summary>
	/// <remarks>
	/// 移動処理：
	/// - アーカイブファイル（.zip など）：ファイルを作品フォルダ直下へ移動
	/// - 画像のみフォルダ：フォルダごと作品フォルダ直下へ移動
	/// - epub ファイル：ファイルを作品フォルダ直下へ移動
	/// 
	/// 同一フォルダの判定：
	/// - Path.GetFullPath() で正規化した上でパス文字列で比較
	/// 
	/// 移動対象の選別：
	/// - CanRemove == true のみが移動対象
	/// - CanRemove == false（既存素材）は SkippedItems に追加される
	/// </remarks>
	/// <param name="destinationSourceFolder">登録先の素材フォルダ（SourceFolder）。</param>
	/// <param name="materialFolderName">作品フォルダ名。</param>
	/// <param name="materialFiles">移動する素材ファイル/フォルダ一覧。</param>
	/// <returns>移動結果を表す MaterialMoveResult。</returns>
	public async ValueTask<MaterialMoveResult> MoveMaterialsAsync(
		SourceFolder destinationSourceFolder,
		string materialFolderName,
		IEnumerable<MaterialFile> materialFiles)
	{
		ArgumentNullException.ThrowIfNull(destinationSourceFolder);
		ArgumentException.ThrowIfNullOrEmpty(materialFolderName);
		ArgumentNullException.ThrowIfNull(materialFiles);

		// 登録先素材フォルダのパスを取得
		var destinationSourcePath = Path.GetFullPath(destinationSourceFolder.FolderPath.Value);

		// 作品フォルダパスを構築
		var seriesFolderPath = Path.Combine(destinationSourcePath, materialFolderName);
		var seriesFolderPathFull = Path.GetFullPath(seriesFolderPath);

		// 作品フォルダが存在するかチェック（作成フラグ用）
		var createdSeriesFolder = !Directory.Exists(seriesFolderPathFull);

		// 作品フォルダが存在しない場合は作成
		if (createdSeriesFolder)
		{
			Directory.CreateDirectory(seriesFolderPathFull);
		}

		var movedItems = new List<MaterialMoveItem>();
		var skippedItems = new List<MaterialMoveItem>();

		foreach (var materialFile in materialFiles)
		{
			var sourcePath = Path.GetFullPath(materialFile.FullPath);

			// CanRemove == false の既存素材はスキップ対象に追加
			if (!materialFile.CanRemove)
			{
				var destinationPath = Path.Combine(seriesFolderPathFull, Path.GetFileName(sourcePath));

				skippedItems.Add(new MaterialMoveItem
				{
					SourcePath = sourcePath,
					DestinationPath = destinationPath,
					Type = materialFile.Type,
				});
				continue;
			}

			// 移動元フォルダと登録先作品フォルダが同一かチェック
			var sourceDir = Directory.Exists(sourcePath)
				? Path.GetFullPath(sourcePath)
				: Path.GetFullPath(Path.GetDirectoryName(sourcePath) ?? "");

			// ソースがファイルの場合、親ディレクトリを取得
			if (File.Exists(sourcePath))
			{
				sourceDir = Path.GetFullPath(Path.GetDirectoryName(sourcePath) ?? "");
			}
			else if (Directory.Exists(sourcePath))
			{
				sourceDir = Path.GetFullPath(sourcePath);
			}

			// 同一フォルダ判定：正規化したパスで比較
			if (string.Equals(sourceDir, seriesFolderPathFull, StringComparison.OrdinalIgnoreCase))
			{
				// 移動不要：登録先が移動元フォルダ自体
				var destinationPath = string.Equals(sourceDir, seriesFolderPathFull, StringComparison.OrdinalIgnoreCase)
					? sourcePath
					: Path.Combine(seriesFolderPathFull, Path.GetFileName(sourcePath));

				skippedItems.Add(new MaterialMoveItem
				{
					SourcePath = sourcePath,
					DestinationPath = destinationPath,
					Type = materialFile.Type,
				});
				continue;
			}

			// ItemType に応じた移動処理
			switch (materialFile.Type)
			{
				case MaterialItemType.Archive:
					// アーカイブファイル：作品フォルダ直下へ移動
					await this.MoveArchiveFileAsync(sourcePath, seriesFolderPathFull, movedItems, materialFile.Type);
					break;

				case MaterialItemType.Folder:
					// 画像フォルダ：フォルダごと作品フォルダ直下へ移動
					await this.MoveFolderAsync(sourcePath, seriesFolderPathFull, movedItems, materialFile.Type);
					break;

				case MaterialItemType.Epub:
					// epub ファイル：作品フォルダ直下へ移動
					await this.MoveEpubFileAsync(sourcePath, seriesFolderPathFull, movedItems, materialFile.Type);
					break;

				default:
					// その他の種別は移動しない
					break;
			}
		}

		return new MaterialMoveResult
		{
			SeriesFolderPath = seriesFolderPathFull,
			CreatedSeriesFolder = createdSeriesFolder,
			MovedItems = movedItems.AsReadOnly(),
			SkippedItems = skippedItems.AsReadOnly(),
		};
	}

	/// <summary>
	/// アーカイブファイルを指定された作品フォルダ直下へ移動します。
	/// 移動先に同名ファイルが既に存在する場合は例外を投げます。
	/// </summary>
	/// <exception cref="InvalidOperationException">移動先に同名ファイルが既に存在する場合。</exception>
	private async ValueTask MoveArchiveFileAsync(
		string sourceFilePath,
		string destinationFolderPath,
		List<MaterialMoveItem> movedItems,
		MaterialItemType itemType)
	{
		var fileName = Path.GetFileName(sourceFilePath);
		var destinationFilePath = Path.Combine(destinationFolderPath, fileName);

		// 移動先に同名ファイルが存在する場合は例外を投げる
		if (File.Exists(destinationFilePath))
		{
			throw new InvalidOperationException($"移動先に同名ファイルが既に存在しています: {destinationFilePath}");
		}

		// ファイルを移動
		File.Move(sourceFilePath, destinationFilePath);

		movedItems.Add(new MaterialMoveItem
		{
			SourcePath = sourceFilePath,
			DestinationPath = destinationFilePath,
			Type = itemType,
		});

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// 画像フォルダをフォルダごと指定された作品フォルダ直下へ移動します。
	/// 移動先に同名フォルダが既に存在する場合は例外を投げます。
	/// </summary>
	/// <exception cref="InvalidOperationException">移動先に同名フォルダが既に存在する場合。</exception>
	private async ValueTask MoveFolderAsync(
		string sourceFolderPath,
		string destinationFolderPath,
		List<MaterialMoveItem> movedItems,
		MaterialItemType itemType)
	{
		var folderName = Path.GetFileName(sourceFolderPath);
		var destinationPath = Path.Combine(destinationFolderPath, folderName);

		// 移動先に同名フォルダが存在する場合は例外を投げる
		if (Directory.Exists(destinationPath))
		{
			throw new InvalidOperationException($"移動先に同名フォルダが既に存在しています: {destinationPath}");
		}

		// フォルダを移動
		Directory.Move(sourceFolderPath, destinationPath);

		movedItems.Add(new MaterialMoveItem
		{
			SourcePath = sourceFolderPath,
			DestinationPath = destinationPath,
			Type = itemType,
		});

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// epub ファイルを指定された作品フォルダ直下へ移動します。
	/// 移動先に同名ファイルが既に存在する場合は例外を投げます。
	/// </summary>
	/// <exception cref="InvalidOperationException">移動先に同名ファイルが既に存在する場合。</exception>
	private async ValueTask MoveEpubFileAsync(
		string sourceFilePath,
		string destinationFolderPath,
		List<MaterialMoveItem> movedItems,
		MaterialItemType itemType)
	{
		var fileName = Path.GetFileName(sourceFilePath);
		var destinationFilePath = Path.Combine(destinationFolderPath, fileName);

		// 移動先に同名ファイルが存在する場合は例外を投げる
		if (File.Exists(destinationFilePath))
		{
			throw new InvalidOperationException($"移動先に同名ファイルが既に存在しています: {destinationFilePath}");
		}

		// ファイルを移動
		File.Move(sourceFilePath, destinationFilePath);

		movedItems.Add(new MaterialMoveItem
		{
			SourcePath = sourceFilePath,
			DestinationPath = destinationFilePath,
			Type = itemType,
		});

		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// 素材候補パスを解析し、EditorPageViewModel.MaterialFiles に追加可能な候補を返します。
	/// </summary>
	/// <remarks>
	/// 処理フロー：
	/// - ファイルの場合：SupportedFileExtensions のアーカイブファイルのみ許可
	/// - フォルダの場合：
	///   - フォルダ直下が epub のみなら、epub ファイルを個別素材候補として返す
	///   - フォルダ直下が画像のみなら、フォルダ自体を素材候補として返す
	///   - それ以外の混在フォルダは追加しない
	/// - 重複排除：
	///   - 同一 FullPath は追加しない
	///   - 既に materialFilesCollection に存在するパスも追加しない
	///   - 同じ入力内で重複しているパスも追加しない
	/// </remarks>
	/// <param name="paths">解析対象のパス一覧（ファイルおよびフォルダパス）。</param>
	/// <param name="materialFilesCollection">既存の素材ファイル一覧。重複チェックに使用します。</param>
	/// <returns>追加可能な素材候補 DTO の列挙。</returns>
	public IEnumerable<MaterialFileCandidate> AnalyzePaths(IEnumerable<string> paths, IEnumerable<string> existingFullPaths)
	{
		var result = new List<MaterialFileCandidate>();
		var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var path in paths ?? Enumerable.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				continue;
			}

			var fullPath = Path.GetFullPath(path);

			// 同じ入力内での重複を排除
			if (processedPaths.Contains(fullPath))
			{
				continue;
			}

			// 既存の素材に同じパスがあるかチェック
			if (existingFullPaths.Any(existing => string.Equals(existing, fullPath, StringComparison.OrdinalIgnoreCase)))
			{
				processedPaths.Add(fullPath);
				continue;
			}

			if (File.Exists(fullPath))
			{
				// ファイルの場合：拡張子をチェック
				var ext = Path.GetExtension(fullPath);
				if (SupportedExtensionHelper.IsArchive(ext))
				{
					var candidate = new MaterialFileCandidate
					{
						FullPath = fullPath,
						FileName = Path.GetFileName(fullPath),
						Size = new FileInfo(fullPath).Length,
						Type = MaterialItemType.Archive,
					};
					result.Add(candidate);
					processedPaths.Add(fullPath);
				}
			}
			else if (Directory.Exists(fullPath))
			{
				// フォルダの場合：内容を解析
				var epubFiles = new List<string>();
				var imageFiles = new List<string>();
				var otherFiles = new List<string>();

				// フォルダ直下のファイルのみを分析（サブフォルダは見ない）
				try
				{
					foreach (var file in Directory.GetFiles(fullPath))
					{
						var fileExt = Path.GetExtension(file);

						if (string.Equals(fileExt, ".epub", StringComparison.OrdinalIgnoreCase))
						{
							epubFiles.Add(file);
						}
						else if (SupportedExtensionHelper.IsImage(fileExt))
						{
							imageFiles.Add(file);
						}
						else
						{
							otherFiles.Add(file);
						}
					}
				}
				catch
				{
					// ディレクトリアクセス エラーの場合はスキップ
					processedPaths.Add(fullPath);
					continue;
				}

				// フォルダの種類を判定
				var hasOnlyEpub = epubFiles.Count > 0 && imageFiles.Count == 0 && otherFiles.Count == 0;
				var hasOnlyImages = imageFiles.Count > 0 && epubFiles.Count == 0 && otherFiles.Count == 0;

				if (hasOnlyEpub)
				{
					// epub のみが入っているフォルダ：epub ファイルを個別に追加
					foreach (var epubFile in epubFiles)
					{
						// 既存の素材と重複チェック
						if (existingFullPaths.Any(existing => string.Equals(existing, epubFile, StringComparison.OrdinalIgnoreCase)))
						{
							continue;
						}

						// 同じ入力内での重複チェック
						if (processedPaths.Contains(epubFile))
						{
							continue;
						}

						var candidate = new MaterialFileCandidate
						{
							FullPath = epubFile,
							FileName = Path.GetFileName(epubFile),
							Size = new FileInfo(epubFile).Length,
							Type = MaterialItemType.Epub,
						};
						result.Add(candidate);
						processedPaths.Add(epubFile);
					}
				}
				else if (hasOnlyImages)
				{
					// 画像のみが入っているフォルダ：フォルダ自体を追加
					var candidate = new MaterialFileCandidate
					{
						FullPath = fullPath,
						FileName = Path.GetFileName(fullPath),
						Size = null,
						Type = MaterialItemType.Folder,
					};
					result.Add(candidate);
					processedPaths.Add(fullPath);
				}
				// それ以外は追加しない
			}
		}

		return result;
	}
}
