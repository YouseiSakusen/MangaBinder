using ObservableCollections;
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
	/// この巻用の製本作業フォルダのフルパスを取得または設定します。
	/// SeriesInspectionManager が製本前確認処理で確定した値を設定します。
	/// DB へ永続化される情報ではなく、現在のセッション中の一時的な管理情報です。
	/// </summary>
	public string? WorkFolderPath { get; set; }

	/// <summary>
	/// この巻の製本処理で使用するWork上の画像一覧を取得します。
	/// WorkVolumeBuilder が実体化した画像を追加します。
	/// </summary>
	public ObservableList<BindingImage> Images { get; }

	/// <summary>
	/// <see cref="BindingVolume"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="material">この巻の元となった素材。BindingStore.Materials 内に存在するインスタンスを指定してください。</param>
	public BindingVolume(MaterialItem material)
	{
		this.Material = material ?? throw new ArgumentNullException(nameof(material));
		this.VolumeNumber = new BindableReactiveProperty<decimal?>(null)
			.AddTo(ref this.disposableBag);
		this.WorkFolderPath = null;
		this.Images = new ObservableList<BindingImage>();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Images 内の各 BindingImage を破棄する（TemporaryImageStream等の安全網）
		foreach (var image in this.Images)
		{
			image.Dispose();
		}
		this.Images.Clear();

		// VolumeNumber の ReactiveProperty を破棄する
		// Material は BindingStore.Materials が所有しているため Dispose しない
		this.disposableBag.Dispose();
	}
}
