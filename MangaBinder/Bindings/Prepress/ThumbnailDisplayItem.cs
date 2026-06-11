using System.Windows.Media.Imaging;
using R3;

namespace MangaBinder.Bindings.Prepress;

/// <summary>
/// <see cref="VolumeThumbnailItem"/> を WPF 表示用にラップした画面バインド用アイテムです。
/// </summary>
public sealed class ThumbnailDisplayItem : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>元データを取得します。</summary>
	public VolumeThumbnailItem Source { get; }

	/// <summary>ファイルのフルパスを取得します。</summary>
	public string FilePath => this.Source.FilePath;

	/// <summary>ファイル名を取得します。</summary>
	public string FileName => this.Source.FileName;

	/// <summary>表示用サムネイル画像を取得します。</summary>
	public BitmapImage Thumbnail { get; }

	/// <summary>分割対象としてチェックされているかどうかを取得または設定します。</summary>
	public BindableReactiveProperty<bool> IsChecked { get; }

	/// <summary>このアイテムにエラーがあるかどうかを取得します。</summary>
	public bool HasError => this.Source.HasError;

	/// <summary>対応外ファイル形式かどうかを取得します。</summary>
	public bool IsUnsupported => this.Source.IsUnsupported;

	/// <summary>
	/// <see cref="ThumbnailDisplayItem"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="source">元データ。</param>
	/// <param name="thumbnail">生成済みサムネイル画像。</param>
	public ThumbnailDisplayItem(VolumeThumbnailItem source, BitmapImage thumbnail)
	{
		this.Source = source;
		this.Thumbnail = thumbnail;
		this.IsChecked = new BindableReactiveProperty<bool>(source.IsChecked)
			.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public void Dispose()
		=> this.disposableBag.Dispose();
}
