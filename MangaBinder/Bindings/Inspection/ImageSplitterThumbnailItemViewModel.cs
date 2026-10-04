using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 見開き分割一覧の1画像分の表示用 ViewModel です。
/// チェック状態は <see cref="BindingImage.IsSpreadSplitTarget"/> を直接使用します。
/// </summary>
public sealed class ImageSplitterThumbnailItemViewModel
{
	/// <summary>元の BindingImage を取得します。</summary>
	public BindingImage Image { get; }

	/// <summary>サムネイル画像を取得します。</summary>
	public ImageSource ThumbnailSource { get; }

	/// <summary>
	/// フルサイズのプレビュー画像キャッシュを取得または設定します。
	/// 未ロードの場合は null です。ImageSplitter 画面のセッション中のみ保持されます。
	/// </summary>
	public BitmapSource? PreviewSource { get; set; }

	/// <summary>表示用ファイル名を取得します。</summary>
	public string FileName => this.Image.FileName;

	/// <summary>
	/// <see cref="ImageSplitterThumbnailItemViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="image">元の BindingImage。</param>
	/// <param name="thumbnailSource">サムネイル画像。</param>
	public ImageSplitterThumbnailItemViewModel(BindingImage image, ImageSource thumbnailSource)
	{
		this.Image = image;
		this.ThumbnailSource = thumbnailSource;
	}
}
