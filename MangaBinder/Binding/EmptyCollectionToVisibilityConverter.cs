using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MangaBinder.Binding;

/// <summary>
/// コレクションが空でない場合に <see cref="Visibility.Visible"/> を返すコンバーターです。
/// </summary>
[ValueConversion(typeof(IEnumerable), typeof(Visibility))]
public class EmptyCollectionToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ICollection collection)
            return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (value is IEnumerable enumerable)
            return enumerable.Cast<object>().Any() ? Visibility.Visible : Visibility.Collapsed;

        return Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
