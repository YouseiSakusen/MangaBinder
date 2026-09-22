using HalationGhost.Utilities;
using MangaBinder.Bindings;
using MangaBinder.Helpers;
using MangaBinder.Settings;

namespace MangaBinder.Bindings;

/// <summary>
/// 巻選択工程における素材の初期化を担当する Manager です。
/// SeriesMaterialFolderLoader の解析結果を BindingStore.Materials へ反映します。
/// </summary>
public class VolumeSelectionManager
{
	private readonly BindingStore bindingStore;
	private readonly SeriesMaterialFolderLoader materialFolderLoader;
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="VolumeSelectionManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="materialFolderLoader">素材フォルダ解析サービス。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	public VolumeSelectionManager(
		BindingStore bindingStore,
		SeriesMaterialFolderLoader materialFolderLoader,
		AppSettings appSettings)
	{
		this.bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
		this.materialFolderLoader = materialFolderLoader ?? throw new ArgumentNullException(nameof(materialFolderLoader));
		this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
	}

	/// <summary>
	/// 製本対象の素材を初期化します。
	/// BindingStore.BindingTarget から対象作品を取得し、
	/// SeriesMaterialFolderLoader で解析した結果を
	/// MaterialItemDto ツリーから MaterialItem ツリーへ変換して
	/// BindingStore.Materials へ格納します。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>初期化処理の結果。</returns>
	/// <exception cref="InvalidOperationException">BindingTarget が設定されていない場合。</exception>
	public async ValueTask<VolumeSelectionInitializeResult> InitializeAsync(
		CancellationToken cancellationToken = default)
	{
		// ① BindingStore.BindingTarget から対象作品を取得
		var bindingTarget = this.bindingStore.BindingTarget.Value;
		if (bindingTarget is null)
		{
			throw new InvalidOperationException(
				"巻選択の初期化には BindingStore.BindingTarget が設定されている必要があります。");
		}

		// ② VolumeSelectionCompleted が true の場合は再初期化を行わない
		if (this.bindingStore.VolumeSelectionCompleted.Value)
		{
			// 既存の素材は保持したまま、成功結果を返す
			// BindingTarget は既に保持しており、Materials/BindingVolumes も前回状態で保持
			return new VolumeSelectionInitializeResult
			{
				Status = MaterialFolderStatus.Success,
				TargetPath = string.Empty,
				HasNestedArchive = false,
				NestedArchiveFileNames = [],
			};
		}

		// ③ 素材展開方法の初期状態を決定（素材Loader実行前に設定）
		this.initializeImageExpansionState(bindingTarget.Series);
		this.initializeVolumeFolderDigits(bindingTarget.Series);

		// ④ SeriesMaterialFolderLoader.GetMaterialsAsync() を呼び出す
		var loaderResult = await this.materialFolderLoader.GetMaterialsAsync(
			bindingTarget.Series,
			cancellationToken);

		// ⑤ Loader 失敗時
		if (loaderResult.Status != MaterialFolderStatus.Success)
		{
			// Materials・BindingVolumes は空のままで返す
			return new VolumeSelectionInitializeResult
			{
				Status = loaderResult.Status,
				TargetPath = loaderResult.TargetPath,
				HasNestedArchive = loaderResult.HasNestedArchive,
				NestedArchiveFileNames = loaderResult.NestedArchiveFileNames,
			};
		}

		// ⑥ Success時は MaterialItemDto → MaterialItem へ再帰変換
		try
		{
			// 変換前に新しい MaterialItem ツリーを作成（完全変換まで Store には入れない）
			var convertedRoots = new List<MaterialItem>();

			foreach (var dtoRoot in loaderResult.Materials)
			{
				var materialItemRoot = this.ConvertMaterialItemDtoToMaterialItem(dtoRoot);
				convertedRoots.Add(materialItemRoot);
			}

			// 変換が完全に成功したので、Store へ反映
			foreach (var materialItem in convertedRoots)
			{
				this.bindingStore.Materials.Add(materialItem);
			}

			return new VolumeSelectionInitializeResult
			{
				Status = MaterialFolderStatus.Success,
				TargetPath = loaderResult.TargetPath,
				HasNestedArchive = loaderResult.HasNestedArchive,
				NestedArchiveFileNames = loaderResult.NestedArchiveFileNames,
			};
		}
		catch
		{
			// 変換途中で例外が発生した場合は、
			// 生成済みの MaterialItem を破棄して例外を再送出
			foreach (var material in this.bindingStore.Materials)
			{
				material.Dispose();
			}
			this.bindingStore.Materials.Clear();

			throw;
		}
	}

	/// <summary>
	/// 指定した MaterialItem の選択状態をトグルします。
	/// 現在 IsChecked が false の場合は SelectMaterial を呼び出し、
	/// true の場合は UnselectMaterial を呼び出します。
	/// </summary>
	/// <param name="material">トグル対象の MaterialItem。</param>
	/// <param name="allowSelectionOverride">
	/// SelectMaterial 呼び出し時に使用する allowSelectionOverride フラグ。
	/// </param>
	/// <exception cref="ArgumentNullException">material が null の場合。</exception>
	public void ToggleMaterialSelection(MaterialItem material, bool allowSelectionOverride = false)
	{
		if (material is null)
		{
			throw new ArgumentNullException(nameof(material));
		}

		if (material.IsChecked.Value)
		{
			this.UnselectMaterial(material);
		}
		else
		{
			this.SelectMaterial(material, allowSelectionOverride);
		}
	}

	/// <summary>
	/// 指定した MaterialItem に対応する BindingVolume を BindingStore.BindingVolumes から削除し、
	/// Material.IsChecked を false に設定します。
	/// BindingVolume が存在しない場合でも IsChecked は false に設定します。
	/// </summary>
	/// <param name="material">選択解除対象の MaterialItem。</param>
	/// <exception cref="ArgumentNullException">material が null の場合。</exception>
	public void UnselectMaterial(MaterialItem material)
	{
		if (material is null)
		{
			throw new ArgumentNullException(nameof(material));
		}

		// BindingVolume を検索
		var volume = this.FindBindingVolumeByMaterial(material);

		// BindingVolume が存在する場合は削除・破棄
		if (volume is not null)
		{
			this.bindingStore.BindingVolumes.Remove(volume);
			volume.Dispose();
		}

		// IsChecked を false に設定（Store内の不整合を残さない）
		material.IsChecked.Value = false;
	}

	/// <summary>
	/// 指定した MaterialItem を製本対象として選択します。
	/// 選択可能な場合、新しい BindingVolume を作成して BindingStore.BindingVolumes に追加し、
	/// Material.IsChecked を true に設定します。
	/// 既に BindingVolume が存在する場合は、IsChecked が true であることだけを保証します。
	/// </summary>
	/// <param name="material">選択対象の MaterialItem。</param>
	/// <param name="allowSelectionOverride">
	/// 選択不可と判定されたアイテムをOverrideしてよいかどうか。
	/// Material.IsSelectableByDefault == false の場合、このフラグが true なら選択可能になります。
	/// </param>
	/// <exception cref="ArgumentNullException">material が null の場合。</exception>
	public void SelectMaterial(MaterialItem material, bool allowSelectionOverride = false)
	{
		if (material is null)
		{
			throw new ArgumentNullException(nameof(material));
		}

		// 選択可否を判定
		if (!this.DetermineMaterialSelectability(material, allowSelectionOverride))
		{
			// 選択不可の場合は何もしない（IsChecked も変更しない）
			return;
		}

		// 既に BindingVolume が存在する場合は何もしない（IsChecked は既に true のはず）
		var existingVolume = this.FindBindingVolumeByMaterial(material);
		if (existingVolume is not null)
		{
			// IsChecked が true であることを確認（念のため）
			if (!material.IsChecked.Value)
			{
				material.IsChecked.Value = true;
			}
			return;
		}

		// 新しい BindingVolume を作成
		var newVolume = new BindingVolume(material);

		// VolumeNumberHelper を使用して巻番号を解析
		try
		{
			var sourceType = this.DetermineVolumeNumberSourceType(material);
			var parseResult = VolumeNumberHelper.Parse(material.Name, sourceType);

			// 単巻として明確に解析できた場合のみ巻番号を設定
			if (parseResult.Kind == VolumeNumberParseKind.Single && parseResult.SingleVolume.HasValue)
			{
				newVolume.VolumeNumber.Value = parseResult.SingleVolume.Value;
			}
			// Range / Unknown / NotVolume の場合は VolumeNumber は null のままにする
		}
		catch
		{
			// VolumeNumberHelper での例外は握り潰さない（巻番号解析失敗は重大）
			newVolume.Dispose();
			throw;
		}

		// BindingVolumes へ挿入
		try
		{
			// IsManualVolumeOrder の状態に応じて挿入ルールを変更
			if (this.bindingStore.IsManualVolumeOrder.Value)
			{
				// 手動並び替え済みの場合は末尾に追加
				this.bindingStore.BindingVolumes.Add(newVolume);
			}
			else
			{
				// 自動挿入（昇順）の場合
				this.InsertBindingVolumeInOrder(newVolume);
			}
		}
		catch
		{
			// BindingVolumes への追加に失敗した場合はクリーンアップ
			newVolume.Dispose();
			throw;
		}

		// 成功したので IsChecked を true に設定
		material.IsChecked.Value = true;

	}

	/// <summary>
	/// BindingVolume を VolumeNumber の昇順で BindingStore.BindingVolumes へ挿入します。
	/// VolumeNumber == null の項目は末尾に配置されます。
	/// </summary>
	/// <param name="bindingVolume">挿入対象の BindingVolume。</param>
	private void InsertBindingVolumeInOrder(BindingVolume bindingVolume)
	{
		var targetVolumeNumber = bindingVolume.VolumeNumber.Value;

		// 昇順挿入位置を探す
		int insertIndex = this.bindingStore.BindingVolumes.Count;

		for (int i = 0; i < this.bindingStore.BindingVolumes.Count; i++)
		{
			var existingVolume = this.bindingStore.BindingVolumes[i];
			var existingVolumeNumber = existingVolume.VolumeNumber.Value;

			// targetVolumeNumber が null の場合は末尾に挿入
			if (targetVolumeNumber is null)
			{
				// null は末尾なので、null でない項目が見つかるまでスキップ
				if (existingVolumeNumber is null)
				{
					// 既に null の項目を見つけたら、その位置に挿入
					insertIndex = i;
					break;
				}
			}
			else
			{
				// targetVolumeNumber が値を持つ場合
				if (existingVolumeNumber is null)
				{
					// 値を持つ項目より後ろにある null 項目に到達したので、その位置に挿入
					insertIndex = i;
					break;
				}

				// どちらも値を持つ場合は昇順比較
				if (targetVolumeNumber < existingVolumeNumber)
				{
					insertIndex = i;
					break;
				}
			}
		}

		this.bindingStore.BindingVolumes.Insert(insertIndex, bindingVolume);
	}

	/// <summary>
	/// BindingStore.BindingVolumes 内の BindingVolume を指定された位置へ移動します。
	/// 移動が実際に実行された場合、IsManualVolumeOrder を true に設定します。
	/// </summary>
	/// <param name="bindingVolume">移動対象の BindingVolume。</param>
	/// <param name="newIndex">移動先のインデックス。</param>
	/// <exception cref="ArgumentNullException">bindingVolume が null の場合。</exception>
	public void MoveBindingVolume(BindingVolume bindingVolume, int newIndex)
	{
		if (bindingVolume is null)
		{
			throw new ArgumentNullException(nameof(bindingVolume));
		}

		// BindingStore.BindingVolumes 内に bindingVolume が存在するかを ReferenceEquals で確認
		int currentIndex = -1;
		for (int i = 0; i < this.bindingStore.BindingVolumes.Count; i++)
		{
			if (ReferenceEquals(this.bindingStore.BindingVolumes[i], bindingVolume))
			{
				currentIndex = i;
				break;
			}
		}

		// 存在しない場合は例外を送出
		if (currentIndex < 0)
		{
			throw new ArgumentException(
				$"指定された BindingVolume は BindingStore.BindingVolumes に含まれていません。",
				nameof(bindingVolume));
		}

		// 同じ位置への移動は何もしない
		if (currentIndex == newIndex)
		{
			return;
		}

		// 境界チェック（ObservableCollections/ObservableList の自然なAPI契約に従う）
		if (newIndex < 0 || newIndex >= this.bindingStore.BindingVolumes.Count)
		{
			throw new ArgumentOutOfRangeException(
				nameof(newIndex),
				$"インデックスは 0 以上 {this.bindingStore.BindingVolumes.Count - 1} 以下である必要があります。");
		}

		// Remove → Insert で同一インスタンスを維持（ObservableList に正式な Move API がない場合の標準パターン）
		this.bindingStore.BindingVolumes.RemoveAt(currentIndex);
		this.bindingStore.BindingVolumes.Insert(newIndex, bindingVolume);

		// 実際に順番が変わったので手動並び替え済みに設定
		this.bindingStore.IsManualVolumeOrder.Value = true;
	}

	/// <summary>
	/// Root 直下の実素材を物理削除し、成功後に BindingStore から除去します。
	/// 削除対象は Folder / Archive / Epub のいずれかである必要があります。
	/// </summary>
	/// <param name="material">削除対象の MaterialItem。Root 直下の Folder / Archive / Epub である必要があります。</param>
	/// <param name="sendToRecycleBin">
	/// true の場合、Windows のごみ箱へ移動します。
	/// false の場合、完全削除します。
	/// </param>
	/// <returns>物理削除と BindingStore からの除去が成功した場合は true。失敗した場合は false。</returns>
	/// <exception cref="ArgumentNullException">material が null の場合。</exception>
	/// <exception cref="InvalidOperationException">
	/// material.CanDeleteMaterial が false、
	/// または material が BindingStore.Materials の Root 直下に存在しない、
	/// または material.ItemType が削除対象外の場合。
	/// </exception>
	public bool DeleteMaterial(MaterialItem material, bool sendToRecycleBin)
	{
		// null チェック
		if (material is null)
		{
			throw new ArgumentNullException(nameof(material));
		}

		// CanDeleteMaterial チェック
		if (!material.CanDeleteMaterial)
		{
			throw new InvalidOperationException(
				"指定された MaterialItem は削除対象ではありません。CanDeleteMaterial が false です。");
		}

		// ItemType の安全側確認
		if (material.ItemType != MaterialItemType.Folder
			&& material.ItemType != MaterialItemType.Archive
			&& material.ItemType != MaterialItemType.Epub)
		{
			throw new InvalidOperationException(
				$"指定された MaterialItem の ItemType ({material.ItemType}) は削除対象ではありません。");
		}

		// BindingStore.Materials 内の各 Root を検索し、ReferenceEquals で対象 material の直接の子を確認
		MaterialItem? parentRoot = null;
		int materialIndexInRoot = -1;

		foreach (var root in this.bindingStore.Materials)
		{
			for (int i = 0; i < root.Children.Count; i++)
			{
				if (ReferenceEquals(root.Children[i], material))
				{
					parentRoot = root;
					materialIndexInRoot = i;
					break;
				}
			}

			if (parentRoot is not null)
			{
				break;
			}
		}

		// 見つからない場合は例外を送出
		if (parentRoot is null || materialIndexInRoot < 0)
		{
			throw new InvalidOperationException(
				"指定された MaterialItem は BindingStore.Materials の Root 直下に存在しません。");
		}

		// 物理削除（完全削除またはごみ箱）
		try
		{
			if (sendToRecycleBin)
			{
				// ごみ箱へ移動（Folder / Archive / Epub の判別は FileSystemHelper 側で行う）
				FileSystemHelper.SendToRecycleBin(material.SourcePath);
			}
			else
			{
				// 完全削除
				switch (material.ItemType)
				{
					case MaterialItemType.Folder:
						Directory.Delete(material.SourcePath, recursive: true);
						break;

					case MaterialItemType.Archive:
					case MaterialItemType.Epub:
						File.Delete(material.SourcePath);
						break;
				}
			}
		}
		catch (IOException)
		{
			// 物理削除失敗時は Store を一切変更しない
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			// 物理削除失敗時は Store を一切変更しない
			return false;
		}

		// 物理削除成功後、BindingStore を更新

		// 1. 既存の UnselectMaterial を呼び出し（BindingVolume 削除と IsChecked = false）
		this.UnselectMaterial(material);

		// 2. 親 Root.Children から対象 Material を削除
		parentRoot.Children.RemoveAt(materialIndexInRoot);

		// 3. Material を Dispose（子ノードを所有している場合も含め）
		material.Dispose();

		// 4. 成功を返す
		return true;
	}

	/// <summary>
	/// 指定した MaterialItem を参照する BindingVolume を BindingStore.BindingVolumes から検索します。
	/// ReferenceEquals で同一インスタンスを判定します。
	/// </summary>
	/// <param name="material">検索対象の MaterialItem。</param>
	/// <returns>見つかった BindingVolume、見つからない場合は null。</returns>
	private BindingVolume? FindBindingVolumeByMaterial(MaterialItem material)
	{
		foreach (var volume in this.bindingStore.BindingVolumes)
		{
			if (ReferenceEquals(volume.Material, material))
			{
				return volume;
			}
		}

		return null;
	}

	/// <summary>
	/// MaterialItem の素材由来から VolumeNumberSourceType を決定します。
	/// </summary>
	/// <param name="material">対象の素材。</param>
	/// <returns>素材由来に対応する VolumeNumberSourceType。</returns>
	private VolumeNumberSourceType DetermineVolumeNumberSourceType(MaterialItem material)
	{
		// ItemType に基づいて判定（ArchiveEntryPrefix ではなく）
		// Archive 内部の Folder ノードも ItemType == Folder なら Folder として扱う
		return material.ItemType switch
		{
			MaterialItemType.Folder => VolumeNumberSourceType.Folder,
			MaterialItemType.Archive => VolumeNumberSourceType.Archive,
			MaterialItemType.Epub => VolumeNumberSourceType.Epub,
			_ => VolumeNumberSourceType.Folder, // デフォルトは Folder
		};
	}

	/// <summary>
	/// MaterialItem が現在選択可能な状態かどうかを判定します。
	/// </summary>
	/// <param name="material">判定対象の素材。</param>
	/// <param name="allowSelectionOverride">選択不可判定をOverrideしてよいかどうか。</param>
	/// <returns>選択可能な場合は true。</returns>
	private bool DetermineMaterialSelectability(MaterialItem material, bool allowSelectionOverride = false)
	{
		// Root は常に選択不可
		if (material.ItemType == MaterialItemType.Root)
		{
			return false;
		}

		// Archive 本体は常に選択不可
		if (material.ItemType == MaterialItemType.Archive)
		{
			return false;
		}

		// IsSelectableByDefault == true なら常に選択可能
		if (material.IsSelectableByDefault)
		{
			return true;
		}

		// IsSelectableByDefault == false で allowSelectionOverride == true なら選択可能
		return allowSelectionOverride;
	}

	/// <summary>
	/// MaterialItemDto を MaterialItem へ再帰的に変換します。
	/// 各ノードの元素材入力元種別（OriginalSourceType）を、
	/// 親の種別と現在のノードの種別から確定します。
	/// </summary>
	/// <param name="dto">変換元の DTO。</param>
	/// <param name="isDirectChildOfRoot">Root の直下かどうか。</param>
	/// <param name="parentSourceType">親ノードの元素材入力元種別。Root の場合は null。</param>
	/// <returns>変換後の MaterialItem。</returns>
	private MaterialItem ConvertMaterialItemDtoToMaterialItem(
		MaterialItemDto dto,
		bool isDirectChildOfRoot = false,
		MaterialSourceType? parentSourceType = null)
	{
		// 元素材入力元種別（OriginalSourceType）を確定
		MaterialSourceType originalSourceType;

		// 親が Archive 配下の場合、子は ItemType が Folder でも Archive 由来
		if (parentSourceType == MaterialSourceType.Archive)
		{
			originalSourceType = MaterialSourceType.Archive;
		}
		// 親が Archive ではなく、現在のノードが Archive の場合
		else if (dto.ItemType == MaterialItemType.Archive)
		{
			originalSourceType = MaterialSourceType.Archive;
		}
		// 現在のノードが Epub の場合
		else if (dto.ItemType == MaterialItemType.Epub)
		{
			originalSourceType = MaterialSourceType.Epub;
		}
		// それ以外（Folder または Root）は Folder
		else
		{
			originalSourceType = MaterialSourceType.Folder;
		}

		// CanEnableSelectionOverride の判定
		var canEnableSelectionOverride =
			dto.ItemType != MaterialItemType.Root
			&& dto.ItemType != MaterialItemType.Archive
			&& !dto.IsSelectableByDefault;

		// CanDeleteMaterial の判定
		var canDeleteMaterial =
			isDirectChildOfRoot
			&& (
				dto.ItemType == MaterialItemType.Folder
				|| dto.ItemType == MaterialItemType.Archive
				|| dto.ItemType == MaterialItemType.Epub);

		var materialItem = new MaterialItem(
			itemType: dto.ItemType,
			originalSourceType: originalSourceType,
			name: dto.Name,
			fullPath: dto.FullPath,
			fileSizeText: dto.FileSizeText,
			fileSizeBytes: dto.FileSizeBytes,
			fileCount: dto.FileCount,
			totalImageBytes: dto.TotalImageBytes,
			sourcePath: dto.SourcePath,
			archiveEntryPrefix: dto.ArchiveEntryPrefix,
			isSelectableByDefault: dto.IsSelectableByDefault,
			selectionDisabledReason: dto.SelectionDisabledReason,
			canEnableSelectionOverride: canEnableSelectionOverride,
			canDeleteMaterial: canDeleteMaterial);

		// 子を再帰的に変換
		// Root の場合だけ isDirectChildOfRoot = true を渡す
		// 親の SourceType は、Archive 配下の判定に使用するため引き継ぐ
		var childIsDirectChildOfRoot = dto.ItemType == MaterialItemType.Root;
		var childParentSourceType = dto.ItemType == MaterialItemType.Archive
			? MaterialSourceType.Archive
			: parentSourceType;

		foreach (var childDto in dto.Children)
		{
			var childMaterialItem = this.ConvertMaterialItemDtoToMaterialItem(
				childDto,
				childIsDirectChildOfRoot,
				childParentSourceType);
			materialItem.Children.Add(childMaterialItem);
		}

		return materialItem;
	}

	/// <summary>
	/// 巻フォルダ名の桁数を初期化します。
	/// MaxVolumeDigits に基づいて、適切な桁数を設定します。
	/// </summary>
	/// <param name="series">対象作品。</param>
	private void initializeVolumeFolderDigits(MangaSeries series)
	{
		this.bindingStore.VolumeFolderDigits.Value =
			Math.Max(2, series.MaxVolumeDigits);
	}

	/// <summary>
	/// Work 作品フォルダの存在に基づいて、素材展開方法の初期状態を決定します。
	/// </summary>
	/// <param name="series">対象作品。</param>
	private void initializeImageExpansionState(MangaSeries series)
	{
		// WorkFolder 設定が無効な場合は Recreate で統一
		if (!this.appSettings.HasValidWorkFolder)
		{
			this.bindingStore.HasExistingWorkFolder.Value = false;
			this.bindingStore.ImageExpansionMethod.Value = global::MangaBinder.Bindings.ImageExpansionMethod.Recreate;
			return;
		}

		// WorkFolder 設定が有効な場合、作品別フォルダの存在確認
		var seriesFolderPath = this.appSettings.CreateWorkSeriesFolderPath(series.Title);
		var folderExists = Directory.Exists(seriesFolderPath);

		if (folderExists)
		{
			// 既存フォルダがある場合は UseExisting
			this.bindingStore.HasExistingWorkFolder.Value = true;
			this.bindingStore.ImageExpansionMethod.Value = global::MangaBinder.Bindings.ImageExpansionMethod.UseExisting;
		}
		else
		{
			// 存在しない場合は Recreate
			this.bindingStore.HasExistingWorkFolder.Value = false;
			this.bindingStore.ImageExpansionMethod.Value = global::MangaBinder.Bindings.ImageExpansionMethod.Recreate;
		}
	}

	/// <summary>
	/// 巻選択工程から次へ進めるかを検証します。
	/// </summary>
	/// <returns>検証結果。</returns>
	public VolumeSelectionValidationResult ValidateVolumeSelection()
	{
		// ① Work フォルダ設定の確認
		if (!this.appSettings.HasValidWorkFolder)
		{
			return new VolumeSelectionValidationResult(
				error: VolumeSelectionValidationError.WorkFolderUnavailable);
		}

		// ② 製本対象0件
		if (this.bindingStore.BindingVolumes.Count == 0)
		{
			return new VolumeSelectionValidationResult(
				error: VolumeSelectionValidationError.NoVolumes);
		}

		// ③ 巻番号未入力
		if (this.bindingStore.BindingVolumes.Any(volume => volume.VolumeNumber.Value == null))
		{
			return new VolumeSelectionValidationResult(
				error: VolumeSelectionValidationError.VolumeNumberMissing);
		}

		// ④ 巻番号重複
		var duplicateVolumeNumbers = this.bindingStore.BindingVolumes
			.GroupBy(volume => volume.VolumeNumber.Value)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key!.Value)
			.OrderBy(x => x)
			.ToList();

		if (duplicateVolumeNumbers.Count > 0)
		{
			return new VolumeSelectionValidationResult(
				error: VolumeSelectionValidationError.DuplicateVolumeNumbers,
				duplicateVolumeNumbers: duplicateVolumeNumbers.AsReadOnly());
		}

		// ⑤ 抜け巻
		var sortedVolumeNumbers = this.bindingStore.BindingVolumes
			.Select(volume => volume.VolumeNumber.Value!.Value)
			.OrderBy(x => x)
			.ToList();

		var hasMissingVolume = false;
		for (int i = 0; i < sortedVolumeNumbers.Count - 1; i++)
		{
			if (sortedVolumeNumbers[i + 1] - sortedVolumeNumbers[i] > 1)
			{
				hasMissingVolume = true;
				break;
			}
		}

		if (hasMissingVolume)
		{
			return new VolumeSelectionValidationResult(
				warning: VolumeSelectionValidationWarning.MissingVolume);
		}

		// ⑥ 問題なし
		return new VolumeSelectionValidationResult();
	}
}
