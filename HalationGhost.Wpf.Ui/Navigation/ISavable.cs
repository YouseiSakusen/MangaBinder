namespace HalationGhost.Wpf.Ui.Navigation;

/// <summary>
/// 保存操作を非同期で実行できることを示すインターフェースです。
/// </summary>
public interface ISavable
{
	/// <summary>
	/// データを非同期で保存します。
	/// </summary>
	/// <returns>保存操作の結果を表す <see cref="ISaveResult"/>。</returns>
	ValueTask<ISaveResult> SaveAsync();
}
