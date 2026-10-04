using MangaBinder.Controls;
using MangaBinder.Helpers;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程の各画面で共通の、作品タイトルと作品カードの表示責務を持つ ViewModel です。
/// </summary>
public class BindingSeriesViewModel : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>選択中の作品名を取得します。</summary>
	public IReadOnlyBindableReactiveProperty<string> SeriesTitle { get; }

	/// <summary>
	/// 作品サムネイルカード用の ViewModel を取得します。
	/// </summary>
	public MangaSeriesCardViewModel MangaSeriesCard { get; }

	/// <summary>
	/// <see cref="BindingSeriesViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
	public BindingSeriesViewModel(
		BindingStore bindingStore,
		ThumbnailImageLoader thumbnailImageLoader)
	{
		// SeriesTitle: BindingTarget.Value?.Series.Title から Reactive に導出
		this.SeriesTitle = bindingStore.BindingTarget
			.Select(bindingTarget => bindingTarget?.Series?.Title ?? string.Empty)
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		// MangaSeriesCard: 作品サムネイルカード用ViewModel
		this.MangaSeriesCard = new MangaSeriesCardViewModel()
			.AddTo(ref this.disposableBag);

		// BindingTarget 変更時に MangaSeriesCard へ Series と ThumbnailSource を接続
		bindingStore.BindingTarget.Subscribe(bindingTarget =>
		{
			var series = bindingTarget?.Series;
			if (series is not null)
			{
				// ThumbnailImageLoader で最終表示用 ImageSource を取得
				var imageSource = thumbnailImageLoader.Load(series);

				// MangaSeriesCard へ設定
				this.MangaSeriesCard.ThumbnailSource.Value = imageSource;

				// MangaSeriesCard の Series へ接続
				if (!ReferenceEquals(this.MangaSeriesCard.Series.Value, series))
				{
					this.MangaSeriesCard.Series.Value = series;
				}
				else
				{
					// 同一インスタンスの場合は ForceNotify() で再通知させる
					this.MangaSeriesCard.Series.ForceNotify();
				}
			}
			else
			{
				// series が null の場合はクリア
				this.MangaSeriesCard.ThumbnailSource.Value = null;
				this.MangaSeriesCard.Series.Value = null;
			}
		}).AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// リソースを破棄します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
