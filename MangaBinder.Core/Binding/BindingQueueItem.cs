using R3;

namespace MangaBinder.Binding;

/// <summary>
/// 右ListView（製本キュー）に表示する製本対象エントリです。
/// </summary>
public sealed class BindingQueueItem
{
	/// <summary>元の TreeView ノードを取得します。</summary>
	public MaterialVolumeNode SourceNode { get; }

	/// <summary>巻番号を取得または設定します。取得できない場合は <see langword="null"/>。</summary>
	public BindableReactiveProperty<decimal?> VolumeNumber { get; }

	/// <summary>ListView に表示する名称を取得します。</summary>
	public string DisplayName { get; }

	/// <summary>素材種別の表示文字列を取得します。</summary>
	public string SourceTypeText { get; }

	/// <summary>対象フォルダ配下の画像ファイル数を取得します。</summary>
	public int FileCount { get; }

	/// <summary>ファイル数列の表示文字列を取得します。EPUB の場合は "-"、それ以外は実数値。</summary>
	public string FileCountText { get; }

	/// <summary>
	/// <see cref="BindingQueueItem"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="node">対象 TreeView ノード。</param>
	/// <param name="volumeNumber">自動抽出した巻番号。取得できない場合は <see langword="null"/>。</param>
	public BindingQueueItem(MaterialVolumeNode node, decimal? volumeNumber)
	{
		this.SourceNode = node;
		this.VolumeNumber = new BindableReactiveProperty<decimal?>(volumeNumber);
		this.DisplayName = node.Name.Value;
		this.FileCount = node.FileCount;
		this.FileCountText = node.NodeType == MaterialVolumeNodeType.Epub ? "-" : node.FileCount.ToString();
		this.SourceTypeText = node.NodeType switch
		{
			MaterialVolumeNodeType.Epub => "Epub",
			MaterialVolumeNodeType.Folder when node.ArchiveEntryPrefix is not null => "Archive Folder",
			_ => "Folder",
		};
	}
}
