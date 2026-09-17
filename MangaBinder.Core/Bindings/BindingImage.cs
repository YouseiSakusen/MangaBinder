namespace MangaBinder.Bindings;

/// <summary>
/// 製本処理中の1画像を表すエンティティです。
/// 素材側の画像から、その後の処理・検査で判明した情報を同じインスタンスへ追加していくオブジェクトです。
/// </summary>
public class BindingImage : IDisposable
{
	/// <summary>
	/// この画像が所属する親 BindingVolume を取得します。
	/// 生成時に指定された親 BindingVolume インスタンスを保持します。
	/// </summary>
	public BindingVolume BindingVolume { get; }

	/// <summary>
	/// この画像の元となった素材側の画像を取得します。
	/// 生成時に指定された MaterialImage インスタンスを保持します。
	/// </summary>
	public MaterialImage MaterialImage { get; }

	/// <summary>
	/// 製本処理中のこの画像のファイル名を取得または設定します。
	/// 初期値は MaterialImage.FileName から設定されます。
	/// 画像変換等によって後続の処理で変更される可能性があります。
	/// </summary>
	public string FileName { get; set; }

	/// <summary>
	/// Work側に実体化された画像ファイルのフルパスを取得または設定します。
	/// BindingImage はWorkへのコピー前に生成するため、初期値は null です。
	/// Workへのコピーが正常終了した後に設定されます。
	/// </summary>
	public string? FilePath { get; set; }

	/// <summary>
	/// 画像の幅（ピクセル）を取得または設定します。
	/// 画像を実際に開いた際に判明する値です。
	/// 初期値は null です。
	/// </summary>
	public int? Width { get; set; }

	/// <summary>
	/// 画像の高さ（ピクセル）を取得または設定します。
	/// 画像を実際に開いた際に判明する値です。
	/// 初期値は null です。
	/// </summary>
	public int? Height { get; set; }

	/// <summary>
	/// この画像に紐づく一時的なStream実体を取得または設定します。
	/// Archive展開、Folder素材からのFileStream、EPUB等から取得した画像Streamなど、
	/// 画像処理中だけ一時的にこのプロパティへセットして使用します。
	/// このStreamは永続保持する情報ではなく、処理終了時点で即座にDisposeし、
	/// このプロパティを null に戻す前提です。
	/// 初期値は null です。
	/// </summary>
	public Stream? TemporaryImageStream { get; set; }

	/// <summary>
	/// BindingImageProcessor の Simulation により求めた、
	/// 実際に画像処理を行った場合の出力予定ファイル名を取得または設定します。
	/// フルパスではなくファイル名のみです。
	/// 初期値は null です。
	/// </summary>
	public string? SimulatedFileName { get; set; }

	/// <summary>
	/// 後続の実画像処理を行わない画像であるかどうかを取得または設定します。
	/// ファイル名競合が発生した BindingImage に対して true が設定されます。
	/// 初期値は false です。
	/// </summary>
	public bool SkipImageProcessing { get; set; }

	/// <summary>
	/// 実画像処理の結果を取得または設定します。
	/// 初期値は NotProcessed です。
	/// </summary>
	public BindingImageProcessStatus ProcessStatus { get; set; }

	/// <summary>
	/// UIで詳細表示するための画像処理エラーメッセージを取得または設定します。
	/// Exceptionオブジェクト自体は保持しません。
	/// 初期値は null です。
	/// </summary>
	public string? ProcessErrorMessage { get; set; }

	/// <summary>
	/// ファイル名正規化 Simulation により求めた、
	/// 正規化後に予定されるファイル名を取得または設定します。
	/// フルパスではなくファイル名のみです。
	/// 初期値は null です。
	/// </summary>
	public string? SimulatedNormalizedFileName { get; set; }

	/// <summary>
	/// ファイル名正規化（主番号のゼロ埋め）の処理状態を取得または設定します。
	/// 初期値は NotProcessed です。
	/// </summary>
	public FileNameNormalizationStatus FileNameNormalizationStatus { get; set; }

	/// <summary>
	/// ファイル名正規化に関する詳細情報を取得または設定します。
	/// 解析失敗や Conflict の詳細を後から UI で確認するために保持します。
	/// Exception オブジェクト自体は保持しません。
	/// 初期値は null です。
	/// </summary>
	public string? FileNameNormalizationErrorMessage { get; set; }

	/// <summary>
	/// ファイル名が文字化けしている可能性がある場合 true を取得または設定します。
	/// これは FileNameNormalizationStatus とは独立した警告情報です。
	/// 初期値は false です。
	/// </summary>
	public bool HasSuspectedMojibake { get; set; }

	/// <summary>
	/// ファイルを正規化ファイル名にリネーム可能かどうかを取得します。
	/// FileNameNormalizationStatus == Ready の場合のみ true です。
	/// </summary>
	public bool CanRenameFile => this.FileNameNormalizationStatus == FileNameNormalizationStatus.Ready;

	/// <summary>
	/// 画像が横長（幅 > 高さ）かどうかを取得します。
	/// Width と Height の両方が取得できており、Width > Height の場合のみ true です。
	/// Width または Height が null の場合は false です。
	/// 判定結果は別フィールドへ保持せず、都度 Width / Height から計算されます。
	/// </summary>
	public bool IsLandscape => this.Width.HasValue && this.Height.HasValue && this.Width > this.Height;

	/// <summary>
	/// この BindingImage の処理によって別の出力ファイルを正常に生成した場合、
	/// 入力元の物理ファイルを削除すべきかを取得します。
	/// 入力元が Work フォルダである場合のみ true を返します。
	/// Folder、Archive、Epub の場合は false です。
	/// </summary>
	public bool ShouldDeleteSourceFile => this.BindingVolume.Material.EffectiveSourceType == MaterialSourceType.WorkFolder;

	/// <summary>
	/// <see cref="BindingImage"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingVolume">この画像が所属する親 BindingVolume。</param>
	/// <param name="materialImage">この画像の元となった素材側の画像。</param>
	/// <exception cref="ArgumentNullException"><paramref name="bindingVolume"/> または <paramref name="materialImage"/> が null の場合。</exception>
	public BindingImage(BindingVolume bindingVolume, MaterialImage materialImage)
	{
		this.BindingVolume = bindingVolume ?? throw new ArgumentNullException(nameof(bindingVolume));
		this.MaterialImage = materialImage ?? throw new ArgumentNullException(nameof(materialImage));
		this.FileName = materialImage.FileName;
		this.FilePath = null;
		this.Width = null;
		this.Height = null;
		this.TemporaryImageStream = null;
		this.SimulatedFileName = null;
		this.SkipImageProcessing = false;
		this.ProcessStatus = BindingImageProcessStatus.NotProcessed;
		this.ProcessErrorMessage = null;
		this.SimulatedNormalizedFileName = null;
		this.FileNameNormalizationStatus = FileNameNormalizationStatus.NotProcessed;
		this.FileNameNormalizationErrorMessage = null;
		this.HasSuspectedMojibake = false;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// TemporaryImageStream が残っている場合は破棄する
		// 通常はファイル処理側の finally で即座に破棄されるが、
		// 異常経路・キャンセル・将来の処理変更等でStreamが残った場合の安全網
		if (this.TemporaryImageStream is not null)
		{
			this.TemporaryImageStream.Dispose();
			this.TemporaryImageStream = null;
		}
	}
}
