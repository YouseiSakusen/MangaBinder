using System.Globalization;
using System.Windows.Data;
using MangaBinder.Settings;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="FolderRole"/> と既定の製本先 ToggleSwitch の bool を相互変換するコンバーターです。
/// </summary>
public class FolderRoleToDefaultBindingConverter : IValueConverter
{
	/// <summary>
	/// <see cref="FolderRole"/> を bool に変換します。
	/// </summary>
	/// <param name="value">フォルダの役割。</param>
	/// <param name="targetType">ターゲットの型。</param>
	/// <param name="parameter">使用されません。</param>
	/// <param name="culture">使用するカルチャ。</param>
	/// <returns>DefaultBinding の場合は true、それ以外は false。</returns>
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is FolderRole role && role == FolderRole.DefaultBinding;

	/// <summary>
	/// bool を <see cref="FolderRole"/> に逆変換します。
	/// </summary>
	/// <param name="value">ToggleSwitch の状態。</param>
	/// <param name="targetType">ターゲットの型。</param>
	/// <param name="parameter">使用されません。</param>
	/// <param name="culture">使用するカルチャ。</param>
	/// <returns>true の場合は DefaultBinding、false の場合は Binding。</returns>
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is true ? FolderRole.DefaultBinding : FolderRole.Binding;
}
