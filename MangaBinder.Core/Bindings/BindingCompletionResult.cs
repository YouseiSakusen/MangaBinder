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
	/// <param name="outputFilePath">
	/// 今回作成されたZIPファイルのフルパス。
	/// CreateZip が false の場合は string.Empty。
	/// </param>
	/// <param name="openFolderPath">
	/// Explorerで開く対象フォルダのフルパス。
	/// CreateZip が true の場合は ZIPを作成したフォルダ、
	/// CreateZip が false の場合は Work 作品フォルダ。
	/// </param>
	/// <param name="selectFilePath">
	/// Explorerで選択するファイルのフルパス（/select オプション用）。
	/// CreateZip が true の場合は ZIPファイルのパス、
	/// CreateZip が false の場合は string.Empty。
	/// </param>
	public BindingCompletionResult(
		BindingSeries bindingSeries,
		string outputFilePath,
		string openFolderPath,
		string selectFilePath)
	{
		this.BindingSeries = bindingSeries;
		this.OutputFilePath = outputFilePath;
		this.OpenFolderPath = openFolderPath;
		this.SelectFilePath = selectFilePath;
	}

	/// <summary>
	/// 今回製本した BindingStore.BindingTarget.Value の同一インスタンスを取得します。
	/// </summary>
	public BindingSeries BindingSeries { get; }

	/// <summary>
	/// 今回作成されたZIPファイルのフルパスを取得します。
	/// CreateZip が false の場合は string.Empty です。
	/// </summary>
	public string OutputFilePath { get; }

	/// <summary>
	/// Explorerで開く対象フォルダのフルパスを取得します。
	/// CreateZip が true の場合は ZIPを作成したフォルダ、
	/// CreateZip が false の場合は Work 作品フォルダです。
	/// </summary>
	public string OpenFolderPath { get; }

	/// <summary>
	/// Explorerで選択するファイルのフルパスを取得します（/select オプション用）。
	/// CreateZip が true の場合は ZIPファイルのパス、
	/// CreateZip が false の場合は string.Empty です。
	/// </summary>
	public string SelectFilePath { get; }
}
