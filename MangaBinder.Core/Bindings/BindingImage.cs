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
	/// 素材側で、この BindingImage がどの画像を表しているか識別する名前を取得または設定します。
	/// Folder素材ではファイル名、Archive / EPUB ではそれぞれの段階で決定される名前です。
	/// </summary>
	public string? SourceName { get; set; }

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
	/// <see cref="BindingImage"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingVolume">この画像が所属する親 BindingVolume。</param>
	/// <exception cref="ArgumentNullException"><paramref name="bindingVolume"/> が null の場合。</exception>
	public BindingImage(BindingVolume bindingVolume)
	{
		this.BindingVolume = bindingVolume ?? throw new ArgumentNullException(nameof(bindingVolume));
		this.SourceName = null;
		this.FilePath = null;
		this.Width = null;
		this.Height = null;
		this.TemporaryImageStream = null;
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
