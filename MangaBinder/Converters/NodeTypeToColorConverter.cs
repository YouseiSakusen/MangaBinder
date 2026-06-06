using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MangaBinder.Binding;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MaterialVolumeNodeType"/> をアイコン色（ブラシ）に変換するコンバーターです。
/// フォルダ系は黄/橙系、圧縮ファイル系は青/紫系で色分けされます。
/// </summary>
[ValueConversion(typeof(MaterialVolumeNodeType), typeof(Brush))]
public class NodeTypeToColorConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not MaterialVolumeNodeType nodeType)
			return Brushes.Gray;

		return nodeType switch
		{
			// フォルダ系：黄/橙系（ライトテーマに馴染む控えめな色）
			MaterialVolumeNodeType.Root => new SolidColorBrush(Color.FromRgb(0xD4, 0xA5, 0x2A)), // 黄土色
			MaterialVolumeNodeType.Folder => new SolidColorBrush(Color.FromRgb(0xE8, 0xB8, 0x3D)), // 明るい黄色

			// 圧縮ファイル系：青/紫系（ライトテーマに馴染む控えめな色）
			MaterialVolumeNodeType.Archive => new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0xBF)), // スチールブルー
			MaterialVolumeNodeType.Epub => new SolidColorBrush(Color.FromRgb(0x5B, 0x7C, 0xB8)), // 紫青

			_ => Brushes.Gray,
		};
	}

	/// <inheritdoc/>
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
