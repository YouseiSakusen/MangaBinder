namespace MangaBinder.Bindings;

/// <summary>
/// 製本完了処理を開始する前の事前確認情報を保持するクラスです。
/// </summary>
public class BindingStartableStatus
{
	private const long SizeWarningThresholdBytes = 2_500_000_000L; // 2.5 GB

	/// <summary>
	/// <see cref="BindingStartableStatus"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="outputFilePath">今回作成予定のZIPファイルのフルパス。出力先を決定できない場合は null。</param>
	/// <param name="outputFileExists">OutputFilePath と同名のファイルが既に存在するか。</param>
	/// <param name="totalSizeBytes">今回製本対象となるWorkフォルダ内の全ファイル合計サイズ（バイト）。</param>
	public BindingStartableStatus(string? outputFilePath, bool outputFileExists, long totalSizeBytes)
	{
		this.OutputFilePath = outputFilePath;
		this.OutputFileExists = outputFileExists;
		this.TotalSizeBytes = totalSizeBytes;
	}

	/// <summary>
	/// 今回作成予定のZIPファイルのフルパスを取得します。
	/// 出力先を決定できない場合は null です。
	/// </summary>
	public string? OutputFilePath { get; }

	/// <summary>
	/// OutputFilePath と同名のファイルが既に存在するかを取得します。
	/// </summary>
	public bool OutputFileExists { get; }

	/// <summary>
	/// 今回製本対象となるWorkフォルダ内の全ファイル合計サイズ（バイト）を取得します。
	/// </summary>
	public long TotalSizeBytes { get; }

	/// <summary>
	/// TotalSizeBytes が 2.5GB を超えている場合 true を取得します。
	/// </summary>
	public bool IsSizeOverWarningThreshold => this.TotalSizeBytes > SizeWarningThresholdBytes;

	/// <summary>
	/// OutputFileExists または IsSizeOverWarningThreshold のどちらかが true の場合 true を取得します。
	/// ユーザーに確認を取る必要があるかを判定するために使用します。
	/// </summary>
	public bool RequiresConfirmation => this.OutputFileExists || this.IsSizeOverWarningThreshold;
}
