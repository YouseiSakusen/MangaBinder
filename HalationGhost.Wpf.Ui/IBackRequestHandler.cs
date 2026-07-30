namespace HalationGhost.Wpf.Ui;

/// <summary>
/// マウスサイドボタンなどの入力デバイスからの「戻る」要求を処理するためのインターフェースです。
/// このインターフェースは入力デバイスを表すものではなく、「戻る要求」を通知するための契約を定義します。
/// </summary>
public interface IBackRequestHandler
{
	/// <summary>
	/// 戻る要求が発生したときに呼ばれます。
	/// </summary>
	/// <returns>戻る処理を実行する非同期タスク。</returns>
	ValueTask OnBackRequestedAsync();
}
