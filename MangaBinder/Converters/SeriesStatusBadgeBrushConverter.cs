using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MangaBinder.Converters;

/// <summary>
/// 完結状態・所持状況をバッジ背景色 <see cref="Brush"/> に変換する <see cref="IMultiValueConverter"/> です。
/// </summary>
public class SeriesStatusBadgeBrushConverter : IMultiValueConverter
{
	/// <summary>完結済み・全巻所持済みの場合の背景色（金系）です。</summary>
	private static readonly SolidColorBrush CompletedAndOwnedBrush = makeFrozen("#C89B3C");

	/// <summary>連載中の場合の背景色（青系）です。</summary>
	private static readonly SolidColorBrush OngoingBrush = makeFrozen("#3B82C4");

	/// <summary>完結済み・未所持ありの場合の背景色（薄いグレー）です。</summary>
	private static readonly SolidColorBrush CompletedNotOwnedBrush = makeFrozen("#909090", 0.6);

	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		// values[0]: SeriesCompleted (bool)
		// values[1]: IsOwnedCompleted (bool)
		if (values == null || values.Length < 2)
			return CompletedNotOwnedBrush;

		var seriesCompleted = (bool)values[0];
		var isOwnedCompleted = (bool)values[1];

		if (!seriesCompleted)
			return OngoingBrush;

		return isOwnedCompleted
			? CompletedAndOwnedBrush
			: CompletedNotOwnedBrush;
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();

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
