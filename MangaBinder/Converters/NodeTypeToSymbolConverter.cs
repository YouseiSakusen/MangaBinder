using System.Globalization;
using System.Windows.Data;
using MangaBinder.Binding;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MaterialVolumeNodeType"/> を Wpf.Ui SymbolIcon の Symbol 名に変換するコンバーターです。
/// </summary>
[ValueConversion(typeof(MaterialVolumeNodeType), typeof(string))]
public class NodeTypeToSymbolConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value is MaterialVolumeNodeType nodeType
			? nodeType switch
			{
				MaterialVolumeNodeType.Root => "Folder24",
				MaterialVolumeNodeType.Folder => "FolderOpen24",
				MaterialVolumeNodeType.Archive => "Archive24",
				MaterialVolumeNodeType.Epub => "Book24",
				_ => "Folder24",
			}
			: "Folder24";
	}

	/// <inheritdoc/>
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
