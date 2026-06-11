using System.Globalization;
using System.Windows.Data;
using MangaBinder.Bindings;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MaterialVolumeNode"/> に基づいて、ui:SymbolIcon の Filled 属性値を決定するマルチコンバーターです。
/// 実フォルダと圧縮ファイル内フォルダを見分けやすくするために使用されます。
/// </summary>
[ValueConversion(typeof(MaterialVolumeNode), typeof(bool))]
public class NodeTypeToFilledConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not MaterialVolumeNode node)
			return false;

		return node.NodeType switch
		{
			// Root は常に Filled = true（実フォルダルート）
			MaterialItemType.Root => true,

			// Folder の場合、ArchiveEntryPrefix で判別
			// null = 実フォルダ → Filled = true
			// not null = 圧縮ファイル内フォルダ → Filled = false
			MaterialItemType.Folder => node.ArchiveEntryPrefix == null,

			// Archive / Epub は Filled = true（実体を持つもの）
			MaterialItemType.Archive => true,
			MaterialItemType.Epub => true,

			_ => false,
		};
	}

	/// <inheritdoc/>
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
