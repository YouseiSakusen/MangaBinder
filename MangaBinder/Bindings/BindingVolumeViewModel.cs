using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// BindingStore.BindingVolumes の BindingVolume を巻選択画面右側の選択巻一覧へ表示するための UI 用 ViewModel です。
/// BindingVolume や MaterialItem をコピーせず、常に同じインスタンスを参照します。
/// 表示項目は BindingVolume.CurrentMaterial（Reactive な現在の入力素材）から導出し、Material の切り替えへ自動追従します。
/// </summary>
public class BindingVolumeViewModel : IDisposable
{
	private DisposableBag disposableBag;

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
	/// 右側一覧で表示する素材名を取得します。現在の Material に追従します。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> Name { get; }

	/// <summary>
	/// 対象フォルダ配下の画像ファイル数を取得します。現在の Material に追従します。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<int> FileCount { get; }

	/// <summary>
	/// ファイル数の表示文字列を取得します。EPUB の場合は "-"、それ以外は "{ファイル数} 件"。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> FileCountText { get; }

	/// <summary>
	/// 選択巻の「素材由来」を表すアイコン表示用の MaterialItemType を取得します。
	/// Archive 内部エントリ、EPUB、実フォルダの区別を正しく判定します。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<MaterialItemType> DisplayItemType { get; }

	/// <summary>
	/// 現在の Material が展開済み巻由来（MaterialSourceType.WorkFolder）かどうかを取得します。
	/// アイコン表示と、巻番号の編集可否の判定に使用します。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> IsExpandedVolume { get; }

	/// <summary>
	/// 巻番号を編集できるかどうかを取得します。展開済み巻由来の場合は false です。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> IsVolumeNumberEditable { get; }

	/// <summary>
	/// <see cref="BindingVolumeViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingVolume">表示対象の BindingVolume。BindingStore.BindingVolumes 内に存在するインスタンスを指定してください。</param>
	public BindingVolumeViewModel(BindingVolume bindingVolume)
	{
		this.BindingVolume = bindingVolume ?? throw new ArgumentNullException(nameof(bindingVolume));

		var currentMaterial = bindingVolume.CurrentMaterial.AsObservable();
		var initial = bindingVolume.Material;

		this.Name = currentMaterial
			.Select(material => material.Name)
			.ToReadOnlyBindableReactiveProperty(initial.Name)
			.AddTo(ref this.disposableBag);

		this.FileCount = currentMaterial
			.Select(material => material.FileCount)
			.ToReadOnlyBindableReactiveProperty(initial.FileCount)
			.AddTo(ref this.disposableBag);

		this.FileCountText = currentMaterial
			.Select(createFileCountText)
			.ToReadOnlyBindableReactiveProperty(createFileCountText(initial))
			.AddTo(ref this.disposableBag);

		this.DisplayItemType = currentMaterial
			.Select(determineDisplayItemType)
			.ToReadOnlyBindableReactiveProperty(determineDisplayItemType(initial))
			.AddTo(ref this.disposableBag);

		this.IsExpandedVolume = currentMaterial
			.Select(material => material.Type == MaterialSourceType.WorkFolder)
			.ToReadOnlyBindableReactiveProperty(initial.Type == MaterialSourceType.WorkFolder)
			.AddTo(ref this.disposableBag);

		this.IsVolumeNumberEditable = this.IsExpandedVolume
			.AsObservable()
			.Select(isExpanded => !isExpanded)
			.ToReadOnlyBindableReactiveProperty(initial.Type != MaterialSourceType.WorkFolder)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 購読と派生 Reactive プロパティを破棄します。BindingVolume・MaterialItem は破棄しません。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}

	/// <summary>
	/// ファイル数の表示文字列を生成します。EPUB の場合は "-" を返します。
	/// </summary>
	/// <param name="material">対象の素材。</param>
	/// <returns>表示文字列。</returns>
	private static string createFileCountText(MaterialItem material)
		=> material.ItemType == MaterialItemType.Epub ? "-" : $"{material.FileCount} 件";

	/// <summary>
	/// アイコン表示用の MaterialItemType を決定します。
	/// Archive 内部エントリ、EPUB、実フォルダの区別を判定します。
	/// </summary>
	/// <param name="material">対象の素材。</param>
	/// <returns>表示用の素材種別。</returns>
	private static MaterialItemType determineDisplayItemType(MaterialItem material)
	{
		// Archive 内部エントリ（ArchiveEntryPrefix が null でない）
		if (!string.IsNullOrEmpty(material.ArchiveEntryPrefix))
		{
			return MaterialItemType.Archive;
		}

		// EPUB ファイル
		if (material.ItemType == MaterialItemType.Epub)
		{
			return MaterialItemType.Epub;
		}

		// それ以外は実フォルダ
		return MaterialItemType.Folder;
	}
}
