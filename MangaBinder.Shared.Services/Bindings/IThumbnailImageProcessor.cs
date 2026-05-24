using MangaBinder.Settings;

namespace MangaBinder.Bindings;

/// <summary>
/// サムネイル生成処理の抽象インターフェースです。
/// </summary>
public interface IThumbnailImageProcessor
{
    /// <summary>
    /// 入力ストリームからサムネイルを生成し、JPEG ストリームとして返します。
    /// </summary>
    /// <param name="input">元画像のストリーム。</param>
    /// <param name="options">サムネイル生成オプション。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>JPEG 画像を含む <see cref="Stream"/>。</returns>
    ValueTask<Stream> ProcessThumbnailAsync(
        Stream input,
        ThumbnailOptions options,
        CancellationToken ct);
}
