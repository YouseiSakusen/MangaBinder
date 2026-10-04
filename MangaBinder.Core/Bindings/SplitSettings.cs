using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 巻共通の見開き分割設定を表す編集状態です。
/// ImageSplitter の View から直接 Binding されます。
/// </summary>
public class SplitSettings : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>見開き分割位置（px）を取得します。</summary>
	public BindableReactiveProperty<int> SplitPosition { get; }

	/// <summary>左トリミング量（px）を取得します。</summary>
	public BindableReactiveProperty<int> TrimLeft { get; }

	/// <summary>上トリミング量（px）を取得します。</summary>
	public BindableReactiveProperty<int> TrimTop { get; }

	/// <summary>右トリミング量（px）を取得します。</summary>
	public BindableReactiveProperty<int> TrimRight { get; }

	/// <summary>下トリミング量（px）を取得します。</summary>
	public BindableReactiveProperty<int> TrimBottom { get; }

	/// <summary>
	/// <see cref="SplitSettings"/> の新しいインスタンスを初期値 0 で初期化します。
	/// </summary>
	public SplitSettings()
	{
		this.SplitPosition = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.TrimLeft = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.TrimTop = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.TrimRight = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
		this.TrimBottom = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// ImageSplitter の編集用として、現在値を持つ独立した SplitSettings を作成します。
	/// ReactiveProperty は共有せず、各 Value のみをコピーします。
	/// </summary>
	/// <returns>独立した新しい SplitSettings。</returns>
	public SplitSettings CloneForImageSplitter()
	{
		var clone = new SplitSettings();
		clone.SplitPosition.Value = this.SplitPosition.Value;
		clone.TrimLeft.Value = this.TrimLeft.Value;
		clone.TrimTop.Value = this.TrimTop.Value;
		clone.TrimRight.Value = this.TrimRight.Value;
		clone.TrimBottom.Value = this.TrimBottom.Value;
		return clone;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
