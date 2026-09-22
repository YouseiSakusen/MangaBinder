using System.Globalization;
using System.Windows.Data;
using MangaBinder.Helpers;

namespace MangaBinder.Converters;

/// <summary>
/// 圧縮ファイルの数とサイズバイト値を、表示文字列に変換する <see cref="IMultiValueConverter"/> です。
/// </summary>
public class ArchiveMaterialSummaryConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		// values[0]: MaterialArchiveCount (int)
		// values[1]: MaterialArchiveTotalBytes (long)
		if (values == null || values.Length < 2)
			return "圧縮ファイル：0";

		if (values[0] is not int archiveCount)
			return "圧縮ファイル：0";

		if (values[1] is not long totalBytes)
			return $"圧縮ファイル：{archiveCount}";

		// 容量が0より大きい場合はサイズを含める
		if (totalBytes > 0)
		{
			var sizeText = StorageSizeHelper.FormatSize(totalBytes);
			return $"圧縮ファイル：{archiveCount}（{sizeText}）";
		}

		return $"圧縮ファイル：{archiveCount}";
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
