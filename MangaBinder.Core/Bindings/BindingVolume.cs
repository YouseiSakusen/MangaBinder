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
	/// BindingImage をファイル名でアルファベット順（OrdinalIgnoreCase）に比較するためのコンパレーターです。
	/// Inspect() メソッド内で Images の並び替えに使用します。
	/// </summary>
	private static readonly IComparer<BindingImage> fileNameComparer =
		Comparer<BindingImage>.Create(
			(x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.FileName, y.FileName));

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
	/// 今回の製本で実際に使用する入力元パスを取得します。
	/// Material.EffectiveSourceType が WorkFolder の場合は this.WorkFolderPath を返し、
	/// それ以外の場合は Material.SourcePath を返します。
	/// </summary>
	public string EffectiveSourcePath => this.Material.EffectiveSourceType == MaterialSourceType.WorkFolder
		? this.WorkFolderPath!
		: this.Material.SourcePath;

	/// <summary>
	/// この巻の製本作業フォルダのフルパスを取得または設定します。
	/// SeriesInspectionManager が製本前確認処理で確定した値を設定します。
	/// DB へ永続化される情報ではなく、現在のセッション中の一時的な管理情報です。
	/// </summary>
	public string? WorkFolderPath { get; set; }

	/// <summary>
	/// この巻の製本処理で使用するWork上の画像一覧を取得します。
	/// </summary>
	public ObservableList<BindingImage> Images { get; }

	/// <summary>
	/// 画像処理後に同一ファイル名になる BindingImage が
	/// この巻内に存在することを表す値を取得または設定します。
	/// 初期値は false です。
	/// </summary>
	public bool HasImageFileNameConflict { get; set; }

	/// <summary>
	/// この巻内のいずれかの BindingImage が
	/// 画像処理で失敗状態（ImageOpenFailed / ConversionFailed / OutputFailed 等）になった場合に
	/// Inspect() により設定されるフラグを取得します。
	/// 初期値は false です。
	/// </summary>
	public bool HasImageProcessingError { get; private set; }

	/// <summary>
	/// FolderMaterialExtractor が処理した元素材フォルダの直下に、
	/// 1つ以上のサブフォルダが存在することを表すフラグを取得または設定します。
	/// 初期値は false です。
	/// これはエラーでも警告でもなく、素材フォルダの構造についての事実を保持するものです。
	/// </summary>
	public bool HasSubFolder { get; set; }

	/// <summary>
	/// この巻について、BindingImageProcessor によって Work側へ正常に実体化された
	/// 画像の数を取得します。これは Inspect() が集計した値です。
	/// </summary>
	public int ImageFileCount { get; private set; }

	/// <summary>
	/// この巻について、ImageFileCount に含まれる画像のうち、
	/// 幅 > 高さ（横長）である画像の数を取得します。これは Inspect() が集計した値です。
	/// </summary>
	public int LandscapeImageCount { get; private set; }

	/// <summary>
	/// この巻の EPUB 展開時に発生した既知エラーを取得または設定します。
	/// EPUB 素材でない場合、またはエラーが発生していない場合は None です。
	/// 初期値は EpubExtractionError.None です。
	/// </summary>
	public EpubExtractionError EpubExtractionError { get; set; }

	/// <summary>
	/// この巻の EPUB 展開中に検出された警告情報を取得または設定します。
	/// 複数の警告が存在する場合は Flags で OR で蓄積されます。
	/// EPUB 素材でない場合、または警告が発生していない場合は None です。
	/// 初期値は EpubExtractionWarning.None です。
	/// </summary>
	public EpubExtractionWarning EpubExtractionWarnings { get; set; }

	/// <summary>
	/// この巻の EPUB 展開時に発生した分類不能な予期しないエラーのメッセージを取得または設定します。
	/// EpubExtractionError が UnexpectedError の場合のみ、例外の Message を保持します。
	/// それ以外の場合は null です。
	/// 初期値は null です。
	/// </summary>
	public string? EpubExtractionErrorMessage { get; set; }

	/// <summary>
	/// この巻共通の見開き分割設定を取得します。
	/// </summary>
	public SplitSettings SplitSettings { get; }

	/// <summary>
	/// <see cref="BindingVolume"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="material">この巻の元となった素材。BindingStore.Materials 内に存在するインスタンスを指定してください。</param>
	public BindingVolume(MaterialItem material)
		: this(material, new SplitSettings())
	{
	}

	/// <summary>
	/// 指定した SplitSettings を所有する <see cref="BindingVolume"/> を初期化します。
	/// </summary>
	/// <param name="material">この巻の元となった素材。</param>
	/// <param name="splitSettings">この巻が所有する見開き分割設定。</param>
	private BindingVolume(MaterialItem material, SplitSettings splitSettings)
	{
		this.Material = material ?? throw new ArgumentNullException(nameof(material));
		this.VolumeNumber = new BindableReactiveProperty<decimal?>(null)
			.AddTo(ref this.disposableBag);
		this.WorkFolderPath = null;
		this.Images = new ObservableList<BindingImage>();
		this.HasImageFileNameConflict = false;
		this.HasImageProcessingError = false;
		this.HasSubFolder = false;
		this.ImageFileCount = 0;
		this.LandscapeImageCount = 0;
		this.EpubExtractionError = EpubExtractionError.None;
		this.EpubExtractionWarnings = EpubExtractionWarning.None;
		this.EpubExtractionErrorMessage = null;
		this.SplitSettings = splitSettings
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// ImageSplitter の編集用として、独立した BindingVolume を作成します。
	/// Material は同一インスタンスを参照し、それ以外の状態・ReactiveProperty・コレクションは共有しません。
	/// </summary>
	/// <returns>編集用の新しい BindingVolume。</returns>
	public BindingVolume CloneForImageSplitter()
	{
		var clone = new BindingVolume(this.Material, this.SplitSettings.CloneForImageSplitter());
		clone.VolumeNumber.Value = this.VolumeNumber.Value;
		clone.WorkFolderPath = this.WorkFolderPath;
		clone.HasImageFileNameConflict = this.HasImageFileNameConflict;
		clone.HasImageProcessingError = this.HasImageProcessingError;
		clone.HasSubFolder = this.HasSubFolder;
		clone.ImageFileCount = this.ImageFileCount;
		clone.LandscapeImageCount = this.LandscapeImageCount;
		clone.EpubExtractionError = this.EpubExtractionError;
		clone.EpubExtractionWarnings = this.EpubExtractionWarnings;
		clone.EpubExtractionErrorMessage = this.EpubExtractionErrorMessage;

		foreach (var image in this.Images)
		{
			clone.Images.Add(image.CloneForImageSplitter(clone));
		}

		return clone;
	}

	/// <summary>
	/// SplitSettings が未設定の画像へ、この巻の共通 SplitSettings を同一インスタンスで設定します。
	/// 設定済みの画像は上書きしません。
	/// </summary>
	public void ApplyCommonSplitSettings()
	{
		foreach (var image in this.Images)
		{
			image.SplitSettings ??= this.SplitSettings;
		}
	}

	/// <summary>
	/// Images を指定した一覧の順序で置き換えます。
	/// 新しい一覧に同一インスタンスとして残らない旧 BindingImage のみ Dispose します。
	/// </summary>
	/// <param name="newImages">新しい BindingImage 一覧。</param>
	public void ReplaceImages(IReadOnlyList<BindingImage> newImages)
	{
		ArgumentNullException.ThrowIfNull(newImages);

		var retained = new HashSet<BindingImage>(newImages, ReferenceEqualityComparer.Instance);
		var oldImages = this.Images.ToArray();

		this.Images.Clear();
		this.Images.AddRange(newImages);

		foreach (var old in oldImages)
		{
			if (!retained.Contains(old))
			{
				old.Dispose();
			}
		}
	}

	/// <summary>
	/// Images の最大 Width / Height を1回の走査で取得します。
	/// Images が0件、または寸法が未設定の場合、該当値は 0 です。
	/// </summary>
	/// <returns>最大画像サイズ。</returns>
	public (int Width, int Height) GetMaxImageSize()
	{
		var maxWidth = 0;
		var maxHeight = 0;
		foreach (var image in this.Images)
		{
			maxWidth = Math.Max(maxWidth, image.Width ?? 0);
			maxHeight = Math.Max(maxHeight, image.Height ?? 0);
		}

		return (maxWidth, maxHeight);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Images 内の
		foreach (var image in this.Images)
		{
			image.Dispose();
		}
		this.Images.Clear();

		// VolumeNumber の ReactiveProperty を破棄する
		// Material は BindingStore.Materials が所有しているため Dispose しない
		this.disposableBag.Dispose();
	}

	/// <summary>
	/// この巻の製本前検査を実行し、BindingImage から集計した結果を
	/// BindingVolume のプロパティに反映させます。
	/// 
	/// 処理内容:
	/// 1. Images を FileName（正規化済み）でアルファベット順（OrdinalIgnoreCase）に並び替える
	/// 2. 集計対象:
	///    - ImageFileCount: Images に存在する全 BindingImage 件数
	///    - LandscapeImageCount: Images のうち IsLandscape == true の BindingImage 件数
	///    - HasImageProcessingError: ProcessStatus が ImageOpenFailed / ConversionFailed / OutputFailed のいずれかの画像が存在するか
	/// 
	/// 注意:
	/// - ImageFileCount / LandscapeImageCount には、ProcessStatus や FilePath の値に関わらず全画像をカウント
	/// - 画像処理Error等により横長判定できなかった画像は IsLandscape == false のため LandscapeImageCount には含まれない
	/// - HasImageProcessingError は引き続き ProcessStatus のみで判定（Skipped は含めない）
	/// - HasSubFolder / HasImageFileNameConflict / EPUB情報は変更・再判定しない
	/// </summary>
	public void Inspect()
	{
		// ① Images を FileName で OrdinalIgnoreCase 順序に並び替える
		// ObservableList<T>.Sort() を使用して、Sort 操作として通知し、
		// CreateView() 等の SynchronizedView 側にも反映される構造とする
		this.Images.Sort(fileNameComparer);

		// ② ImageFileCount: Images に存在する全 BindingImage 件数
		this.ImageFileCount = this.Images.Count;

		// ③ LandscapeImageCount: Images のうち IsLandscape == true の BindingImage 件数
		this.LandscapeImageCount = this.Images
			.Count(img => img.IsLandscape);

		// ④ HasImageProcessingError: エラー状態の画像が存在するか判定
		this.HasImageProcessingError = this.Images.Any(img =>
			img.ProcessStatus == BindingImageProcessStatus.ImageOpenFailed ||
			img.ProcessStatus == BindingImageProcessStatus.ConversionFailed ||
			img.ProcessStatus == BindingImageProcessStatus.OutputFailed);
	}

	/// <summary>
	/// この巻に属する新しい画像を追加します。
	/// 素材側の画像情報から MaterialImage を生成し、
	/// BindingImage を作成してこの巻の Images に追加するための共通入口です。
	/// </summary>
	/// <param name="sourceImagePath">素材内でこの画像を特定するパス。</param>
	/// <param name="fileName">展開直後に使用するファイル名。</param>
	/// <returns>生成・追加された BindingImage。この巻の Images に含まれています。</returns>
	/// <exception cref="ArgumentNullException"><paramref name="sourceImagePath"/> または <paramref name="fileName"/> が null の場合。</exception>
	public BindingImage AddImage(string sourceImagePath, string fileName)
	{
		// MaterialItem から MaterialImage を生成
		var materialImage = this.Material.CreateMaterialImage(sourceImagePath, fileName);

		// BindingImage を生成
		var bindingImage = new BindingImage(this, materialImage);

		// Images に追加
		this.Images.Add(bindingImage);

		return bindingImage;
	}
}
