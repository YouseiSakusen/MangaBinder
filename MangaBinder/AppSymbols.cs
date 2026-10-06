using Wpf.Ui.Controls;

namespace MangaBinder;

/// <summary>
/// MangaBinder 全体で使用するアプリケーション固有の Symbol を定義します。
/// </summary>
public static class AppSymbols
{
	/// <summary>
	/// 素材フォルダを開く操作に使用する共通 Symbol です。
	/// </summary>
	public static readonly SymbolRegular OpenMaterialFolder = SymbolRegular.FolderOpen24;

	/// <summary>
	/// 展開先フォルダを開く操作に使用する共通 Symbol です。
	/// </summary>
	public static readonly SymbolRegular OpenBindingFolder = SymbolRegular.TabDesktopImage24;
}
