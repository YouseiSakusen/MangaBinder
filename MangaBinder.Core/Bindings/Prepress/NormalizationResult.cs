namespace MangaBinder.Bindings.Prepress;

/// <summary>
/// ファイル名桁揃えリネームの結果を表します。
/// </summary>
/// <param name="OriginalPath">元のファイルパス。</param>
/// <param name="NewPath">リネーム後のファイルパス。元と同じ場合は変更なし。</param>
/// <param name="HasError">このファイルのリネームにエラーがあるかどうか。</param>
/// <param name="ErrorMessage">エラーメッセージ。エラーがない場合は <see langword="null"/>。</param>
public sealed record NormalizationResult(
	string OriginalPath,
	string NewPath,
	bool HasError,
	string? ErrorMessage)
{
	/// <summary>リネームが必要かどうかを返します。</summary>
	public bool RequiresRename => !HasError && OriginalPath != NewPath;
}
