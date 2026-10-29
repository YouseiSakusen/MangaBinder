using System.Globalization;
using System.Windows.Data;
using MangaBinder.Settings;

namespace MangaBinder.Converters;

/// <summary>
/// 製本後ファイル名の設定値から、設定画面表示用のファイル名サンプルを生成するマルチバリューコンバーターです。
/// </summary>
/// <remarks>
/// バインド値：
/// [0] 著者名左括弧 / [1] 著者名右括弧 / [2] 区切り文字
/// [3] 通常巻数Prefix / [4] 通常巻数Separator / [5] 通常巻数Suffix
/// [6] 完結を表す文字 / [7] 完結巻数Suffix / [8] 完結文字の位置
/// [9] 開始巻～完結表記の使用有無 / [10] 完結巻数のゼロ埋め / [11] 拡張子
///
/// パラメーター：
/// "Range" / "Complete" / "CompleteSingleDigit" / "RangeToComplete"
/// </remarks>
public sealed class BindingZipFileNameSampleConverter : IMultiValueConverter
{
	private const int valueCount = 12;

	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length < valueCount || values.Any(v => v is null || v == System.Windows.DependencyProperty.UnsetValue))
			return string.Empty;

		var left = (string)values[0];
		var right = (string)values[1];
		var sep = (string)values[2];
		var normalPrefix = (string)values[3];
		var normalSeparator = (string)values[4];
		var normalSuffix = (string)values[5];
		var completeMarker = (string)values[6];
		var completeSuffix = (string)values[7];
		var position = (CompleteMarkerPosition)values[8];
		var usePartial = (bool)values[9];
		var zeroPadding = (bool)values[10];
		var extension = (string)values[11];

		var head = $"{left}作者名{right}{sep}タイトル{sep}";

		string completeText(int volume)
		{
			var number = zeroPadding ? volume.ToString().PadLeft(2, '0') : volume.ToString();
			return position == CompleteMarkerPosition.BeforeVolume
				? $"{completeMarker}{number}{completeSuffix}"
				: $"{number}{completeSuffix}{completeMarker}";
		}

		var volumeText = (parameter as string) switch
		{
			"Range" => $"{normalPrefix}01{normalSeparator}20{normalSuffix}",
			"Complete" => completeText(38),
			"CompleteSingleDigit" => completeText(8),
			"RangeToComplete" => usePartial
				? $"{normalPrefix}31{normalSeparator}{completeText(38)}"
				: $"{normalPrefix}31{normalSeparator}38{normalSuffix}",
			_ => string.Empty,
		};

		return $"{head}{volumeText}{extension}";
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
