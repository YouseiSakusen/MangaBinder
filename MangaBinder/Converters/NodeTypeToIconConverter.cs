using System.Globalization;
using System.Windows.Data;
using MangaBinder.Bindings;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MaterialItemType"/> を表示用アイコン文字列に変換するコンバーターです。
/// </summary>
[ValueConversion(typeof(MaterialItemType), typeof(string))]
public class NodeTypeToIconConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is MaterialItemType nodeType
            ? nodeType switch
            {
                MaterialItemType.Root => "📁",
                MaterialItemType.Folder => "📂",
                MaterialItemType.Archive => "📦",
                MaterialItemType.Epub => "📖",
                _ => "📄",
            }
            : "📄";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
