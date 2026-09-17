namespace MangaBinder.Bindings;

/// <summary>
/// 巻フォルダ名の桁数選択オプションを表します。
/// </summary>
public class VolumeFolderDigitOption
{
	/// <summary>
	/// 桁数を取得します（1, 2, 3 など）。
	/// </summary>
	public int Digits { get; }

	/// <summary>
	/// 表示用ラベルを取得します（例："2桁"）。
	/// </summary>
	public string Label { get; }

	/// <summary>
	/// 例示テキストを取得します（例："（例：01巻）"）。
	/// </summary>
	public string ExampleText { get; }

	/// <summary>
	/// <see cref="VolumeFolderDigitOption"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="digits">桁数。</param>
	/// <param name="label">表示用ラベル。</param>
	/// <param name="exampleText">例示テキスト。</param>
	public VolumeFolderDigitOption(int digits, string label, string exampleText)
	{
		this.Digits = digits;
		this.Label = label;
		this.ExampleText = exampleText;
	}
}
