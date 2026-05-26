namespace MangaBinder.Binding.Prepress;

/// <summary>
/// 巻内の1ファイルを表す Prepress 用アイテムです。
/// </summary>
public class PrepressImageItem
{
	/// <summary>ファイルのフルパスを取得または設定します。</summary>
	public string FilePath { get; set; } = string.Empty;

	/// <summary>ファイル名を取得または設定します。</summary>
	public string FileName { get; set; } = string.Empty;

	/// <summary>分割対象かどうかを取得または設定します。</summary>
	public bool IsSplitTarget { get; set; }

	/// <summary>対応外ファイル形式かどうかを取得または設定します。</summary>
	public bool IsUnsupported { get; set; }

	/// <summary>読み込みエラーが発生しているかどうかを取得または設定します。</summary>
	public bool HasError { get; set; }

	/// <summary>見開き分割設定を取得または設定します。</summary>
	public SpreadSplitInformation SpreadSplitInformation { get; set; } = new();
}
