using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MangaBinder.Binding;

/// <summary>
/// <see cref="MangaSeries"/> の完結状態・所持状況をバッジ背景色 <see cref="Brush"/> に変換する <see cref="IValueConverter"/> です。
/// </summary>
public class SeriesStatusBadgeBrushConverter : IValueConverter
{
    /// <summary>完結済み・全巻所持済みの場合の背景色（金系）です。</summary>
    private static readonly SolidColorBrush CompletedAndOwnedBrush = makeFrozen("#C89B3C");

    /// <summary>連載中の場合の背景色（青系）です。</summary>
    private static readonly SolidColorBrush OngoingBrush = makeFrozen("#3B82C4");

    /// <summary>完結済み・未所持ありの場合の背景色（薄いグレー）です。</summary>
    private static readonly SolidColorBrush CompletedNotOwnedBrush = makeFrozen("#909090", 0.6);

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not MangaSeries series)
            return CompletedNotOwnedBrush;

        if (!series.SeriesCompleted)
            return OngoingBrush;

        return series.IsOwnedCompleted
            ? CompletedAndOwnedBrush
            : CompletedNotOwnedBrush;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;

	/// <summary>
	/// 16進数カラーコードから Freeze 済みの <see cref="SolidColorBrush"/> を生成します。
	/// </summary>
	/// <param name="hex">16進数カラーコード（例: "#C89B3C"）。</param>
	/// <returns>Freeze 済みの <see cref="SolidColorBrush"/>。</returns>
	private static SolidColorBrush makeFrozen(string hex, double opacity = 1.0)
	{
		var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		brush.Opacity = opacity;
		brush.Freeze();
		return brush;
	}
}
