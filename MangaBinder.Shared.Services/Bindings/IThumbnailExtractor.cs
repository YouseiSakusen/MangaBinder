namespace MangaBinder.Bindings;

/// <summary>
/// 作品ソースファイルからサムネイル画像を抽出するためのインターフェースです。
/// </summary>
public interface IThumbnailExtractor
{
    /// <summary>
    /// 指定されたパスからサムネイル画像を非同期で取得します。
    /// </summary>
    /// <param name="path">対象ファイルまたはディレクトリのパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>抽出結果を保持する <see cref="ThumbnailExtractionResult"/>。</returns>
    ValueTask<ThumbnailExtractionResult> GetThumbnailImageAsync(string path, CancellationToken ct);
}
