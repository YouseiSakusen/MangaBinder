namespace HalationGhost.Wpf.Ui;

/// <summary>
/// 画面から離れてよいかどうかを確認できることを表すインターフェースです。
/// </summary>
public interface ILeavable
{
	/// <summary>
	/// 画面から離れてよいかどうかを非同期で確認します。
	/// </summary>
	/// <returns>離れてよい場合は true、画面に留まる場合は false。</returns>
	ValueTask<bool> CanLeaveAsync();
}
