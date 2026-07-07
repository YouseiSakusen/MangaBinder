using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MangaBinder.Series;

/// <summary>
/// WPF 用のサムネイル操作クラスです。
/// Clipboard や画像ファイルから BitmapSource を取得し、byte[] へ変換する機能を提供します。
/// 状態は保持しません。
/// </summary>
public class ThumbnailPicker
{
	/// <summary>
	/// Clipboard から BitmapSource を取得します。
	/// </summary>
	/// <returns>Clipboard に画像が存在する場合は BitmapSource、存在しない場合は null。</returns>
	public BitmapSource? GetFromClipboard()
	{
		try
		{
			return Clipboard.GetImage() as BitmapSource;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// ファイルから BitmapSource を読み込みます。
	/// ファイルロックは保持しません。
	/// </summary>
	/// <param name="fileName">読み込むファイルのパス。</param>
	/// <returns>ファイルが存在する場合は BitmapSource、存在しない場合は null。</returns>
	public BitmapSource? LoadFromFile(string fileName)
	{
		if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
		{
			return null;
		}

		try
		{
			var bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.UriSource = new Uri(fileName, UriKind.Absolute);
			bitmap.CacheOption = BitmapCacheOption.OnLoad; // ファイルロック解放
			bitmap.EndInit();
			bitmap.Freeze();

			return bitmap;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// BitmapSource を PNG 形式の byte[] に変換します。
	/// </summary>
	/// <param name="bitmap">変換する BitmapSource。null の場合は null を返す。</param>
	/// <returns>PNG 形式の byte[]、または null。</returns>
	public byte[]? ToBytes(BitmapSource? bitmap)
	{
		if (bitmap is null)
		{
			return null;
		}

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));

		using var stream = new MemoryStream();
		encoder.Save(stream);
		return stream.ToArray();
	}
}
