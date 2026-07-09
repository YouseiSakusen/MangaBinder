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

		// 作品フォルダが存在しない場合は作成
		if (!Directory.Exists(seriesFolderPathFull))
		{
			Directory.CreateDirectory(seriesFolderPathFull);
		}

		var movedItems = new List<MaterialMoveItem>();
		var skippedItems = new List<MaterialMoveItem>();

		foreach (var materialFile in materialFiles)
		{
			var sourcePath = Path.GetFullPath(materialFile.FullPath);

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
			MovedItems = movedItems.AsReadOnly(),
			SkippedItems = skippedItems.AsReadOnly(),
		};
	}

	/// <summary>
	/// アーカイブファイルを指定された作品フォルダ直下へ移動します。
	/// </summary>
	private async ValueTask MoveArchiveFileAsync(
		string sourceFilePath,
		string destinationFolderPath,
		List<MaterialMoveItem> movedItems,
		MaterialItemType itemType)
	{
		var fileName = Path.GetFileName(sourceFilePath);
		var destinationFilePath = Path.Combine(destinationFolderPath, fileName);

		// 既に存在する場合は削除
		if (File.Exists(destinationFilePath))
		{
			File.Delete(destinationFilePath);
		}

		// ファイルを移動
		File.Move(sourceFilePath, destinationFilePath, overwrite: true);

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
	/// </summary>
	private async ValueTask MoveFolderAsync(
		string sourceFolderPath,
		string destinationFolderPath,
		List<MaterialMoveItem> movedItems,
		MaterialItemType itemType)
	{
		var folderName = Path.GetFileName(sourceFolderPath);
		var destinationPath = Path.Combine(destinationFolderPath, folderName);

		// 既に存在する場合は削除
		if (Directory.Exists(destinationPath))
		{
			Directory.Delete(destinationPath, recursive: true);
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
	/// </summary>
	private async ValueTask MoveEpubFileAsync(
		string sourceFilePath,
		string destinationFolderPath,
		List<MaterialMoveItem> movedItems,
		MaterialItemType itemType)
	{
		var fileName = Path.GetFileName(sourceFilePath);
		var destinationFilePath = Path.Combine(destinationFolderPath, fileName);

		// 既に存在する場合は削除
		if (File.Exists(destinationFilePath))
		{
			File.Delete(destinationFilePath);
		}

		// ファイルを移動
		File.Move(sourceFilePath, destinationFilePath, overwrite: true);

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

/// <summary>
/// 素材候補ファイル/フォルダを表すDTO。
/// MaterialFileItemViewModel に変換する前のデータ転送用です。
/// </summary>
public sealed class MaterialFileCandidate
{
	/// <summary>実ファイルまたは実フォルダのフルパスを取得します。</summary>
	public required string FullPath { get; init; }

	/// <summary>ファイル名またはフォルダ名を取得します。</summary>
	public required string FileName { get; init; }

	/// <summary>ファイルサイズ（バイト）。フォルダの場合は null。</summary>
	public long? Size { get; init; }

	/// <summary>素材アイテムの種別を取得します。</summary>
	public required MaterialItemType Type { get; init; }
}

/// <summary>
/// 素材移動用のDTO。
/// ViewModel から受け取る情報の最小化を目的としています。
/// </summary>
public sealed class MaterialFile
{
	/// <summary>実ファイルまたは実フォルダのフルパスを取得します。</summary>
	public required string FullPath { get; init; }

	/// <summary>素材アイテムの種別を取得します。</summary>
	public required MaterialItemType Type { get; init; }
}

/// <summary>
/// 素材移動の結果を表すDTO。
/// 後続の補償処理で利用できるように、移動した素材と移動不要だった素材を保持します。
/// </summary>
public sealed class MaterialMoveResult
{
	/// <summary>作成または利用した作品フォルダのパスを取得します。</summary>
	public required string SeriesFolderPath { get; init; }

	/// <summary>実際に移動した素材アイテム一覧を取得します。</summary>
	public required IReadOnlyList<MaterialMoveItem> MovedItems { get; init; }

	/// <summary>移動不要だった素材アイテム一覧を取得します（移動元と登録先が同一の場合など）。</summary>
	public required IReadOnlyList<MaterialMoveItem> SkippedItems { get; init; }
}

/// <summary>
/// 移動結果に含まれる素材アイテムを表すDTO。
/// </summary>
public sealed class MaterialMoveItem
{
	/// <summary>移動元のフルパスを取得します。</summary>
	public required string SourcePath { get; init; }

	/// <summary>移動先のフルパスを取得します。</summary>
	public required string DestinationPath { get; init; }

	/// <summary>素材アイテムの種別を取得します。</summary>
	public required MaterialItemType Type { get; init; }
}
