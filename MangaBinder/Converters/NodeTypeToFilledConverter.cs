using System.Globalization;
using System.Windows.Data;
using MangaBinder.Bindings;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MaterialVolumeNode"/> または <see cref="MaterialItemType"/> に基づいて、ui:SymbolIcon の Filled 属性値を決定するコンバーターです。
/// 実フォルダと圧縮ファイル内フォルダを見分けやすくするために使用されます。
/// </summary>
[ValueConversion(typeof(object), typeof(bool))]
public class NodeTypeToFilledConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		// MaterialVolumeNode の場合
		if (value is MaterialVolumeNode node)
		{
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

		// MaterialItemType を直接受け取った場合
		if (value is MaterialItemType itemType)
		{
			return itemType switch
			{
				// Root / Archive / Epub は常に Filled = true
				MaterialItemType.Root => true,
				MaterialItemType.Archive => true,
				MaterialItemType.Epub => true,

				// Folder はデータソースから判別できないため、Filled = true として表示
				MaterialItemType.Folder => true,

				_ => false,
			};
		}

		return false;
	}

	/// <inheritdoc/>
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
