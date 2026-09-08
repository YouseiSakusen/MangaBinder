using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程で選択された1巻を表すエンティティです。
/// 素材ツリー上の MaterialItem が、製本対象の1巻として採用された状態を表します。
/// </summary>
public class BindingVolume : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// この巻の元となった素材を取得します。
	/// BindingStore.Materials 内に存在する MaterialItem と同一インスタンスを参照します。
	/// </summary>
	public MaterialItem Material { get; init; }

	/// <summary>
	/// この巻に割り当てられた巻番号を取得または設定します。
	/// 初期値は null で、後続の処理で VolumeNumberHelper 等によって設定されます。
	/// </summary>
	public BindableReactiveProperty<decimal?> VolumeNumber { get; }

	/// <summary>
	/// <see cref="BindingVolume"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="material">この巻の元となった素材。BindingStore.Materials 内に存在するインスタンスを指定してください。</param>
	public BindingVolume(MaterialItem material)
	{
		this.Material = material ?? throw new ArgumentNullException(nameof(material));
		this.VolumeNumber = new BindableReactiveProperty<decimal?>(null)
			.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// VolumeNumber のみ Dispose する
		// Material は BindingStore.Materials が所有しているため Dispose しない
		this.disposableBag.Dispose();
	}
}
