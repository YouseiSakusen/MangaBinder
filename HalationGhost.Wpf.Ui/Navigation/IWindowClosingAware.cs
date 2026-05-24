namespace HalationGhost.Wpf.Ui.Navigation;

/// <summary>
/// ウィンドウのクローズ要求を検知して非同期処理を実行できることを示すインターフェースです。
/// </summary>
public interface IWindowClosingAware
{
	/// <summary>
	/// ウィンドウが閉じられる直前に非同期で実行されます。
	/// </summary>
	/// <returns>完了を表す <see cref="ValueTask"/>。</returns>
	ValueTask OnClosingAsync();
}
