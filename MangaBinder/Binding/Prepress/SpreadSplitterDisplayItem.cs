using System.IO;
using System.Windows.Media.Imaging;

namespace MangaBinder.Binding.Prepress;

/// <summary>
/// <see cref="PrepressImageItem"/> を WPF 表示用にラップした画面バインド用アイテムです。
/// </summary>
public sealed class SpreadSplitterDisplayItem
{
	/// <summary>元データを取得します。</summary>
	public PrepressImageItem SourceItem { get; }

	/// <summary>表示用画像を取得します。読み込み失敗時は <see langword="null"/>。</summary>
	public BitmapImage? DisplayImage { get; }

	/// <summary>ファイル名を取得します。</summary>
	public string FileName => this.SourceItem.FileName;

	/// <summary>分割対象かどうかを取得します。</summary>
	public bool IsSplitTarget => this.SourceItem.IsSplitTarget;

	/// <summary>分割対象外のとき <see langword="true"/> を返します。グレーオーバーレイ表示に使用します。</summary>
	public bool ShowDisabledOverlay => !this.SourceItem.IsSplitTarget;

	/// <summary>
	/// <see cref="SpreadSplitterDisplayItem"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="sourceItem">元データ。</param>
	/// <param name="imageBytes">画像バイト列。<see langword="null"/> の場合は画像なしで生成。</param>
	public SpreadSplitterDisplayItem(PrepressImageItem sourceItem, byte[]? imageBytes)
	{
		this.SourceItem = sourceItem;
		this.DisplayImage = imageBytes is { } bytes ? toBitmapImage(bytes) : null;
	}

	/// <summary>
	/// バイト列から <see cref="BitmapImage"/> を生成します。
	/// </summary>
	private static BitmapImage? toBitmapImage(byte[] bytes)
	{
		try
		{
			var bmp = new BitmapImage();
			using var ms = new MemoryStream(bytes);
			bmp.BeginInit();
			bmp.CacheOption = BitmapCacheOption.OnLoad;
			bmp.StreamSource = ms;
			bmp.EndInit();
			bmp.Freeze();
			return bmp;
		}
		catch
		{
			return null;
		}
	}
}
