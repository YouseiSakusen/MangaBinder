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

	/// <summary>
	/// Workへ実体化済みの BindingImage を処理します。
	/// 画像をOpenして Width/Height を取得し、必要に応じて既定形式へ変換します。
	/// 同じ BindingImage インスタンスを更新して返します。
	/// </summary>
	/// <param name="image">処理対象の BindingImage。FilePath が設定済みである必要があります。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>非同期処理のタスク。</returns>
	ValueTask ProcessAsync(BindingImage image, CancellationToken cancellationToken = default);
}
