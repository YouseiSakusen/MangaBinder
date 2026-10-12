namespace MangaBinder.Bindings;

/// <summary>
/// 製本履歴を表すモデルです。
/// </summary>
public class BindingHistory
{
	/// <summary>履歴 ID です。</summary>
	public long HistoryId { get; set; }

	/// <summary>対象作品の SeriesId です。</summary>
	public long SeriesId { get; set; }

	/// <summary>製本完了日時（ローカル時刻）です。</summary>
	public DateTime BoundAt { get; set; }

	/// <summary>製本した巻番号の文字列表現です。（例：01-03,05）</summary>
	public string VolumeRange { get; set; } = string.Empty;

	/// <summary>出力 ZIP ファイル名です。ZIP を作成していない場合は空文字列です。</summary>
	public string OutputFileName { get; set; } = string.Empty;

	/// <summary>出力先フォルダのフルパスです。</summary>
	public string OutputFolderPath { get; set; } = string.Empty;
}
