namespace MangaBinder.Bindings;

/// <summary>
/// 製本設定として保存する1巻のDTOです。
/// Work 側の実行時パスは保持しません。
/// </summary>
public class BindingVolumeDto
{
	/// <summary>巻番号を取得または設定します。</summary>
	public decimal? VolumeNumber { get; set; }

	/// <summary>今回の製本でこの巻を使用するかを取得または設定します。JSON に存在しない場合は true です。</summary>
	public bool IsBinding { get; set; } = true;

	/// <summary>この巻に紐づく素材の情報を取得または設定します。</summary>
	public MaterialItemDto Material { get; set; } = new();

	/// <summary>画像処理後に同一ファイル名になる画像が存在するかを取得または設定します。</summary>
	public bool HasImageFileNameConflict { get; set; }

	/// <summary>画像処理で失敗状態になった画像が存在するかを取得または設定します。</summary>
	public bool HasImageProcessingError { get; set; }

	/// <summary>連番直前文字が複数種類に分かれているかを取得または設定します。</summary>
	public bool HasMixedSerialNumberPrecedingCharacters { get; set; }

	/// <summary>素材フォルダの直下にサブフォルダが存在するかを取得または設定します。</summary>
	public bool HasSubFolder { get; set; }

	/// <summary>Work 側へ実体化された画像の数を取得または設定します。</summary>
	public int ImageFileCount { get; set; }

	/// <summary>横長画像の数を取得または設定します。</summary>
	public int LandscapeImageCount { get; set; }

	/// <summary>EPUB 展開時に発生した既知エラーを取得または設定します。</summary>
	public EpubExtractionError EpubExtractionError { get; set; }

	/// <summary>EPUB 展開中に検出された警告情報を取得または設定します。</summary>
	public EpubExtractionWarning EpubExtractionWarnings { get; set; }

	/// <summary>EPUB 展開時の予期しないエラーのメッセージを取得または設定します。</summary>
	public string? EpubExtractionErrorMessage { get; set; }

	/// <summary>この巻共通の見開き分割設定を取得または設定します。</summary>
	public SplitSettingsDto SplitSettings { get; set; } = new();

	/// <summary>この巻の画像一覧を取得または設定します。</summary>
	public List<BindingImageDto> Images { get; set; } = [];
}
