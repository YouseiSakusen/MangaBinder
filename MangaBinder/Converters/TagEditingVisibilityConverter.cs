using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MangaBinder.Tags;

namespace MangaBinder.Converters;

/// <summary>
/// 現在の行タグが編集中かどうかを <see cref="Visibility"/> に変換するマルチバリューコンバーターです。
/// </summary>
/// <remarks>
/// バインド値：
/// [0] 行の MangaTag（DataContext）
/// [1] EditingTag.Value（ViewModel の編集中タグ）
///
/// パラメーター：
/// "Editing"  → 編集中の行に Visible を返す
/// "Normal"   → 通常表示の行に Visible を返す（既定）
/// </remarks>
public sealed class TagEditingVisibilityConverter : IMultiValueConverter
{
	/// <inheritdoc/>
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length < 2)
			return Visibility.Collapsed;

		var rowTag = values[0] as MangaTag;
		var editingTag = values[1] as MangaTag;

		var isEditing = rowTag is not null && ReferenceEquals(rowTag, editingTag);
		var wantEditing = parameter is string p && p == "Editing";

		return (isEditing == wantEditing) ? Visibility.Visible : Visibility.Collapsed;
	}

	/// <inheritdoc/>
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
