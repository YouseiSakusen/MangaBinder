using System.Globalization;
using System.Windows.Data;
using MangaBinder.Bindings;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MaterialItemType"/> を Wpf.Ui SymbolIcon の Symbol 名に変換するコンバーターです。
/// </summary>
[ValueConversion(typeof(MaterialItemType), typeof(string))]
public class NodeTypeToSymbolConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value is MaterialItemType nodeType
			? nodeType switch
			{
				MaterialItemType.Root => "Folder24",
				MaterialItemType.Folder => "FolderOpen24",
				MaterialItemType.Archive => "Archive24",
				MaterialItemType.Epub => "Book24",
				_ => "Folder24",
			}
			: "Folder24";
	}

	/// <inheritdoc/>
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
