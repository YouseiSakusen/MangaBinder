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
	private readonly AppSettings appSettings;

	/// <summary>
	/// ThumbnailPicker を初期化します。
	/// </summary>
	/// <param name="thumbnailImageProcessor">サムネイル生成プロセッサ。</param>
	/// <param name="appSettings">アプリケーション設定。</param>
	public ThumbnailPicker(IThumbnailImageProcessor thumbnailImageProcessor, AppSettings appSettings)
	{
		this.thumbnailImageProcessor = thumbnailImageProcessor;
		this.appSettings = appSettings;
	}

	/// <summary>
	/// Clipboard から BitmapSource を取得します。
	/// </summary>
	/// <returns>Clipboard に画像が存在する場合は BitmapSource、存在しない場合は null。</returns>
	public BitmapSource? GetFromClipboard()
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
	/// </summary>
	/// <param name="bitmap">変換する BitmapSource。null の場合は null を返す。</param>
	/// <returns>PNG 形式の byte[]、または null。</returns>
	public byte[]? ToBytes(BitmapSource? bitmap)
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

			var thumbnailOptions = new ThumbnailOptions
			{
				Width = this.appSettings.ThumbnailWidth.Value,
				Height = this.appSettings.ThumbnailHeight.Value,
				JpegQuality = 90,
				BackgroundColor = "#FFFFFF"
			};

			// ThumbnailImageProcessor で JPEG サムネイルを生成
			using var thumbnailStream = await this.thumbnailImageProcessor.ProcessThumbnailAsync(
				fileStream,
				thumbnailOptions,
				cancellationToken);

			// ストリームから byte[] を読み取る
			var thumbnailBytes = new byte[thumbnailStream.Length];
			thumbnailStream.Seek(0, SeekOrigin.Begin);
			await thumbnailStream.ReadExactlyAsync(thumbnailBytes, 0, (int)thumbnailStream.Length, cancellationToken);

			// JPEG byte[] からプレビュー用 BitmapSource を生成
			var previewBitmap = new BitmapImage();
			previewBitmap.BeginInit();
			previewBitmap.StreamSource = new MemoryStream(thumbnailBytes);
			previewBitmap.CacheOption = BitmapCacheOption.OnLoad;
			previewBitmap.EndInit();
			previewBitmap.Freeze();

			return new PickFileResult
			{
				Success = true,
				IsCanceled = false,
				PreviewImage = previewBitmap,
				ThumbnailBytes = thumbnailBytes,
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
