using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MangaBinder.Converters;

/// <summary>
/// 完結状態・所持状況をバッジ背景色 <see cref="Brush"/> に変換する <see cref="IMultiValueConverter"/> です。
/// </summary>
public class SeriesStatusBadgeBrushConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		// values[0]: SeriesCompleted (bool)
		// values[1]: IsOwnedCompleted (bool)
		// values[2]: IsIncomplete (bool)
		if (values == null || values.Length < 2)
			return this.GetBrushFromResource("NotOwnedCompletedBrush");

		// IsIncomplete を優先判定
		if (values.Length >= 3)
		{
			var isIncomplete = (bool)values[2];
			if (isIncomplete)
				return this.GetBrushFromResource("IncompleteBrush");
		}

		var seriesCompleted = (bool)values[0];
		var isOwnedCompleted = (bool)values[1];

		if (!seriesCompleted)
			return this.GetBrushFromResource("InProgressBrush");

		if (isOwnedCompleted)
			return this.GetBrushFromResource("OwnedCompletedBrush");

		return this.GetBrushFromResource("NotOwnedCompletedBrush");
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();

	/// <summary>
	/// Application Resourceから指定されたキーでBrushを取得します。
	/// </summary>
	/// <param name="resourceKey">Brushリソースのキー。</param>
	/// <returns>取得したBrush。リソースが見つからない場合は NotOwnedCompletedBrush をフォールバック。</returns>
	private Brush GetBrushFromResource(string resourceKey)
	{
		var brush = Application.Current.Resources[resourceKey] as Brush;
		return brush ?? (Application.Current.Resources["NotOwnedCompletedBrush"] as Brush)!;
	}
}
