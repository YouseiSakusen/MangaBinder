namespace MangaBinder;

/// <summary>
/// 画面遷移時に遷移元ViewModel へ渡す要求を表すレコード型です。
/// </summary>
/// <remarks>
/// 遷移先ViewModel が、遷移元ViewModel の一時状態を保持すべきかどうかを
/// 遷移元へ要求するための情報を保持します。
/// </remarks>
public sealed record NavigationLeavingRequest
{
	/// <summary>
	/// 遷移元の一時状態を保持すべきかどうかを示す値を取得します。
	/// </summary>
	public bool PreserveState { get; init; }

	/// <summary>
	/// 何も要求しない既定値を取得します。
	/// </summary>
	public static NavigationLeavingRequest None { get; } = new();
}
