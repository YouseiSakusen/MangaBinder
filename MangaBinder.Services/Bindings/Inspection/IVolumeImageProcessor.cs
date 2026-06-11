namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 製本用画像変換処理の抽象インターフェースです。
/// </summary>
public interface IVolumeImageProcessor
{
	/// <summary>
	/// 製本用に画像を既定フォーマットへ変換し、変換後ストリームと画像サイズを返します。
	/// </summary>
	/// <param name="sourceStream">変換元画像のストリーム。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>
	/// 変換後画像のストリームと Width / Height を含む <see cref="ConvertedImageResult"/>。
	/// ストリームの Dispose は呼び出し元が行います。
	/// </returns>
	ValueTask<ConvertedImageResult> ConvertAsync(
		Stream sourceStream,
		CancellationToken cancellationToken = default);
}
