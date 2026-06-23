using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// VolumeSelectionPage の TreeView 表示用ノードです。
/// </summary>
public class MaterialVolumeNode : IDisposable
{
    private DisposableBag disposableBag;

    /// <summary>ノードの種別を取得します。</summary>
    public MaterialItemType NodeType { get; init; }

    /// <summary>ノードの表示名を取得します。</summary>
    public BindableReactiveProperty<string> Name { get; }

    /// <summary>ノードのフルパスを取得します。</summary>
    public BindableReactiveProperty<string> FullPath { get; }

    /// <summary>Archive ノードのファイルサイズ表示用テキストを取得します（例：「1.2 GB」）。Archive 以外は空文字。</summary>
    public string FileSizeText { get; init; } = string.Empty;

    /// <summary>製本対象としてカウントするファイル数を取得します。</summary>
    public int FileCount { get; init; }

    /// <summary>ノード直下に存在する画像ファイルの展開後サイズ合計（バイト）を取得します。</summary>
    public long TotalImageBytes { get; init; }

    /// <summary>
    /// 解凍元の実パスを取得します。
    /// 実フォルダの場合はフォルダパス、Archive 内部フォルダの場合は Archive ファイルパス、Epub の場合は Epub ファイルパス。
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Archive 内部フォルダの場合のエントリ接頭辞を取得します。
    /// 実フォルダ・Epub では <see langword="null"/>。
    /// </summary>
    public string? ArchiveEntryPrefix { get; init; }

    /// <summary>製本対象として選択されているかを取得します。</summary>
    public BindableReactiveProperty<bool> IsChecked { get; }

    /// <summary>ノードが展開されているかを取得します。</summary>
    public BindableReactiveProperty<bool> IsExpanded { get; }

    /// <summary>子ノードの一覧を取得します。</summary>
    public ObservableList<MaterialVolumeNode> Children { get; }

    /// <summary>親ノードを取得します。Root ノードの場合は <see langword="null"/>。</summary>
    public MaterialVolumeNode? Parent { get; private set; }

    /// <summary>デフォルトで選択可能なノードかどうかを取得します。</summary>
    public bool IsSelectableByDefault { get; }

    /// <summary>選択不可の理由を取得します。選択可能なノードの場合は <see langword="null"/>。</summary>
    public string? SelectionDisabledReason { get; }

    /// <summary>右クリックによる例外許可が有効かどうかを取得します。</summary>
    public BindableReactiveProperty<bool> IsSelectionOverrideEnabled { get; }

    /// <summary>チェック可能なノードかどうかを取得します。</summary>
    public BindableReactiveProperty<bool> CanCheck { get; }

    /// <summary>選択可能に切り替えるコマンドを取得します（右クリックメニューから実行）。</summary>
    public ReactiveCommand EnableSelectionOverrideCommand { get; }

    /// <summary>IsChecked を反転するコマンドを取得します。</summary>
    public ReactiveCommand ToggleCheckedCommand { get; }

    /// <summary>
    /// <see cref="MaterialVolumeNode"/> の新しいインスタンスを初期化します。
    /// </summary>
    /// <param name="name">表示名。</param>
    /// <param name="fullPath">フルパス。</param>
    /// <param name="nodeType">ノード種別。</param>
    /// <param name="isSelectableByDefault">デフォルトで選択可能かどうか。</param>
    /// <param name="selectionDisabledReason">選択不可の理由。選択可能な場合は <see langword="null"/>。</param>
    public MaterialVolumeNode(string name, string fullPath, MaterialItemType nodeType = MaterialItemType.Folder,
        bool isSelectableByDefault = true, string? selectionDisabledReason = null)
    {
        this.NodeType = nodeType;
        this.IsSelectableByDefault = isSelectableByDefault;
        this.SelectionDisabledReason = selectionDisabledReason;
        this.Name = new BindableReactiveProperty<string>(name)
            .AddTo(ref this.disposableBag);
        this.FullPath = new BindableReactiveProperty<string>(fullPath)
            .AddTo(ref this.disposableBag);
        this.IsChecked = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);
        this.IsExpanded = new BindableReactiveProperty<bool>(true)
            .AddTo(ref this.disposableBag);
        this.Children = new ObservableList<MaterialVolumeNode>();

        this.IsSelectionOverrideEnabled = new BindableReactiveProperty<bool>(false)
            .AddTo(ref this.disposableBag);

        // CanCheck: Archive は常に false。それ以外は IsSelectableByDefault または IsSelectionOverrideEnabled で決まる
        this.CanCheck = new BindableReactiveProperty<bool>(this.computeCanCheck())
            .AddTo(ref this.disposableBag);
        this.IsSelectionOverrideEnabled
            .Subscribe(_ => this.CanCheck.Value = this.computeCanCheck())
            .AddTo(ref this.disposableBag);

        this.EnableSelectionOverrideCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.EnableSelectionOverrideCommand.Subscribe(_ =>
        {
            this.IsSelectionOverrideEnabled.Value = true;
        }).AddTo(ref this.disposableBag);

        this.ToggleCheckedCommand = new ReactiveCommand()
            .AddTo(ref this.disposableBag);
        this.ToggleCheckedCommand.Subscribe(_ =>
        {
            if (!this.CanCheck.Value)
                return;
            this.IsChecked.Value = !this.IsChecked.Value;
        }).AddTo(ref this.disposableBag);
        this.IsChecked
            .Subscribe(value =>
            {
                if (this.NodeType == MaterialItemType.Root)
                {
                    this.setCheckedRecursive(value);
                    return;
                }

                if (!value)
                    return;

                if (!this.isBindingTargetNode())
                    return;

                this.uncheckCheckedAncestors();
                this.uncheckCheckedDescendants();
            })
            .AddTo(ref this.disposableBag);
    }

    /// <summary>CanCheck の値を算出します。</summary>
    private bool computeCanCheck()
    {
        if (this.NodeType == MaterialItemType.Archive)
            return false;

        return this.NodeType == MaterialItemType.Root
            || this.IsSelectableByDefault
            || this.IsSelectionOverrideEnabled.Value;
    }

    /// <summary>
    /// 親ノードを設定します。
    /// </summary>
    /// <param name="parent">設定する親ノード。</param>
    public void SetParent(MaterialVolumeNode parent)
        => this.Parent = parent;

    /// <summary>このノードが親子排他処理の対象かどうかを返します。</summary>
    private bool isBindingTargetNode()
        => this.NodeType != MaterialItemType.Root
        && this.NodeType != MaterialItemType.Archive
        && this.CanCheck.Value;

    /// <summary>チェック済みの祖先ノードを OFF にします（Root / Archive は除外）。</summary>
    private void uncheckCheckedAncestors()
    {
        var current = this.Parent;
        while (current is not null)
        {
            if (current.NodeType != MaterialItemType.Root
                && current.NodeType != MaterialItemType.Archive
                && current.IsChecked.Value)
            {
                current.IsChecked.Value = false;
            }
            current = current.Parent;
        }
    }

    /// <summary>配下のチェック済みノードを OFF にします（Archive 自身は除外、配下は再帰対象）。</summary>
    private void uncheckCheckedDescendants()
    {
        foreach (var child in this.Children)
        {
            if (child.NodeType != MaterialItemType.Archive && child.IsChecked.Value)
                child.IsChecked.Value = false;

            child.uncheckCheckedDescendants();
        }
    }

    /// <summary>
    /// 配下の Folder / Epub ノードへ IsChecked を再帰的に反映します。
    /// Archive 自身の IsChecked は変更しませんが、Archive 配下の Children は再帰対象に含めます。
    /// </summary>
    /// <param name="value">設定するチェック状態。</param>
    private void setCheckedRecursive(bool value)
    {
        foreach (var child in this.Children)
        {
            if (child.CanCheck.Value)
                child.IsChecked.Value = value;

            child.setCheckedRecursive(value);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var child in this.Children)
            child.Dispose();

        this.disposableBag.Dispose();
    }
}
