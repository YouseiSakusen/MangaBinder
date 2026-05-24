namespace MangaBinder.Bindings;

/// <summary>
/// アーカイブからの抽出結果を保持するクラスです。
/// </summary>
public sealed class ThumbnailExtractionResult : IDisposable
{
    /// <summary>抽出結果の状態。</summary>
    public ExtractionStatus Status { get; init; }

    /// <summary>抽出された画像ストリーム。<see cref="ExtractionStatus.Success"/> 以外の場合は <c>null</c>。</summary>
    public Stream? ImageStream { get; init; }

    /// <inheritdoc/>
    public void Dispose() => ImageStream?.Dispose();
}
