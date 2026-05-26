using System.IO;
using NetVips;

namespace MangaBinder.Binding.Prepress;

/// <summary>
/// Prepress 用サムネイル画像を NetVips を使用して生成するクラスです。
/// </summary>
public sealed class VolumeThumbnailImageProcessor
{
	/// <summary>サムネイルの目標幅（ピクセル）。</summary>
	private const int ThumbnailWidth = 140;

	/// <summary>対応外ファイル用フォールバック PNG ファイル名。</summary>
	public const string UnsupportedFormatResource = "unsupported-format";

	/// <summary>読み込み失敗用フォールバック PNG ファイル名。</summary>
	public const string LoadFailedResource = "load-failed";

	/// <summary>
	/// 指定された画像ファイルからサムネイル JPEG バイト列を生成します。
	/// </summary>
	/// <param name="filePath">元画像のフルパス。</param>
	/// <returns>JPEG バイト列。失敗した場合は <see langword="null"/>。</returns>
	public byte[]? GenerateThumbnail(string filePath)
	{
		try
		{
			using var source = NetVips.Image.NewFromFile(filePath, access: Enums.Access.Sequential);

			var scale = (double)ThumbnailWidth / source.Width;
			using var resized = source.Resize(scale, kernel: Enums.Kernel.Lanczos3);
			using var flat = resized.HasAlpha() ? resized.Flatten() : resized;

			return flat.WriteToBuffer(".jpg", new VOption { { "Q", 80 } });
		}
		catch
		{
			return null;
		}
	}
}
