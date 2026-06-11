namespace MangaBinder.Bindings.Prepress;

/// <summary>
/// 見開き分割後のページ順を示す列挙体です。
/// </summary>
public enum SpreadPageOrder
{
	/// <summary>右→左（日本漫画）。</summary>
	RightToLeft = 0,

	/// <summary>左→右（左開き・洋書）。</summary>
	LeftToRight = 1,
}
