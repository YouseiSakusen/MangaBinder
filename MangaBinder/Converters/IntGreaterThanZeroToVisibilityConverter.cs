using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MangaBinder.Converters;

/// <summary>
/// int 値が 0 より大きい場合に <see cref="Visibility.Visible"/> を返すコンバーターです。
/// </summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is int intValue)
		{
			return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
		}

		return Visibility.Collapsed;
	}

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
