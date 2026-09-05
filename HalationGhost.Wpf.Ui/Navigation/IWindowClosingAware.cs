using System.ComponentModel;

namespace HalationGhost.Wpf.Ui.Navigation;

/// <summary>
/// ウィンドウのクローズ要求を検知して非同期処理を実行できることを示すインターフェースです。
/// </summary>
public interface IWindowClosingAware
{
	/// <summary>
	/// ウィンドウが閉じられる直前に非同期で実行されます。
	/// <see cref="CancelEventArgs"/> の <see cref="CancelEventArgs.Cancel"/> を <c>true</c> に設定することで、
	/// ウィンドウのクローズをキャンセルできます。
	/// </summary>
	/// <param name="e">クローズイベントの引数。Cancel を設定してクローズをキャンセル可能。</param>
	/// <returns>完了を表す <see cref="ValueTask"/>。</returns>
	ValueTask OnClosingAsync(CancelEventArgs e);
}
