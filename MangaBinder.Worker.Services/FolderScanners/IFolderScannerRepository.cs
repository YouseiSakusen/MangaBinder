using MangaBinder.Bindings;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// フォルダスキャン用リポジトリのインターフェースです。
/// </summary>
public interface IFolderScannerRepository
{
    /// <summary>
    /// 指定された役割のスキャン対象フォルダパス一覧を非同期で取得します。
    /// </summary>
    /// <param name="role">フォルダの役割を表す値（<see cref="MangaBinder.Settings.FolderRole"/> のキャスト値）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>フォルダのフルパス一覧。</returns>
    ValueTask<IEnumerable<string>> GetSourceFoldersAsync(int role, CancellationToken ct);

    /// <summary>
    /// 素材スキャン結果を 1 件単位で UPSERT 保存し、保存後のDB最新状態の <see cref="MangaBinder.MangaSeries"/> を返します。
    /// Author は更新対象から除外され、EndVolume / IsOwnedCompleted 等が反映されます。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaBinder.MangaSeries"/>。</returns>
    ValueTask<MangaBinder.MangaSeries> SaveMaterialSeriesAsync(MangaBinder.MangaSeries series, CancellationToken ct);

    /// <summary>
    /// 製本済みスキャン結果を 1 件単位で UPSERT 保存し、保存後のDB最新状態の <see cref="MangaBinder.MangaSeries"/> を返します。
    /// Author を上書きし、BoundEndVolume / SeriesCompleted 等が反映されます。
    /// </summary>
    /// <param name="series">保存対象の作品。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaBinder.MangaSeries"/>。</returns>
    ValueTask<MangaBinder.MangaSeries> SaveBindingSeriesAsync(MangaBinder.MangaSeries series, CancellationToken ct);

    /// <summary>
    /// サムネイル情報を更新します。
    /// </summary>
    /// <param name="seriesId">対象作品ID。</param>
    /// <param name="thumbnailFileName">サムネイルファイル名。</param>
    /// <param name="thumbnailStatus">サムネイル処理状態。</param>
    /// <param name="ct">キャンセルトークン。</param>
    ValueTask UpdateThumbnailAsync(long seriesId, string thumbnailFileName, ThumbnailStatus thumbnailStatus, CancellationToken ct);

    /// <summary>
    /// <see cref="ThumbnailStatus.LimitExceeded"/> の作品が 1 件以上存在するかを返します。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>1 件以上存在する場合は <c>true</c>。</returns>
    ValueTask<bool> HasLimitExceededAsync(CancellationToken ct);

    /// <summary>
    /// 指定された役割のスキャン対象フォルダ配下に存在する MangaSource 一覧を取得します。
    /// スキャン開始時に呼び出し、スキャン中に見つかった作品と比較して、削除された作品を検出するために使用します。
    /// </summary>
    /// <param name="role">フォルダの役割を表す値（<see cref="MangaBinder.Settings.FolderRole"/> のキャスト値）。</param>
    /// <param name="sourceFolderPaths">スキャン対象フォルダパス一覧。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>SourceId をキーとした MangaSource の辞書。</returns>
    ValueTask<Dictionary<long, MangaSource>> GetSourcesByFolderRoleAsync(int role, IEnumerable<string> sourceFolderPaths, CancellationToken ct);

    /// <summary>
    /// 指定された SourceId の MangaSource を削除します。
    /// </summary>
    /// <param name="sourceIds">削除対象の SourceId 一覧。</param>
    /// <param name="ct">キャンセルトークン。</param>
    ValueTask DeleteSourcesByIdAsync(IEnumerable<long> sourceIds, CancellationToken ct);
}
