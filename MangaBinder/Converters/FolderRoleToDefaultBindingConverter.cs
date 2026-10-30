using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MangaBinder.Settings;

namespace MangaBinder.Converters;

/// <summary>
/// FolderRole を既定の製本先 ToggleSwitch 用の値に変換するコンバーターです。
/// 通常は IsChecked 用の bool（DefaultBinding のみ true）へ変換し、
/// parameter に "Visibility" を指定すると Visibility（Material のみ Collapsed）へ変換します。
/// </summary>
public class FolderRoleToDefaultBindingConverter : IValueConverter
{
	/// <inheritdoc/>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var role = value is FolderRole r ? r : FolderRole.Material;

		if (parameter is string p && p.Equals("Visibility", StringComparison.OrdinalIgnoreCase))
		{
			return role == FolderRole.Material ? Visibility.Collapsed : Visibility.Visible;
		}

		return role == FolderRole.DefaultBinding;
	}

	/// <inheritdoc/>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is true ? FolderRole.DefaultBinding : FolderRole.Binding;
	}
}
