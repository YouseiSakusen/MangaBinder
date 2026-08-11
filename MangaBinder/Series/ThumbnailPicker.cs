using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MangaBinder.Settings;
using MangaBinder.Helpers;

namespace MangaBinder.Series;

/// <summary>
/// WPF 用のサムネイル操作クラスです。
/// Clipboard や画像ファイルから BitmapSource を取得し、byte[] へ変換する機能を提供します。
/// </summary>
public class ThumbnailPicker
{
	private readonly IThumbnailImageProcessor thumbnailImageProcessor;
	private readonly IMangaBinderConfig config;

	/// <summary>
	/// ThumbnailPicker を初期化します。
	/// </summary>
	/// <param name="thumbnailImageProcessor">サムネイル生成プロセッサ。</param>
	/// <param name="config">アプリケーション設定を提供するインターフェース。</param>
	public ThumbnailPicker(IThumbnailImageProcessor thumbnailImageProcessor, IMangaBinderConfig config)
	{
		this.thumbnailImageProcessor = thumbnailImageProcessor;
		this.config = config;
	}

	/// <summary>
	/// Clipboard から BitmapSource を取得します。
	/// 内部用の補助メソッドです。PickFromClipboardAsync から使用されます。
	/// </summary>
	/// <returns>Clipboard に画像が存在する場合は BitmapSource、存在しない場合は null。</returns>
	private BitmapSource? GetFromClipboard()
	{
		try
		{
			return Clipboard.GetImage() as BitmapSource;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// ファイルから BitmapSource を読み込みます。
	/// ファイルロックは保持しません。
	/// </summary>
	/// <param name="fileName">読み込むファイルのパス。</param>
	/// <returns>ファイルが存在する場合は BitmapSource、存在しない場合は null。</returns>
	public BitmapSource? LoadFromFile(string fileName)
	{
		if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
		{
			return null;
		}

		try
		{
			var bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.UriSource = new Uri(fileName, UriKind.Absolute);
			bitmap.CacheOption = BitmapCacheOption.OnLoad; // ファイルロック解放
			bitmap.EndInit();
			bitmap.Freeze();

			return bitmap;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// BitmapSource を PNG 形式の byte[] に変換します。
	/// 内部用の補助メソッドです。BitmapSourceToStream から使用されます。
	/// </summary>
	/// <param name="bitmap">変換する BitmapSource。null の場合は null を返す。</param>
	/// <returns>PNG 形式の byte[]、または null。</returns>
	private byte[]? ToBytes(BitmapSource? bitmap)
	{
		if (bitmap is null)
		{
			return null;
		}

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));

		using var stream = new MemoryStream();
		encoder.Save(stream);
		return stream.ToArray();
	}

	/// <summary>
	/// BitmapSource を PNG ストリームに変換します。
	/// IThumbnailImageProcessor への入力に使用します。
	/// </summary>
	/// <param name="bitmap">変換する BitmapSource。</param>
	/// <returns>PNG エンコード済みストリーム。</returns>
	private Stream BitmapSourceToStream(BitmapSource bitmap)
	{
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));

		var stream = new MemoryStream();
		encoder.Save(stream);
		stream.Seek(0, SeekOrigin.Begin);
		return stream;
	}

	/// <summary>
	/// 入力ストリームをIThumbnailImageProcessorで処理し、JPEG byte[]とPreview用BitmapSourceを生成します。
	/// 参照と貼り付けの共通処理です。
	/// </summary>
	/// <param name="inputStream">入力画像ストリーム。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>JPEG byte[]とPreview用BitmapSource、またはエラー情報。</returns>
	private async ValueTask<(byte[] JpegBytes, BitmapSource PreviewBitmap)> ProcessThumbnailCoreAsync(
		Stream inputStream,
		CancellationToken cancellationToken)
	{
		// IMangaBinderConfig から ThumbnailOptions を取得
		var thumbnailOptions = this.config.ThumbnailOptions;

		// IThumbnailImageProcessor で JPEG サムネイルを生成
		using var thumbnailStream = await this.thumbnailImageProcessor.ProcessThumbnailAsync(
			inputStream,
			thumbnailOptions,
			cancellationToken);

		// ストリームから byte[] を読み取る
		var jpegBytes = new byte[thumbnailStream.Length];
		thumbnailStream.Seek(0, SeekOrigin.Begin);
		await thumbnailStream.ReadExactlyAsync(jpegBytes, 0, (int)thumbnailStream.Length, cancellationToken);

		// JPEG byte[] からプレビュー用 BitmapSource を生成
		var previewBitmap = new BitmapImage();
		previewBitmap.BeginInit();
		previewBitmap.StreamSource = new MemoryStream(jpegBytes);
		previewBitmap.CacheOption = BitmapCacheOption.OnLoad;
		previewBitmap.EndInit();
		previewBitmap.Freeze();

		return (jpegBytes, previewBitmap);
	}

	/// <summary>
	/// クリップボードから画像を取得し、サムネイルを生成します。
	/// キャンセルされた場合や読み込み失敗時も例外をスローしません。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>クリップボード画像処理結果。</returns>
	public async ValueTask<PickFileResult> PickFromClipboardAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			// クリップボードから BitmapSource を取得
			var bitmapSource = this.GetFromClipboard();
			if (bitmapSource == null)
			{
				System.Diagnostics.Debug.WriteLine("[ThumbnailPicker.PickFromClipboardAsync] クリップボードに画像がありません。");
				return new PickFileResult
				{
					Success = false,
					IsCanceled = true,
					PreviewImage = null,
					ThumbnailBytes = null,
					ErrorMessage = null
				};
			}

			// BitmapSource をストリームに変換
			using var inputStream = this.BitmapSourceToStream(bitmapSource);

			// 共通のサムネイル処理を実行
			var (jpegBytes, previewBitmap) = await this.ProcessThumbnailCoreAsync(inputStream, cancellationToken);

			return new PickFileResult
			{
				Success = true,
				IsCanceled = false,
				PreviewImage = previewBitmap,
				ThumbnailBytes = jpegBytes,
				ErrorMessage = null
			};
		}
		catch (OperationCanceledException)
		{
			return new PickFileResult
			{
				Success = false,
				IsCanceled = true,
				PreviewImage = null,
				ThumbnailBytes = null,
				ErrorMessage = null
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[ThumbnailPicker.PickFromClipboardAsync] Exception: {ex.Message}");
			return new PickFileResult
			{
				Success = false,
				IsCanceled = false,
				PreviewImage = null,
				ThumbnailBytes = null,
				ErrorMessage = $"画像の処理に失敗しました: {ex.Message}"
			};
		}
	}

	/// <summary>
	/// OpenFileDialog を表示して、ファイルから画像を選択します。
	/// キャンセルされた場合や読み込み失敗時も例外をスローしません。
	/// </summary>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>ファイル選択結果。</returns>
	public async ValueTask<PickFileResult> PickFromFileAsync(CancellationToken cancellationToken = default)
	{
		var dialog = new OpenFileDialog
		{
			Filter = SupportedExtensionHelper.ImageOpenFileDialogFilter,
			Title = "サムネイル画像を選択",
			CheckFileExists = true,
			CheckPathExists = true,
		};

		// OpenFileDialog は同期的に動作するため、ValueTask で即座に結果を返す
		var result = dialog.ShowDialog() ?? false;

		if (!result)
		{
			// キャンセルされた場合
			return new PickFileResult
			{
				Success = false,
				IsCanceled = true,
				PreviewImage = null,
				ThumbnailBytes = null,
				ErrorMessage = null
			};
		}

		try
		{
			var filePath = dialog.FileName;

			// ファイルが実際に存在することを確認
			if (!File.Exists(filePath))
			{
				return new PickFileResult
				{
					Success = false,
					IsCanceled = false,
					PreviewImage = null,
					ThumbnailBytes = null,
					ErrorMessage = "ファイルが見つかりません。"
				};
			}

			// ファイルを開いてサムネイルを生成
			using var fileStream = new FileStream(
				filePath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read);

			// 共通のサムネイル処理を実行
			var (jpegBytes, previewBitmap) = await this.ProcessThumbnailCoreAsync(fileStream, cancellationToken);

			return new PickFileResult
			{
				Success = true,
				IsCanceled = false,
				PreviewImage = previewBitmap,
				ThumbnailBytes = jpegBytes,
				ErrorMessage = null
			};
		}
		catch (OperationCanceledException)
		{
			return new PickFileResult
			{
				Success = false,
				IsCanceled = true,
				PreviewImage = null,
				ThumbnailBytes = null,
				ErrorMessage = null
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[ThumbnailPicker.PickFromFileAsync] Exception: {ex.Message}");
			return new PickFileResult
			{
				Success = false,
				IsCanceled = false,
				PreviewImage = null,
				ThumbnailBytes = null,
				ErrorMessage = $"画像の読み込みに失敗しました: {ex.Message}"
			};
		}
	}
}

/// <summary>
/// ファイル選択結果を表します。
/// </summary>
public class PickFileResult
{
	/// <summary>処理が成功したかどうか。</summary>
	public bool Success { get; init; }

	/// <summary>ユーザーがキャンセルしたかどうか。</summary>
	public bool IsCanceled { get; init; }

	/// <summary>プレビュー表示用の BitmapSource。失敗またはキャンセル時は null。</summary>
	public BitmapSource? PreviewImage { get; init; }

	/// <summary>保存用の JPEG byte[]。失敗またはキャンセル時は null。</summary>
	public byte[]? ThumbnailBytes { get; init; }

	/// <summary>エラーメッセージ。成功時またはキャンセル時は null。</summary>
	public string? ErrorMessage { get; init; }
}
