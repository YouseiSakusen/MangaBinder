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
	/// 移動処理全体はバックグラウンドスレッドで実行されます。
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
	public ValueTask<MaterialMoveResult> MoveMaterialsAsync(
		SourceFolder destinationSourceFolder,
		string materialFolderName,
		IEnumerable<MaterialFile> materialFiles)
	{
		ArgumentNullException.ThrowIfNull(destinationSourceFolder);
		ArgumentException.ThrowIfNullOrEmpty(materialFolderName);
		ArgumentNullException.ThrowIfNull(materialFiles);

		// バックグラウンドスレッドで実行
		return new ValueTask<MaterialMoveResult>(
			Task.Run(() => this.moveMaterialsSync(destinationSourceFolder, materialFolderName, materialFiles)));
	}

	/// <summary>
	/// 素材ファイル/フォルダを移動する実際の処理をバックグラウンドで実行します。
	/// UIスレッドをブロックしないよう、Task.Run内で呼び出されることを想定しています。
	/// </summary>
	private MaterialMoveResult moveMaterialsSync(
		SourceFolder destinationSourceFolder,
		string materialFolderName,
		IEnumerable<MaterialFile> materialFiles)
	{
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
					this.moveArchiveFile(sourcePath, seriesFolderPathFull, movedItems, materialFile.Type);
					break;

				case MaterialItemType.Folder:
					// 画像フォルダ：フォルダごと作品フォルダ直下へ移動
					this.moveFolder(sourcePath, seriesFolderPathFull, movedItems, materialFile.Type);
					break;

				case MaterialItemType.Epub:
					// epub ファイル：作品フォルダ直下へ移動
					this.moveEpubFile(sourcePath, seriesFolderPathFull, movedItems, materialFile.Type);
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
	private void moveArchiveFile(
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
	}

	/// <summary>
	/// 画像フォルダをフォルダごと指定された作品フォルダ直下へ移動します。
	/// 移動先に同名フォルダが既に存在する場合は例外を投げます。
	/// </summary>
	/// <exception cref="InvalidOperationException">移動先に同名フォルダが既に存在する場合。</exception>
	private void moveFolder(
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
	}

	/// <summary>
	/// epub ファイルを指定された作品フォルダ直下へ移動します。
	/// 移動先に同名ファイルが既に存在する場合は例外を投げます。
	/// </summary>
	/// <exception cref="InvalidOperationException">移動先に同名ファイルが既に存在する場合。</exception>
	private void moveEpubFile(
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
	}

	/// <summary>
	/// 素材候補パスを解析し、EditorPageViewModel.MaterialFiles に追加可能な候補を返します。
	/// </summary>
	/// <remarks>
	/// 処理フロー：
	/// - ファイルの場合：SupportedFileExtensions のアーカイブファイルのみ許可
	/// - フォルダの場合：配下を再帰的に探索し、SupportedFileExtensions に含まれるサポート対象ファイルが1つでも存在する場合、
	///   ドロップされた最上位フォルダを素材フォルダとして追加。サブフォルダ自体は登録しない。
	///   アクセス不可能なフォルダが存在する場合は、そのフォルダのみをスキップし、他のフォルダの検索は継続する。
	/// - 重複排除：
	///   - 同一 FullPath は追加しない
	///   - 既に existingFullPaths に存在するパスも追加しない
	///   - 同じ入力内で重複しているパスも追加しない
	/// </remarks>
	/// <param name="paths">解析対象のパス一覧（ファイルおよびフォルダパス）。</param>
	/// <param name="existingFullPaths">既存の素材ファイル一覧。重複チェックに使用します。</param>
	/// <returns>追加可能な素材候補 DTO の列挙。</returns>
	/// <remarks>
	/// 指定されたフォルダ配下を再帰的に探索し、SupportedFileExtensions に含まれるサポート対象ファイルが存在するかを調べます。
	/// アクセス不可能なフォルダが存在する場合は、そのフォルダのみをスキップし、他のフォルダの検索は継続します。
	/// </remarks>
	private bool ContainsSupportedFile(string folderPath)
	{
		try
		{
			// フォルダ直下のファイルをチェック
			foreach (var file in Directory.GetFiles(folderPath))
			{
				var ext = Path.GetExtension(file);
				if (SupportedExtensionHelper.GetFileType(ext) != null)
				{
					return true;
				}
			}

			// サブフォルダを再帰的に探索
			foreach (var subFolder in Directory.GetDirectories(folderPath))
			{
				if (this.ContainsSupportedFile(subFolder))
				{
					return true;
				}
			}
		}
		catch
		{
			// アクセス不可能なフォルダはスキップ
		}

		return false;
	}

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
				// フォルダの場合：配下を再帰的に探索してサポート対象ファイルが存在するかをチェック
				try
				{
					// 配下にサポート対象ファイルが存在するかを確認
					if (this.ContainsSupportedFile(fullPath))
					{
						// サポート対象ファイルが存在する場合、フォルダ自体を素材フォルダとして追加
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
				}
				catch
				{
					// ディレクトリアクセス エラーの場合はスキップ
					processedPaths.Add(fullPath);
					continue;
				}
			}
		}

		return result;
	}

	/// <summary>
	/// 指定された素材フォルダ（MangaSource のうち FolderRole.Material のみ）を、新しいタイトルへリネームします。
	/// 複数の MaterialSource が存在する場合はすべてを対象にします。
	/// 物理フォルダのリネームのみを実行し、DB やメモリ上の Path は更新しません。
	/// </summary>
	/// <remarks>
	/// リネーム処理：
	/// - 対象は FolderRole.Material のフォルダのみ（BindingSource は対象外）
	/// - 各フォルダについて：
	///   - 現在パスが存在しない場合 → InvalidOperationException を投げる
	///   - 変更後パスが既に存在する場合 → InvalidOperationException を投げる
	///   - 現在パスと変更後パスが同一の場合 → 何もしない
	///   - 上記以外の場合 → Directory.Move() でリネーム
	/// 
	/// パスの正規化：
	/// - Path.GetFullPath() でパス文字列を正規化した上で比較・操作
	/// - 大文字小文字の区別：OrdinalIgnoreCase で比較
	/// 
	/// DB 更新：
	/// - このメソッドでは DB の MangaSources.Path は更新しません
	/// - 呼び出し元（将来の保存処理）で Path 更新を扱うことを想定
	/// </remarks>
	/// <param name="materialSources">リネーム対象の MangaSource の列挙。FolderRole.Material のみが処理対象。</param>
	/// <param name="newTitle">新しい作品タイトル。</param>
	/// <exception cref="ArgumentNullException">materialSources または newTitle が null の場合。</exception>
	/// <exception cref="ArgumentException">newTitle が空文字または空白のみの場合。</exception>
	/// <exception cref="InvalidOperationException">
	/// 以下の場合に投げられます：
	/// - 現在パスが存在しない
	/// - 変更後パスが既に存在する
	/// </exception>
	public ValueTask RenameMaterialFoldersAsync(
		IEnumerable<MangaSource> materialSources,
		string newTitle)
	{
		ArgumentNullException.ThrowIfNull(materialSources);
		ArgumentException.ThrowIfNullOrEmpty(newTitle);

		// Material ロールのみを対象にする
		var targetSources = materialSources
			.Where(s => s.Role == FolderRole.Material)
			.ToList();

		// 各ソースについてリネーム処理を実行
		foreach (var source in targetSources)
		{
			var currentPath = Path.GetFullPath(source.Path);
			var currentParentPath = Path.GetDirectoryName(currentPath);

			if (string.IsNullOrEmpty(currentParentPath))
			{
				throw new InvalidOperationException(
					$"親フォルダの取得に失敗しました。現在パス: {currentPath}");
			}

			var newPath = Path.Combine(currentParentPath, newTitle);
			var newPathFull = Path.GetFullPath(newPath);

			// 現在パスが存在しない場合は例外を投げる
			if (!Directory.Exists(currentPath))
			{
				throw new InvalidOperationException(
					$"現在の素材フォルダが存在しません。パス: {currentPath}");
			}

			// 変更後パスが既に存在する場合は例外を投げる
			if (Directory.Exists(newPathFull))
			{
				throw new InvalidOperationException(
					$"変更後のパスが既に存在しています。パス: {newPathFull}");
			}

			// 現在パスと変更後パスが同一の場合は何もしない
			if (string.Equals(currentPath, newPathFull, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			// フォルダをリネーム
			Directory.Move(currentPath, newPathFull);
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// 作品に紐づく素材フォルダを削除します。
	/// FolderRole.Material のみを削除対象とし、FolderRole.Binding および FolderRole.DefaultBinding は削除しません。
	/// 削除処理はバックグラウンドスレッドで実行されます。
	/// </summary>
	/// <remarks>
	/// 削除対象：
	/// - FolderRole.Material の MangaSource に対応するフォルダ
	/// 
	/// 削除対象外：
	/// - FolderRole.Binding のフォルダ
	/// - FolderRole.DefaultBinding のフォルダ
	/// 
	/// 処理動作：
	/// - Path.GetFullPath() で正規化してからフォルダ削除を実行
	/// - 対象フォルダが存在しない場合は何もしない
	/// - Directory.Delete(path) で空の素材フォルダのみ削除する
	/// - フォルダ内にファイルまたはサブフォルダが存在する場合は例外がスローされ、そのまま呼び出し元へ送出される
	/// </remarks>
	/// <param name="materialSources">削除対象の MangaSource の列挙。</param>
	/// <returns>完了を表す ValueTask。</returns>
	/// <exception cref="ArgumentNullException">materialSources が null の場合にスローされます。</exception>
	/// <exception cref="IOException">フォルダが空でない場合にスローされます。</exception>
	public ValueTask DeleteMaterialFoldersAsync(IEnumerable<MangaSource> materialSources)
	{
		ArgumentNullException.ThrowIfNull(materialSources);

		// バックグラウンドスレッドで実行
		return new ValueTask(
			Task.Run(() => this.deleteMaterialFoldersSync(materialSources)));
	}

	/// <summary>
	/// 素材フォルダを削除する実際の処理をバックグラウンドで実行します。
	/// UIスレッドをブロックしないよう、Task.Run内で呼び出されることを想定しています。
	/// </summary>
	private void deleteMaterialFoldersSync(IEnumerable<MangaSource> materialSources)
	{
		// FolderRole.Material のみを削除対象にする
		var targetSources = materialSources
			.Where(s => s.Role == FolderRole.Material)
			.ToList();

		// 各フォルダをリセット
		foreach (var source in targetSources)
		{
			var folderPath = Path.GetFullPath(source.Path);

			// 対象フォルダが存在しない場合は何もしない
			if (!Directory.Exists(folderPath))
			{
				continue;
			}

			// 空の素材フォルダのみ削除する
			Directory.Delete(folderPath);
		}
	}
}
