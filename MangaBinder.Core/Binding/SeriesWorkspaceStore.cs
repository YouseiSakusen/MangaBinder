using R3;

namespace MangaBinder.Binding;

/// <summary>
/// 製本対象として選択された作品の一覧を保持する Singleton ストアです。
/// </summary>
public class SeriesWorkspaceStore : IDisposable
{
    private DisposableBag disposableBag;

    /// <summary>現在選択中の作品一覧を取得します。</summary>
    public List<MangaSeries> SelectedSeries { get; } = [];

    /// <summary>VolumeSelectionPage で確定した巻の一覧を取得します。</summary>
    public List<BindingSourceVolume> SelectedMaterialVolumes { get; } = [];

    /// <summary>中間フォルダを再作成するかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> RecreateWorkFolder { get; }

    /// <summary>
    /// <see cref="SeriesWorkspaceStore"/> の新しいインスタンスを初期化します。
    /// </summary>
    public SeriesWorkspaceStore()
    {
        this.RecreateWorkFolder = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);
    }

    /// <inheritdoc/>
    public void Dispose()
        => this.disposableBag.Dispose();
}
