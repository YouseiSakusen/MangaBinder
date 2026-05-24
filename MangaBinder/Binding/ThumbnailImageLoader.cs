using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MangaBinder.Bindings;
using MangaBinder.Settings;

namespace MangaBinder.Binding;

/// <summary>
/// <see cref="MangaSeries"/> のサムネイル画像を <see cref="ImageSource"/> として読み込むクラスです。
/// </summary>
public class ThumbnailImageLoader
{
    /// <summary>アプリケーション設定。</summary>
    private readonly IMangaBinderConfig config;

    /// <summary>
    /// <see cref="ThumbnailImageLoader"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="config">アプリケーション設定。</param>
    public ThumbnailImageLoader(IMangaBinderConfig config)
    {
        this.config = config;
    }

    /// <summary>
    /// <see cref="MangaSeries"/> のサムネイルを <see cref="ImageSource"/> として読み込みます。
    /// </summary>
    /// <param name="series">対象の <see cref="MangaSeries"/>。</param>
    /// <returns>読み込んだ <see cref="ImageSource"/>。ファイルが存在しない場合は <see langword="null"/>。</returns>
    public ImageSource? Load(MangaSeries series)
    {
        var fileName = series.ThumbnailStatus switch
        {
            ThumbnailStatus.Completed when !string.IsNullOrEmpty(series.ThumbnailFileName)
                => series.ThumbnailFileName,
            ThumbnailStatus.LimitExceeded  => "00000!_limit-exceeded.jpg",
            ThumbnailStatus.Failed         => "00000!_failed.jpg",
            ThumbnailStatus.ArchiveInArchive => "00000!_nested-archive.jpg",
            _                              => "00000!_none.jpg",
        };

        var fullPath = this.config.GetThumbnailFullPath(fileName);
        if (!File.Exists(fullPath))
            return null;

        var bitmap = new BitmapImage();
        using (var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 160;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }
        bitmap.Freeze();

        return bitmap;
    }
}
