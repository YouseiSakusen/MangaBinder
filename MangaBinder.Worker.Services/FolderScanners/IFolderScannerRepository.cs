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
    /// Path 一致によって既存 SeriesId が確定している素材フォルダについて、
    /// 既存の MangaSeries を直接更新します。
    /// Title / ShortTitle / SeriesCompleted / IsOwnedCompleted / StartVolume / EndVolume / OwnedMaxVolume / MaterialFolderCreatedAt / IsSourceMissing=0 / Author不変を反映します。
    /// </summary>
    /// <param name="seriesId">更新対象の既存作品ID。</param>
    /// <param name="series">更新内容を持つ作品オブジェクト。Sources 含む。</param>
    /// <param name="updateSource">更新元を表す文字列。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaBinder.MangaSeries"/>。</returns>
    ValueTask<MangaBinder.MangaSeries> UpdateMaterialSeriesByPathAsync(long seriesId, MangaBinder.MangaSeries series, string updateSource, CancellationToken ct);

    /// <summary>
    /// 素材スキャン（Phase 2 Path 不一致）で、新規 MangaSeries を新規作成する場合に使用します。
    /// ParseAsMaterial() で取得した Author を保存対象に含めます。
    /// </summary>
    /// <param name="series">新規作成対象の作品。Sources 含む。Author も含める場合は設定済みの状態で渡す。</param>
    /// <param name="updateSource">更新元を表す文字列。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上に生成された最新 <see cref="MangaBinder.MangaSeries"/>（SeriesId を含む）。</returns>
    ValueTask<MangaBinder.MangaSeries> InsertMaterialSeriesAsync(MangaBinder.MangaSeries series, string updateSource, CancellationToken ct);

    /// <summary>
    /// 素材スキャン（Phase 2 Path 不一致）で、既存 MangaSeries を更新する場合に使用します。
    /// Title / ShortTitle / SeriesCompleted / IsOwnedCompleted / StartVolume / EndVolume / OwnedMaxVolume / MaterialFolderCreatedAt / IsSourceMissing=0 を反映します。
    /// Author は既存値を維持し、上書きしません。
    /// </summary>
    /// <param name="seriesId">更新対象の既存作品ID。</param>
    /// <param name="series">更新内容を持つ作品オブジェクト。Sources 含む。</param>
    /// <param name="updateSource">更新元を表す文字列。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaBinder.MangaSeries"/>。</returns>
    ValueTask<MangaBinder.MangaSeries> UpdateMaterialSeriesAsync(long seriesId, MangaBinder.MangaSeries series, string updateSource, CancellationToken ct);

    /// <summary>
    /// 製本済みスキャンの新規 MangaSeries を新規作成する場合に使用します。
    /// </summary>
    /// <param name="series">新規作成対象の作品。Sources 含む。</param>
    /// <param name="updateSource">更新元を表す文字列。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上に生成された最新 <see cref="MangaBinder.MangaSeries"/>（SeriesId を含む）。</returns>
    ValueTask<MangaBinder.MangaSeries> InsertBindingSeriesAsync(MangaBinder.MangaSeries series, string updateSource, CancellationToken ct);

    /// <summary>
    /// 製本済みスキャンで、既存 MangaSeries を更新する場合に使用します。
    /// BoundEndVolume / UpdatedAt / UpdateSource のみを更新し、その他のメタ情報は上書きしません。
    /// </summary>
    /// <param name="seriesId">更新対象の既存作品ID。</param>
    /// <param name="series">更新内容を持つ作品オブジェクト。Sources 含む。</param>
    /// <param name="updateSource">更新元を表す文字列。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>DB上でマージ済みの最新 <see cref="MangaBinder.MangaSeries"/>。</returns>
    ValueTask<MangaBinder.MangaSeries> UpdateBindingSeriesAsync(long seriesId, MangaBinder.MangaSeries series, string updateSource, CancellationToken ct);

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

    /// <summary>
    /// 複数の NormalizedTitleInternal に対応する MangaSeries 候補を一括取得します。
    /// Material / Binding の両スキャナで使用されるスナップショット取得用（Parallel 前に一度だけ呼び出し）。
    /// </summary>
    /// <param name="normalizedTitles">検索対象の正規化タイトル一覧。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>NormalizedTitleInternal をキーとした、候補 MangaSeries の辞書。キーに存在しないタイトルは空リストに対応。</returns>
    ValueTask<IReadOnlyDictionary<string, IReadOnlyList<MangaBinder.MangaSeries>>> GetCandidateSeriesByNormalizedTitlesAsync(IEnumerable<string> normalizedTitles, CancellationToken ct);
}
