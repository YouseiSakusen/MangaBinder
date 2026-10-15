namespace MangaBinder.Bindings;

/// <summary>
/// 製本設定として保存する見開き分割設定のDTOです。
/// </summary>
public class SplitSettingsDto
{
	/// <summary>分割位置のずれ（px、右が正）を取得または設定します。</summary>
	public int SplitOffset { get; set; }

	/// <summary>見開き分割後のページ順を取得または設定します。</summary>
	public SpreadPageOrder PageOrder { get; set; }

	/// <summary>左トリミング量（px）を取得または設定します。</summary>
	public int TrimLeft { get; set; }

	/// <summary>上トリミング量（px）を取得または設定します。</summary>
	public int TrimTop { get; set; }

	/// <summary>右トリミング量（px）を取得または設定します。</summary>
	public int TrimRight { get; set; }

	/// <summary>下トリミング量（px）を取得または設定します。</summary>
	public int TrimBottom { get; set; }
}
