namespace MangaBinder.Binding;

/// <summary>
/// 作品素材構造を走査して <see cref="MaterialVolumeNode"/> ツリーを返すインターフェースです。
/// </summary>
public interface ISeriesExtractor
{
    /// <summary>
    /// 指定されたパスを走査し、素材構造を表す <see cref="MaterialVolumeNode"/> を返します。
    /// </summary>
    /// <param name="path">走査対象の作品フォルダパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>素材構造のルートノード。</returns>
    ValueTask<MaterialVolumeNode> ExtractAsync(string path, CancellationToken ct);
}
