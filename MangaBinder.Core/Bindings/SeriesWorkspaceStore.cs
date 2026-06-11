using MangaBinder.Bindings.Inspection;
using MangaBinder.Bindings.Prepress;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本対象として選択された作品の一覧を保持する Singleton ストアです。
/// </summary>
public class SeriesWorkspaceStore : IDisposable
{
    private DisposableBag disposableBag;

    /// <summary>現在選択中の作品一覧を取得します。</summary>
    public List<MangaSeries> SelectedSeries { get; } = [];

    /// <summary>製本工程で現在処理対象とする単一作品を取得します。</summary>
    public MangaSeries? BindingTarget { get; private set; }

    /// <summary>VolumeSelectionPage で確定した巻の一覧を取得します。</summary>
    public List<BindingSourceVolume> SelectedMaterialVolumes { get; } = [];

    /// <summary>中間フォルダを再作成するかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> RecreateWorkFolder { get; }

    /// <summary>製本前処理（Prepress）対象巻の辞書を取得します。キーは WorkVolumeFolderPath です。</summary>
    public Dictionary<string, VolumeInspectionResult> PrepressVolumes { get; } = [];

    /// <summary>現在の Prepress 処理対象巻キー（WorkVolumeFolderPath）を取得します。</summary>
    public string? CurrentPrepressVolumeKey { get; private set; }

    /// <summary>Prepress 巻単位の作業状態辞書を取得します。キーは WorkVolumeFolderPath です。</summary>
    private Dictionary<string, PrepressVolumeWorkspace> PrepressWorkspaces { get; } = [];

    /// <summary>
    /// <see cref="SeriesWorkspaceStore"/> の新しいインスタンスを初期化します。
    /// </summary>
    public SeriesWorkspaceStore()
    {
        this.RecreateWorkFolder = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);
    }

    /// <summary>
    /// 製本対象作品を設定します。
    /// </summary>
    /// <param name="series">製本対象作品。</param>
    public void SetBindingTarget(MangaSeries series)
    {
        this.BindingTarget = series;
    }

    /// <summary>
    /// 製本対象作品をクリアします。
    /// </summary>
    public void ClearBindingTarget()
    {
        this.BindingTarget = null;
    }

    /// <summary>
    /// Prepress 対象巻を辞書に登録します。既に同キーが存在する場合は上書きします。
    /// </summary>
    /// <param name="result">登録する巻の検査結果。</param>
    public void RegisterPrepressVolume(VolumeInspectionResult result)
    {
        this.PrepressVolumes[result.WorkVolumeFolderPath] = result;
    }

    /// <summary>
    /// 現在の Prepress 処理対象巻を切り替えます。
    /// </summary>
    /// <param name="result">対象とする巻の検査結果。</param>
    public void SetCurrentPrepressVolume(VolumeInspectionResult result)
    {
        this.RegisterPrepressVolume(result);
        this.CurrentPrepressVolumeKey = result.WorkVolumeFolderPath;
    }

    /// <summary>
    /// 現在の Prepress 処理対象巻を取得します。見つからない場合は <see langword="null"/> を返します。
    /// </summary>
    /// <returns>現在対象の <see cref="VolumeInspectionResult"/>。対象未設定の場合は <see langword="null"/>。</returns>
    public VolumeInspectionResult? GetCurrentPrepressVolume()
    {
        if (this.CurrentPrepressVolumeKey is null)
            return null;

        return this.PrepressVolumes.TryGetValue(this.CurrentPrepressVolumeKey, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// 指定した巻の <see cref="PrepressVolumeWorkspace"/> を登録または上書きします。
    /// </summary>
    /// <param name="workspace">登録する作業状態。</param>
    public void SetPrepressWorkspace(PrepressVolumeWorkspace workspace)
    {
        var key = workspace.VolumeInspectionResult.WorkVolumeFolderPath;
        this.PrepressWorkspaces[key] = workspace;
    }

    /// <summary>
    /// 現在の Prepress 処理対象巻の <see cref="PrepressVolumeWorkspace"/> を取得します。
    /// 見つからない場合は <see langword="null"/> を返します。
    /// </summary>
    /// <returns>現在対象の <see cref="PrepressVolumeWorkspace"/>。未設定の場合は <see langword="null"/>。</returns>
    public PrepressVolumeWorkspace? GetCurrentPrepressWorkspace()
    {
        if (this.CurrentPrepressVolumeKey is null)
            return null;

        return this.PrepressWorkspaces.TryGetValue(this.CurrentPrepressVolumeKey, out var workspace)
            ? workspace
            : null;
    }

    /// <inheritdoc/>
    public void Dispose()
        => this.disposableBag.Dispose();
}
