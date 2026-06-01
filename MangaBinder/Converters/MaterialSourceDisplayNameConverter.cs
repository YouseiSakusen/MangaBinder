using MangaBinder.Settings;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace MangaBinder.Converters;

/// <summary>
/// <see cref="MangaSource"/> のパスから表示名を解決するコンバーターです。
/// <see cref="AppSettings.SourceFolders"/> との最長前方一致で <see cref="SourceFolder.DisplayName"/> を返します。
/// 一致しない場合は <see cref="Path.GetFileName"/> の結果を返します。
/// </summary>
public class MaterialSourceDisplayNameConverter : IValueConverter
{
	private readonly AppSettings appSettings;

	/// <summary>
	/// <see cref="MaterialSourceDisplayNameConverter"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="appSettings">アプリケーション設定。</param>
	public MaterialSourceDisplayNameConverter(AppSettings appSettings)
	{
		this.appSettings = appSettings;
	}

	/// <inheritdoc/>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not MangaSource source)
			return string.Empty;

		var bestMatch = this.appSettings.SourceFolders
			.Where(f => source.Path.StartsWith(f.FolderPath.Value, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(f => f.FolderPath.Value.Length)
			.FirstOrDefault();

		if (bestMatch is not null && !string.IsNullOrEmpty(bestMatch.DisplayName.Value))
			return bestMatch.DisplayName.Value;

		return Path.GetFileName(source.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
	}

	/// <inheritdoc/>
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
