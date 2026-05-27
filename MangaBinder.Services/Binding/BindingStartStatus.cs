namespace MangaBinder.Binding;

/// <summary>
/// 製本開始キューの進行状態を表す列挙型です。
/// </summary>
public enum BindingStartStatus
{
	/// <summary>未設定。</summary>
	None = 0,

	/// <summary>設定中。</summary>
	Configuring = 1,

	/// <summary>実行待ち。</summary>
	Ready = 2,

	/// <summary>処理中。</summary>
	Processing = 3,

	/// <summary>エラー。</summary>
	Error = 4,
}
