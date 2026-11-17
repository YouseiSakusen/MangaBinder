using MangaBinder.Bindings;

namespace MangaBinder.Series;

/// <summary>
/// 素材一覧の並び順を決める比較クラスです。
/// フォルダ → ファイル（Archive / Epub）→ その他の順で分類し、同じ分類内では名前の昇順（Ordinal）にします。
/// </summary>
public sealed class MaterialFileItemComparer : IComparer<MaterialFileItem>
{
	/// <summary>共有して使用する比較インスタンスを取得します。</summary>
	public static MaterialFileItemComparer Default { get; } = new();

	/// <inheritdoc/>
	public int Compare(MaterialFileItem? x, MaterialFileItem? y)
	{
		ArgumentNullException.ThrowIfNull(x);
		ArgumentNullException.ThrowIfNull(y);

		var categoryResult = getCategory(x.ItemType).CompareTo(getCategory(y.ItemType));
		return categoryResult != 0
			? categoryResult
			: StringComparer.Ordinal.Compare(x.Name, y.Name);
	}

	/// <summary>
	/// 素材の種別から、並び順の分類番号を取得します。
	/// </summary>
	/// <param name="itemType">素材の種別。</param>
	/// <returns>分類番号。小さいほど先に並びます。</returns>
	private static int getCategory(MaterialItemType itemType)
		=> itemType switch
		{
			MaterialItemType.Folder => 0,
			MaterialItemType.Archive => 1,
			MaterialItemType.Epub => 1,
			_ => 2,
		};
}
