using System.IO;
using System.Windows.Media.Imaging;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 見開き分割プレビュー用のフルサイズ画像を読み込むクラスです。
/// </summary>
public sealed class ImageSplitterPreviewImageLoader
{
	/// <summary>
	/// 指定した画像の Work 側ファイルからフルサイズの画像を非同期に読み込みます。
	/// </summary>
	/// <param name="image">対象の画像。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>Freeze 済みの <see cref="BitmapSource"/>。</returns>
	public Task<BitmapSource> LoadAsync(BindingImage image, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(image);

		var filePath = image.FilePath
			?? throw new InvalidOperationException($"BindingImage.FilePath が null です。（{image.FileName}）");

		return Task.Run<BitmapSource>(() =>
		{
			cancellationToken.ThrowIfCancellationRequested();

			var bitmap = new BitmapImage();
			using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
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
