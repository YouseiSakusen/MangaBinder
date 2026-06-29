namespace MangaBinder;

/// <summary>
/// Home 画面の Empty State の種別を表す列挙型です。
/// </summary>
public enum HomeEmptyStateKind
{
	/// <summary>Empty State なし（作品あり、または判定不能）。</summary>
	None = 0,

	/// <summary>素材フォルダが未登録のため作品がない。</summary>
	MaterialFolderNotRegistered = 1,

	/// <summary>素材フォルダスキャン完了済みだが作品が見つからなかった。</summary>
	MaterialFolderScanCompletedButNoSeries = 2,
}
