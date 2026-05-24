using Wpf.Ui.Controls;

namespace HalationGhost.Wpf.Ui.Navigation;

/// <summary>
/// 保存操作の結果を表すインターフェースです。
/// </summary>
public interface ISaveResult
{
	/// <summary>保存が成功したかどうかを取得します。</summary>
	bool IsSuccess { get; init; }

	/// <summary>結果に付随するメッセージを取得します。</summary>
	string Message { get; init; }

	/// <summary>結果の外観を取得します。スナックバーの色等に使用します。</summary>
	ControlAppearance Appearance { get; init; }
}
