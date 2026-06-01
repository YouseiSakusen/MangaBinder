using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="HomeEmptyStateKind"/> 値が ConverterParameter に一致する場合に <see cref="Visibility.Visible"/> を返すコンバーターです。
/// </summary>
[ValueConversion(typeof(HomeEmptyStateKind), typeof(Visibility))]
public class HomeEmptyStateKindToVisibilityConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not HomeEmptyStateKind kind)
			return Visibility.Collapsed;

		if (parameter is string s && Enum.TryParse<HomeEmptyStateKind>(s, out var target))
			return kind == target ? Visibility.Visible : Visibility.Collapsed;

		return Visibility.Collapsed;
	}

	/// <inheritdoc/>
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
