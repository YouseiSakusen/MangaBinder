namespace MangaBinder.Bindings;

/// <summary>
/// 素材中に存在する1画像を表すエンティティです。
/// MaterialItem が表す素材の中から、個別の画像1枚を識別するための情報を保持します。
/// </summary>
public class MaterialImage
{
	/// <summary>
	/// この画像の元となった素材を取得します。
	/// BindingStore.Materials 内に存在する MaterialItem と同一インスタンスを参照します。
	/// </summary>
	public MaterialItem MaterialItem { get; init; }

	/// <summary>
	/// 素材内でこの画像を特定するパスを取得します。
	/// Archive の場合は Archive Entry のパス、
	/// Folder の場合は実画像ファイルのパス、
	/// EPUB の場合は EPUB 内部画像のパスを保持します。
	/// </summary>
	public string SourceImagePath { get; init; }

	/// <summary>
	/// 素材から展開した直後に使用する画像ファイル名を取得します。
	/// フルパスではなくファイル名のみです。
	/// </summary>
	public string FileName { get; init; }

	/// <summary>
	/// <see cref="MaterialImage"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="materialItem">この画像の元となった素材。</param>
	/// <param name="sourceImagePath">素材内でこの画像を特定するパス。</param>
	/// <param name="fileName">展開直後に使用するファイル名。</param>
	/// <exception cref="ArgumentNullException">いずれかのパラメータが null の場合。</exception>
	private MaterialImage(
		MaterialItem materialItem,
		string sourceImagePath,
		string fileName)
	{
		this.MaterialItem = materialItem ?? throw new ArgumentNullException(nameof(materialItem));
		this.SourceImagePath = sourceImagePath ?? throw new ArgumentNullException(nameof(sourceImagePath));
		this.FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
	}

	/// <summary>
	/// MaterialItem 用の内部的な生成ファクトリー。
	/// CreateMaterialImage() 呼び出しを通じてのみ外部から生成されます。
	/// </summary>
	internal static MaterialImage Create(
		MaterialItem materialItem,
		string sourceImagePath,
		string fileName)
	{
		return new MaterialImage(materialItem, sourceImagePath, fileName);
	}
}
