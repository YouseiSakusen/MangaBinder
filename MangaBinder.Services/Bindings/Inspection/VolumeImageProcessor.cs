using NetVips;
using MangaBinder.Settings;
using MangaBinder.Helpers;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// NetVips を使用した製本用画像変換実装です。
/// </summary>
public sealed class VolumeImageProcessor : IVolumeImageProcessor
{
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="VolumeImageProcessor"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。既定画像形式と変換設定を参照します。</param>
	public VolumeImageProcessor(AppSettings appSettings)
	{
		this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
	}

	/// <inheritdoc />
	public ValueTask<ConvertedImageResult> ConvertAsync(
		Stream sourceStream,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		using var image = Image.NewFromStream(sourceStream);

		var width = image.Width;
		var height = image.Height;

		var output = new MemoryStream();
		image.WriteToStream(output, ".jpg");
		output.Position = 0;

		return ValueTask.FromResult(new ConvertedImageResult(output, width, height));
	}

	/// <summary>
	/// Workへ実体化済みの BindingImage を処理します。
	/// 画像をOpenして Width/Height を取得し、必要に応じて既定形式へ変換します。
	/// 同じ BindingImage インスタンスを更新して返します。
	/// </summary>
	/// <param name="image">処理対象の BindingImage。FilePath が設定済みである必要があります。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	/// <exception cref="ArgumentNullException"><paramref name="image"/> が null の場合。</exception>
	/// <exception cref="InvalidOperationException">BindingImage.FilePath が null / 空の場合。</exception>
	public async ValueTask ProcessAsync(BindingImage image, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (image is null)
		{
			throw new ArgumentNullException(nameof(image));
		}

		if (string.IsNullOrEmpty(image.FilePath))
		{
			throw new InvalidOperationException(
				"BindingImage.FilePath が設定されていません。Workへの実体化前に ProcessAsync が呼ばれています。");
		}

		// NetVips での処理はバックグラウンド実行（同期APIのため UI スレッドを保護）
		await Task.Run(() => ProcessImageInternal(image, cancellationToken), cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// BindingImage の実際の処理（NetVips 操作）を行います。
	/// </summary>
	private void ProcessImageInternal(BindingImage image, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var filePath = image.FilePath!;

		// NetVips で画像をOpen
		using var vipsImage = Image.NewFromFile(filePath);

		var width = vipsImage.Width;
		var height = vipsImage.Height;

		// Width / Height を BindingImage に設定
		image.Width = width;
		image.Height = height;

		// 変換要否の判定
		// FileName が設定されていれば FileName、設定されていなければ FilePath を使用
		var nameForConversionCheck = !string.IsNullOrEmpty(image.FileName)
			? image.FileName
			: filePath;

		var requiresConversion = SupportedExtensionHelper.RequiresConversionForFile(nameForConversionCheck);
		var convertImagesToDefault = this.appSettings.BindingConvertImagesToDefaultFormat.Value;

		// 変換不要の場合はそのまま終了
		if (!requiresConversion && !convertImagesToDefault)
		{
			return;
		}

		// 既定画像形式を正規化（ドット付きに統一）
		var defaultExtension = NormalizeExtension(this.appSettings.BindingDefaultImageExtension.Value);
		var currentExtension = Path.GetExtension(filePath).ToLowerInvariant();

		// 現在の拡張子が既定形式と同一の場合
		if (IsSameImageFormat(currentExtension, defaultExtension))
		{
			// 完全に同じ拡張子の場合は何もしない
			if (currentExtension == defaultExtension)
			{
				return;
			}

			// 実質的には同じ画像形式だが拡張子だけ異なる場合は、再エンコードなくrenameする
			var finalFilePath = RenameImageExtensionOnly(filePath, defaultExtension, cancellationToken);
			image.FilePath = finalFilePath;
			return;
		}

		// 実際に形式変換が必要な場合
		var convertedFinalFilePath = ConvertImageToDefaultFormat(filePath, defaultExtension, cancellationToken);
		image.FilePath = convertedFinalFilePath;
	}

	/// <summary>
	/// 拡張子を正規化してドット付きに統一します。
	/// </summary>
	private static string NormalizeExtension(string? extension)
	{
		if (string.IsNullOrEmpty(extension))
			return ".jpg";

		extension = extension.ToLowerInvariant();
		if (!extension.StartsWith("."))
			extension = "." + extension;

		return extension;
	}

	/// <summary>
	/// 実質的に同じ画像形式だが拡張子表記だけ異なるファイルを、
	/// 再エンコードなく既定拡張子へrenameします。
	/// 例：001.jpeg → 001.jpg
	/// </summary>
	/// <returns>rename後の最終ファイルパス。</returns>
	private static string RenameImageExtensionOnly(string filePath, string targetExtension, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var folderPath = Path.GetDirectoryName(filePath)!;
		var fileName = Path.GetFileNameWithoutExtension(filePath);

		// 既定拡張子を付けた最終パスを生成
		var newFileName = fileName + targetExtension;
		var finalFilePath = Path.Combine(folderPath, newFileName);

		// 上書き不可のコピー（既存ファイルがあれば例外）
		if (File.Exists(finalFilePath))
		{
			throw new InvalidOperationException(
				$"出力先ファイルが既に存在します: {finalFilePath}");
		}

		// 既存ファイルをrenameする
		File.Move(filePath, finalFilePath, overwrite: false);

		return finalFilePath;
	}

	/// <summary>
	/// 2つの拡張子が同一の画像形式かどうかを判定します。
	/// ".jpg" と ".jpeg" は同一として扱います。
	/// </summary>
	private static bool IsSameImageFormat(string ext1, string ext2)
	{
		ext1 = ext1.ToLowerInvariant();
		ext2 = ext2.ToLowerInvariant();

		// ".jpg" と ".jpeg" は同一のJPEG形式
		if ((ext1 == ".jpg" || ext1 == ".jpeg") && (ext2 == ".jpg" || ext2 == ".jpeg"))
			return true;

		return ext1 == ext2;
	}

	/// <summary>
	/// 画像を既定形式へ変換します。
	/// 一時フォルダ ".after" を使用してから元画像を削除・置き換えます。
	/// 正常終了時に最終ファイルパスを返します。
	/// </summary>
	/// <returns>変換後の最終ファイルパス。</returns>
	private static string ConvertImageToDefaultFormat(string filePath, string targetExtension, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var folderPath = Path.GetDirectoryName(filePath)!;
		var fileName = Path.GetFileNameWithoutExtension(filePath);
		var afterFolderPath = Path.Combine(folderPath, ".after");

		try
		{
			// 一時フォルダを作成
			Directory.CreateDirectory(afterFolderPath);

			// 変換後ファイル名を生成
			var convertedFileName = fileName + targetExtension;
			var convertedFilePath = Path.Combine(afterFolderPath, convertedFileName);

			// NetVips で画像を開いて変換
			using (var sourceImage = Image.NewFromFile(filePath))
			{
				sourceImage.WriteToFile(convertedFilePath);
			}

			cancellationToken.ThrowIfCancellationRequested();

			// 変換後ファイルを最終パスへ移動
			var finalFilePath = Path.Combine(folderPath, convertedFileName);

			// 上書き不可のコピー（既存ファイルがあれば例外）
			if (File.Exists(finalFilePath))
			{
				throw new InvalidOperationException(
					$"出力先ファイルが既に存在します: {finalFilePath}");
			}

			File.Move(convertedFilePath, finalFilePath, overwrite: false);

			// 元画像を削除
			File.Delete(filePath);

			// .after フォルダが空なら削除
			if (Directory.GetFiles(afterFolderPath).Length == 0)
			{
				Directory.Delete(afterFolderPath);
			}

			return finalFilePath;
		}
		catch
		{
			// 例外発生時も .after フォルダの後始末を試みる
			try
			{
				if (Directory.Exists(afterFolderPath))
				{
					Directory.Delete(afterFolderPath, recursive: true);
				}
			}
			catch
			{
				// 後始末失敗時も元の例外を再throw
			}

			throw;
		}
	}
}
