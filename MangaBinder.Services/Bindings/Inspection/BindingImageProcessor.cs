using MangaBinder.Settings;
using MangaBinder.Helpers;
using NetVips;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 製本前確認新ルート用の画像処理クラスです。
/// BindingImage 1件ずつの処理を担当します。
/// 
/// Simulation（最終的な出力ファイル名を計算）と
/// ProcessAsync（実際に画像処理してWorkへ出力）を提供します。
/// </summary>
public class BindingImageProcessor
{
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="BindingImageProcessor"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。既定画像形式と変換設定を参照します。</param>
	/// <exception cref="ArgumentNullException"><paramref name="appSettings"/> が null の場合。</exception>
	public BindingImageProcessor(AppSettings appSettings)
	{
		this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
	}

	/// <summary>
	/// BindingImage 1件の画像処理後ファイル名を Simulation します。
	/// 計算結果を <see cref="BindingImage.SimulatedFileName"/> に直接設定します。
	/// 
	/// Simulation では以下を判定して、実際に画像処理を行った場合の最終ファイル名を求めます：
	/// - 画像形式の変換が必要か
	/// - 既定形式への統一が必要か
	/// - .jpg と .jpeg の形式統一
	/// 
	/// この計算ロジックは実画像処理でも同じロジックが必要です。
	/// </summary>
	/// <param name="image">Simulation 対象の BindingImage。</param>
	/// <exception cref="ArgumentNullException"><paramref name="image"/> が null の場合。</exception>
	public void Simulate(BindingImage image)
	{
		if (image is null)
		{
			throw new ArgumentNullException(nameof(image));
		}

		// BindingImage.FileName を使用して最終的な出力ファイル名を計算
		var simulatedFileName = CalculateSimulatedFileName(image.FileName);
		image.SimulatedFileName = simulatedFileName;
	}

	/// <summary>
	/// BindingImage 1件を実際に画像処理してWorkへ出力します。
	/// 
	/// 処理フロー：
	/// 1. SkipImageProcessing が true の場合は ProcessStatus = Skipped として終了
	/// 2. 元画像 Stream を取得（TemporaryImageStream または SourceImagePath の物理ファイル）
	/// 3. NetVips で画像を Open し Width / Height を取得
	/// 4. Simulation と同じ変換仕様に従い、以下のいずれかを実行：
	///    - 変換不要 → 元バイト列を Work へ出力
	///    - 同一形式で拡張子のみ異なる → 元バイト列を SimulatedFileName で出力
	///    - 実変換必要 → NetVips で既定形式へ変換して出力
	/// 5. FilePath / FileName / ProcessStatus を更新
	/// 
	/// エラー時は ProcessStatus と ProcessErrorMessage に詳細を設定してください。
	/// </summary>
	/// <param name="image">処理対象の BindingImage。SimulatedFileName が設定済みである必要があります。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	/// <exception cref="ArgumentNullException"><paramref name="image"/> が null の場合。</exception>
	/// <exception cref="InvalidOperationException">
	/// image.SimulatedFileName が null / 空、または image.BindingVolume.WorkFolderPath が null / 空の場合。
	/// </exception>
	/// <exception cref="OperationCanceledException">キャンセルトークンがキャンセルされた場合。</exception>
	public async ValueTask ProcessAsync(BindingImage image, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (image is null)
		{
			throw new ArgumentNullException(nameof(image));
		}

		if (string.IsNullOrEmpty(image.SimulatedFileName))
		{
			throw new InvalidOperationException(
				"BindingImage.SimulatedFileName が設定されていません。Simulation を先に実行してください。");
		}

		if (string.IsNullOrEmpty(image.BindingVolume.WorkFolderPath))
		{
			throw new InvalidOperationException(
				"BindingVolume.WorkFolderPath が設定されていません。SeriesInspectionManager で Work フォルダを準備してください。");
		}

		// 処理状態を初期化
		image.ProcessStatus = BindingImageProcessStatus.NotProcessed;
		image.ProcessErrorMessage = null;

		// SkipImageProcessing が true の場合はスキップして終了
		if (image.SkipImageProcessing)
		{
			image.ProcessStatus = BindingImageProcessStatus.Skipped;
			image.ProcessErrorMessage = null;
			return;
		}

		Stream? sourceStream = null;
		FileStream? ownedFileStream = null;
		Image? vipsImage = null;
		string? sourceImagePath = null;

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			// 元画像 Stream を取得（優先順位：TemporaryImageStream → SourceImagePath の物理ファイル）
			if (image.TemporaryImageStream is not null)
			{
				sourceStream = image.TemporaryImageStream;
				sourceImagePath = image.MaterialImage.SourceImagePath;

				// Seek 可能な場合は Position = 0 に戻す
				if (sourceStream.CanSeek)
				{
					sourceStream.Position = 0;
				}
			}
			else
			{
				sourceImagePath = image.MaterialImage.SourceImagePath;
				if (string.IsNullOrEmpty(sourceImagePath) || !File.Exists(sourceImagePath))
				{
					image.ProcessStatus = BindingImageProcessStatus.ImageOpenFailed;
					image.ProcessErrorMessage = $"素材画像ファイルが見つかりません: {sourceImagePath}";
					return;
				}

				try
				{
					ownedFileStream = new FileStream(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
					sourceStream = ownedFileStream;
				}
				catch (Exception ex)
				{
					image.ProcessStatus = BindingImageProcessStatus.ImageOpenFailed;
					image.ProcessErrorMessage = $"素材画像ファイルを開けません: {ex.Message}";
					return;
				}
			}

			cancellationToken.ThrowIfCancellationRequested();

			// NetVips で画像を Open
			try
			{
				vipsImage = Image.NewFromStream(sourceStream);
			}
			catch (Exception ex)
			{
				image.ProcessStatus = BindingImageProcessStatus.ImageOpenFailed;
				image.ProcessErrorMessage = $"画像を開けません: {ex.Message}";
				return;
			}

			// Width / Height を設定
			image.Width = vipsImage.Width;
			image.Height = vipsImage.Height;

			cancellationToken.ThrowIfCancellationRequested();

			// 出力先パスを確定
			var outputFilePath = Path.Combine(image.BindingVolume.WorkFolderPath, image.SimulatedFileName);

			// Work 入力の場合：出力先 == 入力元パスかどうかを判定（大文字小文字区別なし）
			var sourceAndOutputAreSame = !string.IsNullOrEmpty(sourceImagePath) 
				&& outputFilePath.Equals(sourceImagePath, StringComparison.OrdinalIgnoreCase);

			// 既存ファイルが存在するかチェック
			// ただし、Work 入力で出力先が入力元と同じ場合は正常処理として扱う
			if (File.Exists(outputFilePath) && !sourceAndOutputAreSame)
			{
				image.ProcessStatus = BindingImageProcessStatus.OutputFailed;
				image.ProcessErrorMessage = $"出力先ファイルが既に存在します: {outputFilePath}";
				return;
			}

			cancellationToken.ThrowIfCancellationRequested();

			// 変換仕様を判定して処理を分岐
			var requiresConversion = DetermineRequiresConversion(image);

			// Work 入力で出力先 == 入力元の場合：既存 Work ファイルを再利用する正常処理
			if (sourceAndOutputAreSame)
			{
				try
				{
					// 既存ファイルから Width / Height を取得（ファイルハンドルは再度開く）
					using (var fileStream = new FileStream(outputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						try
						{
							using (var existingImage = Image.NewFromStream(fileStream))
							{
								image.Width = existingImage.Width;
								image.Height = existingImage.Height;
							}
						}
						catch (Exception ex)
						{
							image.ProcessStatus = BindingImageProcessStatus.ImageOpenFailed;
							image.ProcessErrorMessage = $"既存 Work 画像を開けません: {ex.Message}";
							return;
						}
					}

					cancellationToken.ThrowIfCancellationRequested();

					// 既存 Work ファイルをそのまま使用（コピーしない）
					image.FilePath = outputFilePath;
					image.FileName = image.SimulatedFileName;
					image.ProcessStatus = BindingImageProcessStatus.Succeeded;
					image.ProcessErrorMessage = null;
					return;
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex) when (image.ProcessStatus == BindingImageProcessStatus.NotProcessed)
				{
					image.ProcessStatus = BindingImageProcessStatus.OutputFailed;
					image.ProcessErrorMessage = $"既存 Work 画像の処理に失敗しました: {ex.Message}";
					return;
				}
			}

			if (requiresConversion == ConversionType.NoConversionNeeded)
			{
				// 変換不要 → 元バイト列を Work へ出力
				await ProcessNoConversion(image, sourceStream, outputFilePath, cancellationToken);
			}
			else if (requiresConversion == ConversionType.SameFormatExtensionOnly)
			{
				// 同一形式で拡張子のみ異なる → 元バイト列を SimulatedFileName で出力
				await ProcessNoConversion(image, sourceStream, outputFilePath, cancellationToken);
			}
			else // ConversionType.ActualConversionNeeded
			{
				// 実変換必要 → NetVips で既定形式へ変換して出力
				await ProcessWithConversion(image, vipsImage, outputFilePath, cancellationToken);
			}

			cancellationToken.ThrowIfCancellationRequested();

			// 成功時は FilePath / FileName / ProcessStatus を更新
			image.FilePath = outputFilePath;
			image.FileName = image.SimulatedFileName;
			image.ProcessStatus = BindingImageProcessStatus.Succeeded;
			image.ProcessErrorMessage = null;

			// Work 入力の場合：出力先が入力元と異なり、別のファイルを生成した場合は元ファイルを削除
			if (image.ShouldDeleteSourceFile && !sourceAndOutputAreSame)
			{
				try
				{
					// ファイルハンドルが完全に破棄されるまで待機する必要があるため、
					// finally ブロックで FileStream・NetVips Image が Dispose されるまで遅延
					// ここでの削除はそれ以降に実行される
					if (File.Exists(sourceImagePath))
					{
						File.Delete(sourceImagePath);
					}
				}
				catch (Exception ex)
				{
					// 元ファイル削除失敗は ProcessStatus に反映（新しい出力は成功している）
					image.ProcessStatus = BindingImageProcessStatus.OutputFailed;
					image.ProcessErrorMessage = $"元ファイルの削除に失敗しました: {ex.Message}";
				}
			}
		}
		catch (OperationCanceledException)
		{
			// キャンセルは透過的に上位へ
			throw;
		}
		catch (Exception ex) when (image.ProcessStatus == BindingImageProcessStatus.NotProcessed)
		{
			// 予期しない例外で ProcessStatus がまだ NotProcessed の場合は OutputFailed に設定
			image.ProcessStatus = BindingImageProcessStatus.OutputFailed;
			image.ProcessErrorMessage = $"予期しないエラーが発生しました: {ex.Message}";
		}
		finally
		{
			// ownedFileStream（MaterialImage.SourceImagePath から開いた FileStream）は処理終了後に Dispose
			ownedFileStream?.Dispose();

			// NetVips の Image インスタンスは処理終了後に Dispose
			vipsImage?.Dispose();

			// TemporaryImageStream の Dispose は呼び出し側で実施
		}
	}

	/// <summary>
	/// 変換が必要かどうか、また必要な場合はどの種類の変換かを判定します。
	/// </summary>
	private enum ConversionType
	{
		/// <summary>変換不要（元のファイル名・拡張子のままで出力）。</summary>
		NoConversionNeeded,

		/// <summary>同一画像形式で拡張子のみ異なる場合（.jpeg → .jpg 等、再エンコードなし）。</summary>
		SameFormatExtensionOnly,

		/// <summary>実際の画像形式変換が必要（NetVips による変換）。</summary>
		ActualConversionNeeded,
	}

	/// <summary>
	/// BindingImage について、変換が必要かどうかを判定します。
	/// </summary>
	private ConversionType DetermineRequiresConversion(BindingImage image)
	{
		var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
		var defaultExtension = NormalizeExtension(this.appSettings.BindingDefaultImageExtension.Value);

		var requiresConversion = SupportedExtensionHelper.RequiresConversion(extension);
		var convertImagesToDefault = this.appSettings.BindingConvertImagesToDefaultFormat.Value;

		// 変換不要で、既定形式統一もOFFの場合
		if (!requiresConversion && !convertImagesToDefault)
		{
			return ConversionType.NoConversionNeeded;
		}

		// 現在の拡張子が既定形式と同一の画像フォーマットかどうか判定
		if (IsSameImageFormat(extension, defaultExtension))
		{
			// 完全に同じ拡張子の場合は変換不要
			if (extension == defaultExtension)
			{
				return ConversionType.NoConversionNeeded;
			}

			// 実質的には同じ画像形式だが拡張子だけ異なる場合（例：.jpeg → .jpg）
			return ConversionType.SameFormatExtensionOnly;
		}

		// 実際に形式変換が必要な場合
		return ConversionType.ActualConversionNeeded;
	}

	/// <summary>
	/// 変換不要な画像を Work へ出力します。
	/// 元バイト列をそのまま出力先へ非同期コピーします。
	/// </summary>
	private async ValueTask ProcessNoConversion(
		BindingImage image,
		Stream sourceStream,
		string outputFilePath,
		CancellationToken cancellationToken)
	{
		try
		{
			if (!sourceStream.CanSeek)
			{
				// Seek できない場合は複製して Position = 0 にリセット
				var memoryStream = new MemoryStream();
				await sourceStream.CopyToAsync(memoryStream, cancellationToken);
				memoryStream.Position = 0;
				sourceStream = memoryStream;
			}
			else
			{
				sourceStream.Position = 0;
			}

			cancellationToken.ThrowIfCancellationRequested();

			// Work へ非同期出力
			using (var outputFile = new FileStream(outputFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				await sourceStream.CopyToAsync(outputFile, cancellationToken);
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			image.ProcessStatus = BindingImageProcessStatus.OutputFailed;
			image.ProcessErrorMessage = $"ファイル出力に失敗しました: {ex.Message}";

			// 部分ファイルが生成された場合は削除を試みる
			try
			{
				if (File.Exists(outputFilePath))
				{
					File.Delete(outputFilePath);
				}
			}
			catch
			{
				// 削除失敗は元のエラーを上書きしない
			}
		}
	}

	/// <summary>
	/// 実際の画像形式変換が必要な画像を Work へ出力します。
	/// NetVips を使用して既定形式へ変換し、変換済みバイト列を出力します。
	/// </summary>
	private async ValueTask ProcessWithConversion(
		BindingImage image,
		Image vipsImage,
		string outputFilePath,
		CancellationToken cancellationToken)
	{
		MemoryStream? convertedMemoryStream = null;

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			// NetVips で既定形式へ変換
			var defaultExtension = NormalizeExtension(this.appSettings.BindingDefaultImageExtension.Value);
			convertedMemoryStream = new MemoryStream();

			try
			{
				vipsImage.WriteToStream(convertedMemoryStream, defaultExtension);
				convertedMemoryStream.Position = 0;
			}
			catch (Exception ex)
			{
				image.ProcessStatus = BindingImageProcessStatus.ConversionFailed;
				image.ProcessErrorMessage = $"画像形式の変換に失敗しました: {ex.Message}";
				return;
			}

			cancellationToken.ThrowIfCancellationRequested();

			// 変換済みバイト列を Work へ出力
			try
			{
				using (var outputFile = new FileStream(outputFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					await convertedMemoryStream.CopyToAsync(outputFile, cancellationToken);
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				image.ProcessStatus = BindingImageProcessStatus.OutputFailed;
				image.ProcessErrorMessage = $"ファイル出力に失敗しました: {ex.Message}";

				// 部分ファイルが生成された場合は削除を試みる
				try
				{
					if (File.Exists(outputFilePath))
					{
						File.Delete(outputFilePath);
					}
				}
				catch
				{
					// 削除失敗は元のエラーを上書きしない
				}
			}
		}
		finally
		{
			// convertedMemoryStream は処理終了後に Dispose
			convertedMemoryStream?.Dispose();
		}
	}

	/// <summary>
	/// ファイル名を、画像処理後の最終的なファイル名へ変換します。
	/// 拡張子の変換・正規化のみを行い、ファイル名本体は変更しません。
	/// 
	/// VolumeImageProcessor の変換判定ロジックと同じ仕様で実装されています。
	/// </summary>
	/// <param name="fileName">入力ファイル名。</param>
	/// <returns>処理後の最終的なファイル名。</returns>
	private string CalculateSimulatedFileName(string fileName)
	{
		// 拡張子を抽出
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

		// 既定画像形式を正規化（ドット付きに統一）
		var defaultExtension = NormalizeExtension(this.appSettings.BindingDefaultImageExtension.Value);

		// 変換要否の判定
		var requiresConversion = SupportedExtensionHelper.RequiresConversion(extension);
		var convertImagesToDefault = this.appSettings.BindingConvertImagesToDefaultFormat.Value;

		// 変換不要で、既定形式統一もOFFの場合は元のファイル名を返す
		if (!requiresConversion && !convertImagesToDefault)
		{
			return fileName;
		}

		// 現在の拡張子が既定形式と同一の画像フォーマットかどうか判定
		if (IsSameImageFormat(extension, defaultExtension))
		{
			// 完全に同じ拡張子の場合はそのままのファイル名を返す
			if (extension == defaultExtension)
			{
				return fileName;
			}

			// 実質的には同じ画像形式だが拡張子だけ異なる場合（例：.jpeg → .jpg）
			// 拡張子のみを既定形式へ変更
			return fileNameWithoutExtension + defaultExtension;
		}

		// 実際に形式変換が必要な場合、拡張子を既定形式へ変更
		return fileNameWithoutExtension + defaultExtension;
	}

	/// <summary>
	/// 拡張子を正規化してドット付きに統一します。
	/// </summary>
	/// <param name="extension">正規化対象の拡張子。</param>
	/// <returns>正規化された拡張子（ドット付き、小文字）。</returns>
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
	/// 2つの拡張子が同一の画像形式かどうかを判定します。
	/// ".jpg" と ".jpeg" は同一として扱われます。
	/// </summary>
	/// <param name="ext1">比較対象の拡張子1。</param>
	/// <param name="ext2">比較対象の拡張子2。</param>
	/// <returns>同一の画像形式の場合は true。</returns>
	private static bool IsSameImageFormat(string ext1, string ext2)
	{
		ext1 = ext1.ToLowerInvariant();
		ext2 = ext2.ToLowerInvariant();

		// ".jpg" と ".jpeg" は同一のJPEG形式
		if ((ext1 == ".jpg" || ext1 == ".jpeg") && (ext2 == ".jpg" || ext2 == ".jpeg"))
			return true;

		return ext1 == ext2;
	}
}
