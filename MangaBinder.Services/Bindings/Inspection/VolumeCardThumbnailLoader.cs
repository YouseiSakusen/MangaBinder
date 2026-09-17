using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MangaBinder.Settings;
using Microsoft.Extensions.Logging;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// SeriesInspection の巻カード用代表サムネイル画像を読み込むクラスです。
/// BindingVolume の代表画像（Images[0]）をUIスレッドをブロックせずに読み込み、
/// ImageSource として返します。
/// </summary>
public sealed class VolumeCardThumbnailLoader
{
	private readonly IMangaBinderConfig config;
	private readonly VolumeCardThumbnailImageProcessor imageProcessor;
	private readonly ILogger<VolumeCardThumbnailLoader> logger;

	/// <summary>
	/// <see cref="VolumeCardThumbnailLoader"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="config">アプリケーション設定。</param>
	/// <param name="imageProcessor">サムネイル画像生成処理。</param>
	/// <param name="logger">ログ出力。</param>
	public VolumeCardThumbnailLoader(
		IMangaBinderConfig config,
		VolumeCardThumbnailImageProcessor imageProcessor,
		ILogger<VolumeCardThumbnailLoader> logger)
	{
		this.config = config ?? throw new ArgumentNullException(nameof(config));
		this.imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
		this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// BindingVolume の代表画像をサムネイル用にリサイズし、<see cref="ImageSource"/> として非同期に読み込みます。
	/// NetVips の重い処理はバックグラウンドスレッドで実行されます。
	/// </summary>
	/// <param name="volume">読み込み対象の巻。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>
	/// 読み込んだ <see cref="ImageSource"/>。
	/// 代表画像が使用できない場合は No Image（00000!_none.jpg）を返します。
	/// No Image ファイルも存在しない場合は <see langword="null"/>。
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="volume"/> が null の場合。</exception>
	public ValueTask<ImageSource?> LoadAsync(
		BindingVolume volume,
		CancellationToken cancellationToken = default)
	{
		if (volume == null)
			throw new ArgumentNullException(nameof(volume));

		// 非同期処理を実行
		return new ValueTask<ImageSource?>(this.LoadCoreAsync(volume, cancellationToken));
	}

	/// <summary>
	/// 実際の読み込み処理を行う内部メソッドです。
	/// </summary>
	private async Task<ImageSource?> LoadCoreAsync(BindingVolume volume, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// 代表画像を決定
		if (volume.Images.Count == 0)
		{
			this.logger.LogDebug("巻 {volumeNumber} の画像数が0のため、No Image を返します。", volume.VolumeNumber.Value);
			return await this.LoadNoImageAsync(cancellationToken);
		}

		var firstImage = volume.Images[0];

		// FilePath が null または空白の場合
		if (string.IsNullOrWhiteSpace(firstImage.FilePath))
		{
			this.logger.LogDebug("巻 {volumeNumber} の代表画像（{fileName}）のパスが null または空白のため、No Image を返します。",
				volume.VolumeNumber.Value, firstImage.FileName);
			return await this.LoadNoImageAsync(cancellationToken);
		}

		// ファイルが実在しない場合
		if (!File.Exists(firstImage.FilePath))
		{
			this.logger.LogDebug("巻 {volumeNumber} の代表画像ファイル \"{filePath}\" が存在しないため、No Image を返します。",
				volume.VolumeNumber.Value, firstImage.FilePath);
			return await this.LoadNoImageAsync(cancellationToken);
		}

		// ここまで到達した場合、代表画像ファイルは存在する
		// Processor をバックグラウンドで実行
		try
		{
			var thumbnailBytes = await Task.Run(
				() => this.imageProcessor.GenerateThumbnail(
					firstImage.FilePath,
					this.config.ThumbnailOptions.Width,
					this.config.ThumbnailOptions.Height,
					cancellationToken),
				cancellationToken);

			cancellationToken.ThrowIfCancellationRequested();

			// PNG バイト列を BitmapImage に変換
			return this.ConvertBytesToImageSource(thumbnailBytes, firstImage.FilePath);
		}
		catch (OperationCanceledException)
		{
			// キャンセルはそのまま伝播
			throw;
		}
		catch (NetVips.VipsException ex)
		{
			this.logger.LogError(ex, "巻 {volumeNumber} の代表画像 \"{filePath}\" の処理に失敗しました（VipsException）。No Image を返します。",
				volume.VolumeNumber.Value, firstImage.FilePath);
			return await this.LoadNoImageAsync(cancellationToken);
		}
		catch (IOException ex)
		{
			this.logger.LogError(ex, "巻 {volumeNumber} の代表画像 \"{filePath}\" の処理に失敗しました（IOException）。No Image を返します。",
				volume.VolumeNumber.Value, firstImage.FilePath);
			return await this.LoadNoImageAsync(cancellationToken);
		}
		catch (UnauthorizedAccessException ex)
		{
			this.logger.LogError(ex, "巻 {volumeNumber} の代表画像 \"{filePath}\" の処理に失敗しました（UnauthorizedAccessException）。No Image を返します。",
				volume.VolumeNumber.Value, firstImage.FilePath);
			return await this.LoadNoImageAsync(cancellationToken);
		}
	}

	/// <summary>
	/// PNG バイト列を BitmapImage に変換します。
	/// </summary>
	private ImageSource? ConvertBytesToImageSource(byte[] pngBytes, string sourceFilePath)
	{
		try
		{
			var bitmap = new BitmapImage();
			using (var stream = new MemoryStream(pngBytes, writable: false))
			{
				bitmap.BeginInit();
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.StreamSource = stream;
				bitmap.EndInit();
			}
			bitmap.Freeze();
			return bitmap;
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "PNG バイト列から BitmapImage への変換に失敗しました（ファイル: {filePath}）。",
				sourceFilePath);
			return null;
		}
	}

	/// <summary>
	/// No Image（00000!_none.jpg）を読み込みます。
	/// </summary>
	private async Task<ImageSource?> LoadNoImageAsync(CancellationToken cancellationToken)
	{
		await Task.Yield(); // 非同期コンテキストを保持

		var noImagePath = this.config.GetThumbnailFullPath("00000!_none.jpg");

		if (!File.Exists(noImagePath))
		{
			this.logger.LogWarning("No Image ファイル \"{noImagePath}\" が存在しません。", noImagePath);
			return null;
		}

		try
		{
			var bitmap = new BitmapImage();
			using (var stream = File.Open(noImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				bitmap.BeginInit();
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.StreamSource = stream;
				bitmap.EndInit();
			}
			bitmap.Freeze();
			return bitmap;
		}
		catch (Exception ex)
		{
			this.logger.LogError(ex, "No Image ファイル \"{noImagePath}\" の読み込みに失敗しました。", noImagePath);
			return null;
		}
	}
}
