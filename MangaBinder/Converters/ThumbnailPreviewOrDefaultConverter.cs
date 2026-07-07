using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MangaBinder.Converters;

/// <summary>
/// プレビュー画像がある場合はそれを優先表示し、ない場合は既存のサムネイル（Converter経由）を表示するマルチバインディング Converter です。
/// </summary>
[ValueConversion(typeof(object[]), typeof(BitmapSource))]
public class ThumbnailPreviewOrDefaultConverter : IMultiValueConverter
{
	/// <summary>プレビュー画像がない場合に既存サムネイルを取得する Converter。</summary>
	private readonly ThumbnailImageConverter? defaultConverter;

	/// <summary>
	/// <see cref="ThumbnailPreviewOrDefaultConverter"/> の新しいインスタンスを初期化します。
	/// </summary>
	public ThumbnailPreviewOrDefaultConverter()
	{
		// DI コンテナから ThumbnailImageConverter を取得する
		this.defaultConverter = (ThumbnailImageConverter?)App.Services.GetService(typeof(ThumbnailImageConverter));
	}

	/// <summary>
	/// マルチバインディングの値を ImageSource に変換します。
	/// </summary>
	/// <param name="values">
	/// values[0]: ThumbnailPreviewImageSource (BitmapSource?)
	/// values[1]: EditingSeries (MangaSeries?)
	/// </param>
	/// <param name="targetType">ターゲット型。</param>
	/// <param name="parameter">パラメータ。</param>
	/// <param name="culture">カルチャ。</param>
	/// <returns>表示する BitmapSource。</returns>
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		// values[0]: ThumbnailPreviewImageSource
		// values[1]: EditingSeries
		if (values == null || values.Length < 2)
			return null!;

		var previewImageSource = values[0] as BitmapSource;

		// プレビュー画像がある場合は優先表示
		if (previewImageSource != null)
			return previewImageSource;

		// プレビュー画像がない場合は既存の ThumbnailImageConverter で処理
		if (this.defaultConverter != null && values[1] != null)
			return this.defaultConverter.Convert(values[1], targetType, parameter, culture) ?? null!;

		return null!;
	}

	/// <summary>逆変換は実装しません。</summary>
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
