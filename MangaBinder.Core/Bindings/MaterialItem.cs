using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程中に扱う素材エンティティです。
/// SeriesMaterialFolderLoader から取得した素材構造を、
/// 製本工程中の状態として BindingStore が保持するための素材モデルです。
/// </summary>
public class MaterialItem : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 素材アイテムの種別を取得します。
	/// </summary>
	public MaterialItemType ItemType { get; init; }

	/// <summary>
	/// ノードの表示名を取得します。
	/// </summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>
	/// ノードのフルパスを取得します。
	/// </summary>
	public string FullPath { get; init; } = string.Empty;

	/// <summary>
	/// Archive ノードのファイルサイズ表示用テキストを取得します（例：「1.2 GB」）。
	/// Archive 以外は空文字。
	/// </summary>
	public string FileSizeText { get; init; } = string.Empty;

	/// <summary>
	/// 製本対象としてカウントするファイル数を取得します。
	/// </summary>
	public int FileCount { get; init; }

	/// <summary>
	/// ノード直下に存在する画像ファイルの展開後サイズ合計（バイト）を取得します。
	/// FileCount と同じ粒度です。
	/// </summary>
	public long TotalImageBytes { get; init; }

	/// <summary>
	/// 解凍元の実パスを取得します。
	/// 実フォルダの場合はフォルダパス、Archive 内部フォルダの場合は Archive ファイルパス、Epub の場合は Epub ファイルパス。
	/// </summary>
	public string SourcePath { get; init; } = string.Empty;

	/// <summary>
	/// Archive 内部フォルダの場合のエントリ接頭辞を取得します。
	/// 実フォルダ・Epub では空文字。
	/// </summary>
	public string ArchiveEntryPrefix { get; init; } = string.Empty;

	/// <summary>
	/// デフォルトで選択可能なノードかどうかを取得します。
	/// </summary>
	public bool IsSelectableByDefault { get; init; }

	/// <summary>
	/// 選択不可の理由を取得します。
	/// 選択可能なノードの場合は空文字。
	/// </summary>
	public string SelectionDisabledReason { get; init; } = string.Empty;

	/// <summary>
	/// この素材に対して「チェックを有効にする」操作を使用できるかを取得します。
	/// </summary>
	public bool CanEnableSelectionOverride { get; init; }

	/// <summary>
	/// この素材に対して「素材を削除」操作を使用できるかを取得します。
	/// </summary>
	public bool CanDeleteMaterial { get; init; }

	/// <summary>
	/// この素材が製本対象として選択されているかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> IsChecked { get; }

	/// <summary>
	/// 子素材一覧を取得します。
	/// </summary>
	public ObservableList<MaterialItem> Children { get; }

	/// <summary>
	/// <see cref="MaterialItem"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="itemType">素材アイテムの種別。</param>
	/// <param name="name">表示名。</param>
	/// <param name="fullPath">フルパス。</param>
	/// <param name="fileSizeText">ファイルサイズ表示用テキスト。Archive 以外は空文字。</param>
	/// <param name="fileCount">製本対象としてカウントするファイル数。</param>
	/// <param name="totalImageBytes">展開後の画像ファイル総サイズ（バイト）。</param>
	/// <param name="sourcePath">解凍元の実パス。</param>
	/// <param name="archiveEntryPrefix">Archive 内部フォルダの場合のエントリ接頭辞。実フォルダ・Epub では空文字。</param>
	/// <param name="isSelectableByDefault">デフォルトで選択可能かどうか。</param>
	/// <param name="selectionDisabledReason">選択不可の理由。選択可能な場合は空文字。</param>
	/// <param name="canEnableSelectionOverride">「チェックを有効にする」操作を使用できるかどうか。</param>
	/// <param name="canDeleteMaterial">「素材を削除」操作を使用できるかどうか。</param>
	public MaterialItem(
		MaterialItemType itemType,
		string name,
		string fullPath,
		string fileSizeText = "",
		int fileCount = 0,
		long totalImageBytes = 0L,
		string sourcePath = "",
		string archiveEntryPrefix = "",
		bool isSelectableByDefault = true,
		string selectionDisabledReason = "",
		bool canEnableSelectionOverride = false,
		bool canDeleteMaterial = false)
	{
		this.ItemType = itemType;
		this.Name = name;
		this.FullPath = fullPath;
		this.FileSizeText = fileSizeText;
		this.FileCount = fileCount;
		this.TotalImageBytes = totalImageBytes;
		this.SourcePath = sourcePath;
		this.ArchiveEntryPrefix = archiveEntryPrefix;
		this.IsSelectableByDefault = isSelectableByDefault;
		this.SelectionDisabledReason = selectionDisabledReason;
		this.CanEnableSelectionOverride = canEnableSelectionOverride;
		this.CanDeleteMaterial = canDeleteMaterial;

		this.IsChecked = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.Children = new ObservableList<MaterialItem>();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		foreach (var child in this.Children)
		{
			child.Dispose();
		}

		this.disposableBag.Dispose();
	}

	/// <summary>
	/// この素材内の1画像を表す MaterialImage を生成します。
	/// </summary>
	/// <param name="sourceImagePath">素材内でこの画像を特定するパス。</param>
	/// <param name="fileName">展開直後に使用するファイル名。</param>
	/// <returns>生成された MaterialImage。MaterialImage.MaterialItem は this に設定されています。</returns>
	/// <exception cref="ArgumentNullException"><paramref name="sourceImagePath"/> または <paramref name="fileName"/> が null の場合。</exception>
	public MaterialImage CreateMaterialImage(string sourceImagePath, string fileName)
	{
		return MaterialImage.Create(this, sourceImagePath, fileName);
	}
}
