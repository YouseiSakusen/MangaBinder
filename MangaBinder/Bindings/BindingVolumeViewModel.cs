using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// BindingStore.BindingVolumes の BindingVolume を巻選択画面右側の選択巻一覧へ表示するための UI 用 ViewModel です。
/// BindingVolume や MaterialItem をコピーせず、常に同じインスタンスを参照します。
/// </summary>
public class BindingVolumeViewModel
{
	/// <summary>
	/// この ViewModel の元となった BindingVolume を取得します。
	/// BindingStore.BindingVolumes 内に存在する BindingVolume と同一インスタンスを参照します。
	/// </summary>
	public BindingVolume BindingVolume { get; init; }

	/// <summary>
	/// この BindingVolume の元となった素材を取得します。
	/// BindingVolume.Material をそのまま参照します。
	/// </summary>
	public MaterialItem Material => this.BindingVolume.Material;

	/// <summary>
	/// この巻に割り当てられた巻番号を取得または設定します。
	/// BindingVolume.VolumeNumber と同じインスタンスをそのまま公開し、
	/// XAML バインディングから編集された値が正本へ直接反映されるようにします。
	/// </summary>
	public BindableReactiveProperty<decimal?> VolumeNumber
		=> this.BindingVolume.VolumeNumber;

	/// <summary>
	/// 右側一覧で表示する素材名を取得します。
	/// </summary>
	public string Name => this.Material.Name;

	/// <summary>
	/// 対象フォルダ配下の画像ファイル数を取得します。
	/// </summary>
	public int FileCount => this.Material.FileCount;

	/// <summary>
	/// ファイル数の表示文字列を取得します。EPUB の場合は "-"、それ以外は "{ファイル数} 件"。
	/// </summary>
	public string FileCountText
	{
		get
		{
			if (this.Material.ItemType == MaterialItemType.Epub)
			{
				return "-";
			}

			return $"{this.Material.FileCount} 件";
		}
	}

	/// <summary>
	/// 選択巻の「素材由来」を表すアイコン表示用の MaterialItemType を取得します。
	/// Archive 内部エントリ、EPUB、実フォルダの区別を正しく判定します。
	/// </summary>
	public MaterialItemType DisplayItemType
	{
		get
		{
			// Archive 内部エントリ（ArchiveEntryPrefix が null でない）
			if (!string.IsNullOrEmpty(this.Material.ArchiveEntryPrefix))
			{
				return MaterialItemType.Archive;
			}

			// EPUB ファイル
			if (this.Material.ItemType == MaterialItemType.Epub)
			{
				return MaterialItemType.Epub;
			}

			// それ以外は実フォルダ
			return MaterialItemType.Folder;
		}
	}

	/// <summary>
	/// <see cref="BindingVolumeViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingVolume">表示対象の BindingVolume。BindingStore.BindingVolumes 内に存在するインスタンスを指定してください。</param>
	public BindingVolumeViewModel(BindingVolume bindingVolume)
	{
		this.BindingVolume = bindingVolume ?? throw new ArgumentNullException(nameof(bindingVolume));
	}
}
