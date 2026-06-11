using System.Globalization;
using System.Windows.Data;
using MangaBinder.Bindings;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MangaSeries"/> をサムネイル <see cref="System.Windows.Media.ImageSource"/> に変換する <see cref="IValueConverter"/> です。
/// </summary>
public class ThumbnailImageConverter : IValueConverter
{
    /// <summary>サムネイル画像ローダー。</summary>
    private readonly ThumbnailImageLoader loader;

    /// <summary>
    /// <see cref="ThumbnailImageConverter"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="loader">サムネイル画像ローダー。</param>
    public ThumbnailImageConverter(ThumbnailImageLoader loader)
    {
        this.loader = loader;
    }

    /// <inheritdoc/>
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MangaSeries series)
            return this.loader.Load(series);

        return null;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
