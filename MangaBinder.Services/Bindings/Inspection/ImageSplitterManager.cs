namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// ImageSplitter の編集状態の初期化と終了を担当するマネージャーです。
/// 実画像の分割・トリミング処理は担当しません。
/// </summary>
public class ImageSplitterManager
{
	private readonly BindingStore bindingStore;

	/// <summary>
	/// <see cref="ImageSplitterManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	public ImageSplitterManager(BindingStore bindingStore)
	{
		this.bindingStore = bindingStore;
	}

	/// <summary>
	/// 現在の SplitTargetVolume から編集用 Clone を作成して SplitVolumes へ追加し、
	/// SplitTargetVolume を Clone へ切り替えます。
	/// </summary>
	/// <exception cref="InvalidOperationException">SplitTargetVolume が null の場合。</exception>
	public void Initialize()
	{
		var sourceVolume = this.bindingStore.SplitTargetVolume.Value
			?? throw new InvalidOperationException("BindingStore.SplitTargetVolume が null です。");

		var cloneVolume = sourceVolume.CloneForImageSplitter();
		this.bindingStore.SplitVolumes.Add(cloneVolume);
		this.bindingStore.SplitTargetVolume.Value = cloneVolume;
	}

	/// <summary>
	/// ImageSplitter の編集状態を終了します。
	/// SplitTargetVolume を null にし、SplitVolumes の破棄は BindingStore の購読に委ねます。
	/// </summary>
	public void Finish()
	{
		this.bindingStore.SplitTargetVolume.Value = null;
	}
}
