using System.Globalization;
using System.Windows.Data;
using MangaBinder.Binding;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MaterialVolumeNodeType"/> を表示用アイコン文字列に変換するコンバーターです。
/// </summary>
[ValueConversion(typeof(MaterialVolumeNodeType), typeof(string))]
public class NodeTypeToIconConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is MaterialVolumeNodeType nodeType
            ? nodeType switch
            {
                MaterialVolumeNodeType.Root => "📁",
                MaterialVolumeNodeType.Folder => "📂",
                MaterialVolumeNodeType.Archive => "📦",
                MaterialVolumeNodeType.Epub => "📖",
                _ => "📄",
            }
            : "📄";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
