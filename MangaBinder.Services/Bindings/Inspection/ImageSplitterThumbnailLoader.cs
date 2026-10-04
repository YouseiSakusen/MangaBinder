using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MangaBinder.Settings;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 見開き分割一覧用のサムネイル画像を読み込むクラスです。
/// </summary>
public sealed class ImageSplitterThumbnailLoader
{
	private readonly IMangaBinderConfig config;
	private readonly BindingThumbnailImageProcessor imageProcessor;

	/// <summary>
	/// <see cref="ImageSplitterThumbnailLoader"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="config">アプリケーション設定。</param>
	/// <param name="imageProcessor">サムネイル画像生成処理。</param>
	public ImageSplitterThumbnailLoader(
		IMangaBinderConfig config,
		BindingThumbnailImageProcessor imageProcessor)
	{
		this.config = config ?? throw new ArgumentNullException(nameof(config));
		this.imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
	}

	/// <summary>
	/// 指定した画像の元ファイルから一覧表示用サムネイルを非同期に生成します。
	/// </summary>
	/// <param name="image">対象の画像。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>Freeze 済みの <see cref="ImageSource"/>。</returns>
	public Task<ImageSource> LoadAsync(BindingImage image, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(image);

		var filePath = image.FilePath
			?? throw new InvalidOperationException($"BindingImage.FilePath が null です。（{image.FileName}）");

		return Task.Run<ImageSource>(() =>
		{
			var bytes = this.imageProcessor.GenerateThumbnail(
				filePath,
				this.config.ThumbnailOptions.Width,
				this.config.ThumbnailOptions.Height,
				cancellationToken);

			var bitmap = new BitmapImage();
			using (var stream = new MemoryStream(bytes))
			{
				bitmap.BeginInit();
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.StreamSource = stream;
				bitmap.EndInit();
			}
			bitmap.Freeze();
			return bitmap;
		}, cancellationToken);
	}
}
