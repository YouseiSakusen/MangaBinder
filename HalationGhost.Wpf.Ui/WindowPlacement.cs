using System.Windows;

namespace HalationGhost.Wpf.Ui;

/// <summary>
/// ウィンドウの位置・サイズ・状態を保持するデータクラス。
/// </summary>
internal sealed class WindowPlacement
{
	/// <summary>
	/// ウィンドウの左端の座標。
	/// </summary>
	public double Left { get; set; }

	/// <summary>
	/// ウィンドウの上端の座標。
	/// </summary>
	public double Top { get; set; }

	/// <summary>
	/// ウィンドウの幅。
	/// </summary>
	public double Width { get; set; }

	/// <summary>
	/// ウィンドウの高さ。
	/// </summary>
	public double Height { get; set; }

	/// <summary>
	/// ウィンドウの状態（Normal, Maximized, Minimized）。
	/// </summary>
	public WindowState WindowState { get; set; }
}
