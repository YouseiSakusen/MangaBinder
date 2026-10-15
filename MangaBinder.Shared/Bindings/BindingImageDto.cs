namespace MangaBinder.Bindings;

/// <summary>
/// 製本設定として保存する1画像のDTOです。
/// 親の BindingVolume や素材への参照、Work 側の実行時パスは保持しません。
/// </summary>
public class BindingImageDto
{
	/// <summary>素材内でこの画像を特定するパスを取得または設定します。</summary>
	public string SourceImagePath { get; set; } = string.Empty;

	/// <summary>素材から展開した直後のファイル名を取得または設定します。</summary>
	public string MaterialFileName { get; set; } = string.Empty;

	/// <summary>製本処理中のファイル名を取得または設定します。</summary>
	public string FileName { get; set; } = string.Empty;

	/// <summary>見開き分割対象かどうかを取得または設定します。</summary>
	public bool IsSpreadSplitTarget { get; set; }

	/// <summary>画像の幅（ピクセル）を取得または設定します。</summary>
	public int? Width { get; set; }

	/// <summary>画像の高さ（ピクセル）を取得または設定します。</summary>
	public int? Height { get; set; }

	/// <summary>画像処理後の出力予定ファイル名を取得または設定します。</summary>
	public string? SimulatedFileName { get; set; }

	/// <summary>後続の実画像処理を行わない画像かどうかを取得または設定します。</summary>
	public bool SkipImageProcessing { get; set; }

	/// <summary>実画像処理の結果を取得または設定します。</summary>
	public BindingImageProcessStatus ProcessStatus { get; set; }

	/// <summary>画像処理エラーメッセージを取得または設定します。</summary>
	public string? ProcessErrorMessage { get; set; }

	/// <summary>ファイル名正規化後に予定されるファイル名を取得または設定します。</summary>
	public string? SimulatedNormalizedFileName { get; set; }

	/// <summary>ファイル名正規化の処理状態を取得または設定します。</summary>
	public FileNameNormalizationStatus FileNameNormalizationStatus { get; set; }

	/// <summary>ファイル名正規化に関する詳細情報を取得または設定します。</summary>
	public string? FileNameNormalizationErrorMessage { get; set; }

	/// <summary>ファイル名が文字化けしている可能性があるかを取得または設定します。</summary>
	public bool HasSuspectedMojibake { get; set; }

	/// <summary>ファイル名の連番直前1文字を取得または設定します。</summary>
	public string SerialNumberPrecedingCharacter { get; set; } = string.Empty;

	/// <summary>分割によって生成された画像の、元画像上の左右を取得または設定します。未分割の場合は null です。</summary>
	public SplitSide? SplitSide { get; set; }

	/// <summary>
	/// 巻共通とは別インスタンスとして設定されていた、この画像の個別の見開き分割設定を取得または設定します。
	/// 未設定、または巻共通の設定を使用していた場合は null です。
	/// </summary>
	public SplitSettingsDto? SplitSettings { get; set; }
}
