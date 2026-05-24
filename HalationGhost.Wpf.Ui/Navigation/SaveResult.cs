using Wpf.Ui.Controls;

namespace HalationGhost.Wpf.Ui.Navigation;

/// <summary>
/// 保存操作の結果を表す不変レコードです。
/// </summary>
public record SaveResult : ISaveResult
{
	/// <summary>保存が成功したかどうかを取得します。</summary>
	public bool IsSuccess { get; init; }

	/// <summary>結果に付随するメッセージを取得します。</summary>
	public string Message { get; init; } = string.Empty;

	/// <summary>結果の外観を取得します。スナックバーの色等に使用します。</summary>
	public ControlAppearance Appearance { get; init; }

	/// <summary>
	/// 保存が成功したことを表す <see cref="SaveResult"/> を生成します。
	/// </summary>
	/// <param name="message">付随するメッセージ。省略可。</param>
	/// <returns>成功を表す <see cref="SaveResult"/>。</returns>
	public static SaveResult Success(string message = "") =>
		new() { IsSuccess = true, Appearance = ControlAppearance.Success, Message = message };

	/// <summary>
	/// 警告を伴う保存結果を表す <see cref="SaveResult"/> を生成します。
	/// </summary>
	/// <param name="message">警告の内容を示すメッセージ。</param>
	/// <returns>警告を表す <see cref="SaveResult"/>。</returns>
	public static SaveResult Warning(string message) =>
		new() { IsSuccess = true, Appearance = ControlAppearance.Caution, Message = message };

	/// <summary>
	/// 保存が失敗したことを表す <see cref="SaveResult"/> を生成します。
	/// </summary>
	/// <param name="message">失敗の原因を示すメッセージ。</param>
	/// <returns>失敗を表す <see cref="SaveResult"/>。</returns>
	public static SaveResult Failure(string message) =>
		new() { IsSuccess = false, Appearance = ControlAppearance.Danger, Message = message };
}
