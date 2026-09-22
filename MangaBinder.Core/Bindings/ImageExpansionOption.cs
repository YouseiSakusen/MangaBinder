namespace MangaBinder.Bindings;

/// <summary>
/// 素材展開方法の選択オプションを表します。
/// </summary>
public class ImageExpansionOption
{
	/// <summary>
	/// 展開方法を取得します。
	/// </summary>
	public ImageExpansionMethod Method { get; }

	/// <summary>
	/// 表示用ラベルを取得します。
	/// </summary>
	public string Label { get; }

	/// <summary>
	/// <see cref="ImageExpansionOption"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="method">展開方法。</param>
	/// <param name="label">表示用ラベル。</param>
	public ImageExpansionOption(ImageExpansionMethod method, string label)
	{
		this.Method = method;
		this.Label = label;
	}
}
