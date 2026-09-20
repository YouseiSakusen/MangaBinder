namespace MangaBinder.Bindings;

/// <summary>
/// 製本完了処理の結果を保持するクラスです。
/// </summary>
public class BindingCompletionResult
{
	/// <summary>
	/// <see cref="BindingCompletionResult"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingSeries">今回製本した BindingStore.BindingTarget.Value の同一インスタンス。</param>
	/// <param name="outputFilePath">今回正常に作成されたZIPファイルのフルパス。</param>
	public BindingCompletionResult(BindingSeries bindingSeries, string outputFilePath)
	{
		this.BindingSeries = bindingSeries;
		this.OutputFilePath = outputFilePath;
	}

	/// <summary>
	/// 今回製本した BindingStore.BindingTarget.Value の同一インスタンスを取得します。
	/// </summary>
	public BindingSeries BindingSeries { get; }

	/// <summary>
	/// 今回正常に作成されたZIPファイルのフルパスを取得します。
	/// </summary>
	public string OutputFilePath { get; }
}
