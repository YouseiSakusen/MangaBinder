namespace HalationGhost.Wpf.Ui.Navigation;

/// <summary>
/// ナビゲーションでライフサイクルが終了する ViewModel を示すマーカーインターフェースです。
/// </summary>
public interface INavigationDisposable
{
	/// <summary>
	/// ナビゲーション時にリソースを解放します。
	/// </summary>
	void Dispose();
}
