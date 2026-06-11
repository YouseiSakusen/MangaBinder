namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 画像変換処理の結果を保持するレコードです。
/// </summary>
/// <param name="Stream">変換後画像のストリーム。呼び出し元が Dispose します。</param>
/// <param name="Width">変換元画像の幅（ピクセル）。</param>
/// <param name="Height">変換元画像の高さ（ピクセル）。</param>
public sealed record ConvertedImageResult(Stream Stream, int Width, int Height)
{
	/// <summary>横長画像（幅 &gt; 高さ）かどうかを取得します。</summary>
	public bool IsLandscape => this.Width > this.Height;

	/// <summary>長辺の長さ（ピクセル）を取得します。</summary>
	public int LongSide => Math.Max(this.Width, this.Height);

	/// <summary>短辺の長さ（ピクセル）を取得します。</summary>
	public int ShortSide => Math.Min(this.Width, this.Height);
}
