using MangaBinder.Bindings;
using MangaBinder.Bindings.Inspection;
using MangaBinder.Helpers;

namespace MangaBinder.Bindings;

/// <summary>
/// 1巻分のWorkフォルダ実体化を担当するビルダーです。
/// BindingVolume の素材を BindingVolume.WorkFolderPath へ実体化します。
/// </summary>
public class WorkVolumeBuilder
{
	private readonly IVolumeImageProcessor imageProcessor;

	/// <summary>
	/// <see cref="WorkVolumeBuilder"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="imageProcessor">画像処理プロセッサ。Width/Height取得と形式変換に使用します。</param>
	public WorkVolumeBuilder(IVolumeImageProcessor imageProcessor)
	{
		this.imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
	}

	/// <summary>
	/// 指定された複数の BindingVolume（同一 SourcePath に属する）の素材を Work巻フォルダへ実体化します。
	/// 同一物理素材に属するボリュームをまとめて処理することで、将来のArchive処理に対応します。
	/// 実体化した画像を各 BindingVolume.Images に追加します。
	/// 現段階では「実フォルダ素材」のみ対応しています。
	/// </summary>
	/// <param name="volumes">実体化対象の BindingVolume リスト。すべて同じ Material.SourcePath を持つことが前提です。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	/// <exception cref="ArgumentNullException"><paramref name="volumes"/> が null の場合。</exception>
	/// <exception cref="ArgumentException"><paramref name="volumes"/> が空の場合。</exception>
	/// <exception cref="InvalidOperationException">
	/// BindingVolume.WorkFolderPath が null / 空の場合、
	/// または Material.SourcePath が null / 空の場合。
	/// </exception>
	/// <exception cref="DirectoryNotFoundException">素材フォルダが存在しない場合。</exception>
	/// <exception cref="NotSupportedException">対応していない素材タイプの場合。</exception>
	public async ValueTask BuildAsync(IReadOnlyList<BindingVolume> volumes, CancellationToken cancellationToken = default)
	{
		// ① CancellationToken のキャンセル要求を確認する
		cancellationToken.ThrowIfCancellationRequested();

		// ② volumes が null の場合は ArgumentNullException
		if (volumes is null)
		{
			throw new ArgumentNullException(nameof(volumes));
		}

		// ③ volumes が空の場合は ArgumentException
		if (volumes.Count == 0)
		{
			throw new ArgumentException("BindingVolume リストが空です。", nameof(volumes));
		}

		// ④ グループ内の各ボリュームを順次処理する
		foreach (var volume in volumes)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// 各ボリュームを1巻分の処理で実体化する
			await BuildSingleVolumeAsync(volume, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// 指定された1巻分の BindingVolume の素材を Work巻フォルダへ実体化します。
	/// </summary>
	private async ValueTask BuildSingleVolumeAsync(BindingVolume volume, CancellationToken cancellationToken)
	{
		// ① CancellationToken のキャンセル要求を確認する
		cancellationToken.ThrowIfCancellationRequested();

		// ② volume が null の場合は ArgumentNullException
		if (volume is null)
		{
			throw new ArgumentNullException(nameof(volume));
		}

		// ③ BindingVolume.WorkFolderPath が null / 空の場合は InvalidOperationException
		if (string.IsNullOrEmpty(volume.WorkFolderPath))
		{
			throw new InvalidOperationException(
				"BindingVolume.WorkFolderPath が設定されていません。上位工程でWork巻パスが確定していない内部状態不整合です。");
		}

		// ④ Material.SourcePath が null / 空、または素材フォルダが存在しない場合は例外を送出する
		if (string.IsNullOrEmpty(volume.Material.SourcePath))
		{
			throw new InvalidOperationException(
				$"素材の SourcePath が設定されていません。Material: {volume.Material.Name}");
		}

		// ⑤ 現段階では「実フォルダ素材」のみ対応
		// 実フォルダ素材の判定：ItemType == Folder かつ ArchiveEntryPrefix が null または空
		var isRealFolder = volume.Material.ItemType == MaterialItemType.Folder
			&& string.IsNullOrEmpty(volume.Material.ArchiveEntryPrefix);

		if (!isRealFolder)
		{
			throw new NotSupportedException(
				$"素材タイプ {volume.Material.ItemType} はまだ対応していません。Material: {volume.Material.Name}");
		}

		// ⑥ Folder 素材の処理
		await BuildFromFolderAsync(volume, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// 実フォルダ素材をWork巻フォルダへ実体化します。
	/// </summary>
	private async ValueTask BuildFromFolderAsync(BindingVolume volume, CancellationToken cancellationToken)
	{
		var sourcePath = volume.Material.SourcePath;
		var workFolderPath = volume.WorkFolderPath!;

		// ④ 素材フォルダが存在するか確認
		if (!Directory.Exists(sourcePath))
		{
			throw new DirectoryNotFoundException(
				$"素材フォルダが見つかりません。Path: {sourcePath}");
		}

		// ⑤ BindingVolume.WorkFolderPath の巻フォルダを作成する
		Directory.CreateDirectory(workFolderPath);

		// ⑥ 素材フォルダ直下のファイルだけを列挙する
		var sourceDirectory = new DirectoryInfo(sourcePath);
		var files = sourceDirectory.GetFiles();

		// ⑦ 画像ファイルのみをフィルタリング
		var imageFiles = files
			.Where(f => SupportedExtensionHelper.IsImage(f.Extension))
			.ToList();

		// ⑧ ファイル名順に安定した順序に並べる
		imageFiles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

		// 各画像を処理する
		foreach (var sourceFile in imageFiles)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var destinationPath = Path.Combine(workFolderPath, sourceFile.Name);

			// ① BindingImage を生成する（親 BindingVolume を指定）
			var bindingImage = new BindingImage(volume);

			// ② SourceName に sourceFile.Name を設定する
			bindingImage.SourceName = sourceFile.Name;

			// ③ volume.Images に追加する
			volume.Images.Add(bindingImage);

			// ④ 非同期コピー処理を実行する
			// コピー元を非同期対応の FileStream で開く
			using (var sourceStream = new FileStream(
				sourceFile.FullName,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				bufferSize: 81920,
				useAsync: true))
			{
				// コピー先を非同期対応の FileStream で開く
				using (var destinationStream = new FileStream(
					destinationPath,
					FileMode.Create,
					FileAccess.Write,
					FileShare.None,
					bufferSize: 81920,
					useAsync: true))
				{
					// CopyToAsync(..., cancellationToken) でコピーする
					await sourceStream.CopyToAsync(destinationStream, bufferSize: 81920, cancellationToken)
						.ConfigureAwait(false);
				}
			}

			// ⑤ コピーが正常終了した後、同じ BindingImage の FilePath に destinationPath を設定する
			bindingImage.FilePath = destinationPath;

			// ⑥ VolumeImageProcessor で画像を処理する
			// 画像をOpen、Width/Height取得、必要に応じて既定形式へ変換する
			await this.imageProcessor.ProcessAsync(bindingImage, cancellationToken)
				.ConfigureAwait(false);
		}
	}
}
