namespace MangaBinder.Binding.Prepress;

/// <summary>
/// 見開き分割設定を保持する POCO です。
/// </summary>
public class SpreadSplitInformation
{
	/// <summary>左トリム量（ピクセル）を取得または設定します。</summary>
	public int TrimLeft { get; set; }

	/// <summary>右トリム量（ピクセル）を取得または設定します。</summary>
	public int TrimRight { get; set; }

	/// <summary>上トリム量（ピクセル）を取得または設定します。</summary>
	public int TrimTop { get; set; }

	/// <summary>下トリム量（ピクセル）を取得または設定します。</summary>
	public int TrimBottom { get; set; }

	/// <summary>分割位置（画像幅に対するパーセンテージ 0〜100）を取得または設定します。</summary>
	public int SplitPosition { get; set; } = 50;

	/// <summary>このページのみ個別設定を使用するかどうかを取得または設定します。</summary>
	public bool UsePageOverride { get; set; }

	/// <summary>
	/// 現在の設定値を複製して返します。
	/// </summary>
	/// <returns>複製された <see cref="SpreadSplitInformation"/>。</returns>
	public SpreadSplitInformation Clone()
		=> new()
		{
			TrimLeft = this.TrimLeft,
			TrimRight = this.TrimRight,
			TrimTop = this.TrimTop,
			TrimBottom = this.TrimBottom,
			SplitPosition = this.SplitPosition,
			UsePageOverride = this.UsePageOverride,
		};

	/// <summary>
	/// 指定した設定値をこのインスタンスへ上書きコピーします。
	/// </summary>
	/// <param name="source">コピー元。</param>
	public void CopyFrom(SpreadSplitInformation source)
	{
		this.TrimLeft = source.TrimLeft;
		this.TrimRight = source.TrimRight;
		this.TrimTop = source.TrimTop;
		this.TrimBottom = source.TrimBottom;
		this.SplitPosition = source.SplitPosition;
		this.UsePageOverride = source.UsePageOverride;
	}
}
