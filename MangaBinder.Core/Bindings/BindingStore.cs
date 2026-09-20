using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程を画面間で共有する正本状態を保持する Store です。
/// </summary>
public class BindingStore : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>
	/// 現在の製本工程で扱っている BindingSeries を取得または設定します。
	/// </summary>
	public BindableReactiveProperty<BindingSeries?> BindingTarget { get; }

	/// <summary>
	/// 巻選択工程で扱う素材ツリーの正本を取得します。
	/// </summary>
	public ObservableList<MaterialItem> Materials { get; }

	/// <summary>
	/// 巻選択工程で選択された巻一覧の正本を取得します。
	/// BindingVolumes 内の各 BindingVolume は、Materials 内の MaterialItem と同一インスタンスを参照します。
	/// </summary>
	public ObservableList<BindingVolume> BindingVolumes { get; }

	/// <summary>
	/// 巻選択工程中に、ユーザーが選択巻一覧を手動並び替えしたかどうかを示します。
	/// true の場合、SelectMaterial() は新規選択巻を常に末尾に追加します。
	/// false の場合、巻番号の昇順（null末尾）で自動挿入されます。
	/// </summary>
	public BindableReactiveProperty<bool> IsManualVolumeOrder { get; }

	/// <summary>
	/// 製本用Work作品フォルダが既に存在する場合に、その既存フォルダを削除して新しく作り直すかどうかを示します。
	/// </summary>
	public BindableReactiveProperty<bool> RecreateWorkFolder { get; }

	/// <summary>
	/// 製本用Workフォルダ内に作成する巻フォルダ名の巻番号桁数を取得します。
	/// </summary>
	public BindableReactiveProperty<int> VolumeFolderDigits { get; }

	/// <summary>
	/// 製本完了時に使用する出力 ZIP ファイル名を取得します。
	/// </summary>
	public BindableReactiveProperty<string> ZipOutputFileName { get; }

	/// <summary>
	/// 製本完了後に対象作品を製本待ちから削除するかどうかを取得または設定します。
	/// </summary>
	public BindableReactiveProperty<bool> RemoveFromBindingQueueAfterCompletion { get; }

	/// <summary>
	/// <see cref="BindingStore"/> の新しいインスタンスを初期化します。
	/// </summary>
	public BindingStore()
	{
		this.BindingTarget = new BindableReactiveProperty<BindingSeries?>(null)
			.AddTo(ref this.disposableBag);
		this.Materials = new ObservableList<MaterialItem>();
		this.BindingVolumes = new ObservableList<BindingVolume>();
		this.IsManualVolumeOrder = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.RecreateWorkFolder = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
		this.VolumeFolderDigits = new BindableReactiveProperty<int>(2)
			.AddTo(ref this.disposableBag);
		this.ZipOutputFileName = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);
		this.RemoveFromBindingQueueAfterCompletion = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// 所有関係に従って破棄する
		// BindingVolumes が Materials を参照しているため、先に BindingVolumes を破棄
		foreach (var volume in this.BindingVolumes)
		{
			volume.Dispose();
		}
		this.BindingVolumes.Clear();

		// 次に Materials を破棄
		foreach (var material in this.Materials)
		{
			material.Dispose();
		}
		this.Materials.Clear();

		// 最後に ReactiveProperty を破棄
		this.disposableBag.Dispose();
	}
}
