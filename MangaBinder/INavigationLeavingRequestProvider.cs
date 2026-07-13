namespace MangaBinder;

/// <summary>
/// 画面へ遷移する際に、遷移元ViewModel へ要求を返す ViewModel を識別するためのインターフェースです。
/// </summary>
/// <remarks>
/// 遷移先 ViewModel が、自身へ遷移してくる際に遷移元の退場処理へ渡すべき要求を返します。
/// 例えば、Home 画面へ戻る場合に、遷移元の一時状態を保持するよう要求することができます。
/// </remarks>
public interface INavigationLeavingRequestProvider
{
	/// <summary>
	/// 遷移元 ViewModel へ渡す要求を取得します。
	/// </summary>
	/// <returns>遷移元が従うべき要求を表す <see cref="NavigationLeavingRequest"/>。</returns>
	NavigationLeavingRequest GetNavigationLeavingRequest();
}
