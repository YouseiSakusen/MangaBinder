using System.IO;
using MangaBinder.Bindings.Prepress;

namespace MangaBinder.Bindings.Prepress;

/// <summary>
/// <see cref="PrepressImageItem"/> から画像バイト列を読み込むクラスです。
/// </summary>
public sealed class SpreadSplitterImageLoader
{
	/// <summary>
	/// 指定したアイテムの画像ファイルを読み込み、バイト列を返します。
	/// </summary>
	/// <param name="item">読み込み対象のアイテム。</param>
	/// <returns>画像バイト列。対応外・エラー・ファイル未存在の場合は <see langword="null"/>。</returns>
	public byte[]? LoadImageBytes(PrepressImageItem item)
	{
		if (item.IsUnsupported || item.HasError)
			return null;

		if (!File.Exists(item.FilePath))
			return null;

		try
		{
			return File.ReadAllBytes(item.FilePath);
		}
		catch
		{
			return null;
		}
	}
}
