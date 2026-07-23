using System.Globalization;
using System.Windows.Data;
using MangaBinder.Controls;
using MangaBinder.Series;

namespace MangaBinder.Converters;

/// <summary>
/// SeriesDeleteMethod の enum 値を bool に変換し、ラジオボタンの IsChecked にバインドするコンバーターです。
/// </summary>
public class SeriesDeleteMethodConverter : IValueConverter
{
	/// <summary>
	/// SeriesDeleteMethod を bool に変換します。
	/// </summary>
	/// <param name="value">SeriesDeleteMethod の値。</param>
	/// <param name="targetType">ターゲットの型。</param>
	/// <param name="parameter">比較対象の SeriesDeleteMethod （"InfoOnly" または "InfoAndFolder"）。</param>
	/// <param name="culture">使用するカルチャ。</param>
	/// <returns>value が parameter と一致する場合は true、それ以外は false。</returns>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not SeriesDeleteMethod method || parameter is not string methodStr)
		{
			return false;
		}

		if (Enum.TryParse<SeriesDeleteMethod>(methodStr, out var paramMethod))
		{
			return method == paramMethod;
		}

		return false;
	}

	/// <summary>
	/// bool を SeriesDeleteMethod に逆変換します。
	/// </summary>
	/// <param name="value">bool 値。</param>
	/// <param name="targetType">ターゲットの型。</param>
	/// <param name="parameter">対応する SeriesDeleteMethod （"InfoOnly" または "InfoAndFolder"）。</param>
	/// <param name="culture">使用するカルチャ。</param>
	/// <returns>value が true の場合は parameter に対応する SeriesDeleteMethod、false の場合は SeriesDeleteMethod.InfoOnly。</returns>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not bool isChecked || !isChecked || parameter is not string methodStr)
		{
			return SeriesDeleteMethod.InfoOnly;
		}

		if (Enum.TryParse<SeriesDeleteMethod>(methodStr, out var method))
		{
			return method;
		}

		return SeriesDeleteMethod.InfoOnly;
	}
}

/// <summary>
/// bool を Visibility に変換するコンバーターです。
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
	/// <summary>
	/// bool を Visibility に変換します。true は Visible、false は Collapsed。
	/// parameter に "Invert" を指定すると逆になります。
	/// </summary>
	/// <param name="value">bool 値。</param>
	/// <param name="targetType">ターゲットの型。</param>
	/// <param name="parameter">"Invert" を指定すると反転します。</param>
	/// <param name="culture">使用するカルチャ。</param>
	/// <returns>true の場合は Visible、false の場合は Collapsed（Invert 指定時は反転）。</returns>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var boolValue = value is bool b && b;
		var isInvert = parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase);

		if (isInvert)
		{
			boolValue = !boolValue;
		}

		return boolValue
			? System.Windows.Visibility.Visible
			: System.Windows.Visibility.Collapsed;
	}

	/// <summary>
	/// Visibility を bool に逆変換します。
	/// </summary>
	/// <param name="value">Visibility 値。</param>
	/// <param name="targetType">ターゲットの型。</param>
	/// <param name="parameter">使用されません。</param>
	/// <param name="culture">使用するカルチャ。</param>
	/// <returns>Visible の場合は true、それ以外は false。</returns>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is System.Windows.Visibility visibility && visibility == System.Windows.Visibility.Visible;
	}
}
