namespace MangaBinder;

/// <summary>
/// 画面から離れる際に後処理を行う ViewModel を識別するためのインターフェースです。
/// </summary>
/// <remarks>
/// ナビゲーション時に一時的な状態をクリアしたり、リソースを破棄したりする場合に実装します。
/// 入場処理 (<see cref="IDataInitializable"/>) と責務を分離します。
/// </remarks>
public interface INavigationLeavingAware
{
	/// <summary>
	/// 画面から離れる際の後処理を非同期で実行します。
	/// </summary>
	/// <param name="request">遷移先から受け取った要求。</param>
	/// <returns>非同期操作を表す <see cref="ValueTask"/>。</returns>
	ValueTask OnNavigatingFromAsync(NavigationLeavingRequest request);
}
