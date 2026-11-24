using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 展開先作品フォルダ直下に存在する、1巻分の物理フォルダを表すモデルです。
/// 製本処理の入力である <see cref="MaterialItem"/> とは独立した概念で、
/// 製本処理へ渡す素材は <see cref="CreateMaterial"/> で都度生成します。
/// </summary>
public class ExpandedVolume : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>展開先巻フォルダ名を取得します。フォルダ移動時のみ <see cref="UpdateFolderLocation"/> で更新されます。</summary>
	public string Name { get; private set; }

	/// <summary>実フォルダのフルパスを取得します。フォルダ移動時のみ <see cref="UpdateFolderLocation"/> で更新されます。</summary>
	public string FullPath { get; private set; }

	/// <summary>フォルダ名から取得した巻番号を取得します。生成時に決定し、変更されません。</summary>
	public decimal VolumeNumber { get; }

	/// <summary>対応画像ファイル数を取得します。フォルダ再走査により <see cref="UpdateFolderState"/> で更新されます。</summary>
	public int FileCount { get; private set; }

	/// <summary>対応画像ファイルの合計サイズ（バイト）を取得します。フォルダ再走査により <see cref="UpdateFolderState"/> で更新されます。</summary>
	public long TotalImageBytes { get; private set; }

	/// <summary>展開済み巻追加Dialogで使用するチェック状態を取得します。初期値は false です。フォルダ再走査では初期化されません。</summary>
	public BindableReactiveProperty<bool> IsChecked { get; }

	/// <summary>
	/// この巻に対応する元素材を取得または設定します。特定できない場合は null です。
	/// 参照のみを保持し、所有権は持ちません。<see cref="Dispose"/> でも破棄しません。
	/// </summary>
	public MaterialItem? Source { get; set; }

	/// <summary>
	/// <see cref="ExpandedVolume"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="name">展開先巻フォルダ名。</param>
	/// <param name="fullPath">実フォルダのフルパス。</param>
	/// <param name="volumeNumber">フォルダ名から取得した巻番号。</param>
	/// <param name="fileCount">対応画像ファイル数。</param>
	/// <param name="totalImageBytes">対応画像の合計サイズ（バイト）。</param>
	/// <param name="source">対応する元素材。特定できない場合は null。</param>
	public ExpandedVolume(
		string name,
		string fullPath,
		decimal volumeNumber,
		int fileCount,
		long totalImageBytes,
		MaterialItem? source = null)
	{
		this.Name = name;
		this.FullPath = fullPath;
		this.VolumeNumber = volumeNumber;
		this.FileCount = fileCount;
		this.TotalImageBytes = totalImageBytes;
		this.Source = source;

		this.IsChecked = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// フォルダ再走査の結果を反映します。<see cref="IsChecked"/> と <see cref="Source"/> は変更しません。
	/// </summary>
	/// <param name="fileCount">対応画像ファイル数。</param>
	/// <param name="totalImageBytes">対応画像の合計サイズ（バイト）。</param>
	public void UpdateFolderState(int fileCount, long totalImageBytes)
	{
		this.FileCount = fileCount;
		this.TotalImageBytes = totalImageBytes;
	}

	/// <summary>
	/// 物理フォルダの移動（リネーム）が成功した後に、フォルダ名とフルパスを更新します。
	/// 巻番号・<see cref="IsChecked"/>・<see cref="Source"/> は変更しません。
	/// </summary>
	/// <param name="name">移動後のフォルダ名。</param>
	/// <param name="fullPath">移動後のフォルダのフルパス。</param>
	public void UpdateFolderLocation(string name, string fullPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

		this.Name = name;
		this.FullPath = fullPath;
	}

	/// <summary>
	/// この巻の情報から、製本処理の入力として使用する新しい <see cref="MaterialItem"/> を生成します。
	/// 呼び出すたびに新しいインスタンスを返し、生成した <see cref="MaterialItem"/> は保持しません。
	/// 生成した <see cref="MaterialItem"/> の破棄は呼び出し側の責務です。
	/// </summary>
	/// <returns>新しい <see cref="MaterialItem"/>。<see cref="MaterialItem.Source"/> には <see cref="Source"/> が設定されます。</returns>
	public MaterialItem CreateMaterial()
		=> new MaterialItem(
			itemType: MaterialItemType.Folder,
			originalSourceType: MaterialSourceType.WorkFolder,
			name: this.Name,
			fullPath: this.FullPath,
			fileCount: this.FileCount,
			totalImageBytes: this.TotalImageBytes,
			sourcePath: this.FullPath)
		{
			Source = this.Source,
		};

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
