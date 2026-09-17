using System.IO;
using NetVips;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// SeriesInspection の巻カード用サムネイル画像を NetVips を使用して生成するクラスです。
/// 代表画像1枚を透過的な余白で表示できるよう、背景キャンバスを使用せず、
/// リサイズ後の原寸サイズでPNG出力します。
/// </summary>
public sealed class VolumeCardThumbnailImageProcessor
{
	/// <summary>
	/// 指定された画像ファイルをサムネイル用にリサイズし、PNGバイト列として生成します。
	/// 背景キャンバスは作成せず、アスペクト比を維持してリサイズされた画像そのものをPNG出力します。
	/// </summary>
	/// <param name="filePath">元画像のフルパス。</param>
	/// <param name="maxWidth">最大幅（ピクセル）。</param>
	/// <param name="maxHeight">最大高さ（ピクセル）。</param>
	/// <param name="cancellationToken">キャンセルトークン。</param>
	/// <returns>PNG形式のバイト列。</returns>
	/// <exception cref="ArgumentNullException"><paramref name="filePath"/> が null の場合。</exception>
	/// <exception cref="ArgumentException"><paramref name="filePath"/> が空白の場合。</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWidth"/> または <paramref name="maxHeight"/> が 0 以下の場合。</exception>
	/// <exception cref="OperationCanceledException">キャンセルが要求された場合。</exception>
	public byte[] GenerateThumbnail(
		string filePath,
		int maxWidth,
		int maxHeight,
		CancellationToken cancellationToken = default)
	{
		// 引数チェック
		if (filePath == null)
			throw new ArgumentNullException(nameof(filePath));

		if (string.IsNullOrWhiteSpace(filePath))
			throw new ArgumentException("ファイルパスは空白にできません。", nameof(filePath));

		if (maxWidth <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxWidth), "最大幅は0より大きい必要があります。");

		if (maxHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxHeight), "最大高さは0より大きい必要があります。");

		// 処理開始前のキャンセルチェック
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			// 画像を開く
			using var source = NetVips.Image.NewFromFile(filePath, access: Enums.Access.Sequential);

			// キャンセルチェック：主要処理前
			cancellationToken.ThrowIfCancellationRequested();

			// アスペクト比維持しながらリサイズ
			// scale = Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height)
			// 1.0以上への拡大は行わない
			var scale = Math.Min(
				(double)maxWidth / source.Width,
				(double)maxHeight / source.Height);
			scale = Math.Min(1.0, scale);

			// Lanczos3 を使用してリサイズ
			using var resized = source.Resize(scale, kernel: Enums.Kernel.Lanczos3);

			// キャンセルチェック：PNG出力前
			cancellationToken.ThrowIfCancellationRequested();

			// PNG形式でメモリへ出力（背景キャンバスは作成しない）
			return resized.WriteToBuffer(".png");
		}
		finally
		{
			// 明示的なクリーンアップ（using で自動的に行われるが、念のため）
		}
	}
}
