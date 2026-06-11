using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MangaBinder.Bindings;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MaterialItemType"/> をアイコン色（ブラシ）に変換するコンバーターです。
/// 実フォルダ / EPUB は主役として濃い色、圧縮ファイル内フォルダは見やすい色、
/// 圧縮ファイル本体は控えめな色で表現します。
/// </summary>
[ValueConversion(typeof(MaterialItemType), typeof(Brush))]
public class NodeTypeToColorConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not MaterialItemType nodeType)
			return Brushes.Gray;

		return nodeType switch
		{
			// 実フォルダ：主役、濃い黄土色
			MaterialItemType.Root => new SolidColorBrush(Color.FromRgb(0xC5, 0x8A, 0x00)), // #C58A00
			// 圧縮ファイル内フォルダ：選択対象、黄土色系
			MaterialItemType.Folder => new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B)), // #B8860B

			// 圧縮ファイル本体：コンテナ、控えめな青灰色
			MaterialItemType.Archive => new SolidColorBrush(Color.FromRgb(0x6E, 0x87, 0xA8)), // #6E87A8
			// EPUB：主役、緑系
			MaterialItemType.Epub => new SolidColorBrush(Color.FromRgb(0x2E, 0x9F, 0x55)), // #2E9F55

			_ => Brushes.Gray,
		};
	}

	/// <inheritdoc/>
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
