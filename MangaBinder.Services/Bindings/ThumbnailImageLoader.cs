using System.Windows.Media;
using System.Windows.Media.Imaging;
using MangaBinder.Settings;

namespace MangaBinder.Bindings;

/// <summary>
/// <see cref="MangaSeries"/> のサムネイル画像を <see cref="ImageSource"/> として読み込むクラスです。
/// </summary>
public class ThumbnailImageLoader
{
    /// <summary>アプリケーション設定。</summary>
    private readonly IMangaBinderConfig config;

    /// <summary>アプリケーション設定（WorkThumbnail 用）。</summary>
    private readonly AppSettings appSettings;

    /// <summary>
    /// <see cref="ThumbnailImageLoader"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="config">アプリケーション設定。</param>
    public ThumbnailImageLoader(IMangaBinderConfig config)
    {
        this.config = config;
        this.appSettings = (AppSettings)config;
    }

    /// <summary>
    /// <see cref="MangaSeries"/> のサムネイルを <see cref="ImageSource"/> として読み込みます。
    /// 登録待ち作品の場合は WorkThumbnail フォルダから読み込み、
    /// 正式作品の場合は通常のサムネイルフォルダから読み込みます。
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

        // 登録待ち作品と正式作品でサムネイルフォルダを読み分ける
        var fullPath = series.IsWork
            ? this.appSettings.GetWorkThumbnailFullPath(fileName)
            : this.config.GetThumbnailFullPath(fileName);

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

