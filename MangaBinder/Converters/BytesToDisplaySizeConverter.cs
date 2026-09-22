using System.Globalization;
using System.Windows.Data;
using MangaBinder.Helpers;

namespace MangaBinder.Converters;

/// <summary>
/// バイトサイズ値を、表示用の MB/GB 文字列に変換する <see cref="IValueConverter"/> です。
/// </summary>
public class BytesToDisplaySizeConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not long bytes)
			return "0 MB";

		if (bytes == 0)
			return "0 MB";

		// サイズを 1024 ベースでフォーマット
		if (bytes >= 1024L * 1024 * 1024)
		{
			// 1GB以上はGB表示（小数1桁）
			var sizeGB = bytes / (1024.0 * 1024 * 1024);
			return $"{sizeGB:F1} GB";
		}
		else
		{
			// 1GB未満はMB表示（小数なし）
			var sizeMB = bytes / (1024.0 * 1024);
			return $"{(long)sizeMB} MB";
		}
	}

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
