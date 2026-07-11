using MangaBinder.Bindings;
using MangaBinder.Controls;
using MangaBinder.Core.Formatters;
using MangaBinder.Tags;
using ObservableCollections;
using R3;

namespace MangaBinder;

/// <summary>
/// Home 画面の ListView アイテム表示用 ViewModel です。MangaSeries をラップしています。
/// </summary>
public class SeriesCardViewModel : IDisposable
{
	private DisposableBag disposableBag = new();
	private NotifyCollectionChangedEventHandler<MangaTag>? collectionChangedHandler;
	private MangaSeries series = null!;

	/// <summary>
	/// 基になった MangaSeries です。
	/// </summary>
	public MangaSeries Series { get; }

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// </summary>
	public BindableReactiveProperty<SeriesVolumeStatusViewModel> VolumeStatus { get; }

	/// <summary>
	/// 製本対象として選択されているかどうかを示します。
	/// UI 状態のため、SeriesCardViewModel が保持します。
	/// </summary>
	public BindableReactiveProperty<bool> IsSelected { get; }

	/// <summary>
	/// タグ表示用テキスト。
	/// Series.Tags の変更に応じて自動更新されます。
	/// </summary>
	public BindableReactiveProperty<string> TagDisplayText { get; }

	/// <summary>
	/// <see cref="SeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="series">ラップする MangaSeries。</param>
	/// <param name="bindingQueueStore">製本開始キュー ストア。初期値決定用。</param>
	public SeriesCardViewModel(MangaSeries series, BindingQueueStore? bindingQueueStore = null)
	{
		this.Series = series;
		this.series = series;

		// VolumeStatus の初期化
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(
			SeriesVolumeStatusViewModel.FromSeries(series))
			.AddTo(ref this.disposableBag);

		// IsSelected の初期値を BindingQueueStore から決定
		var isInQueue = bindingQueueStore?.Contains(series.SeriesId) ?? false;
		this.IsSelected = new BindableReactiveProperty<bool>(isInQueue)
			.AddTo(ref this.disposableBag);

		// TagDisplayText の初期化とタグ変更購読
		this.TagDisplayText = new BindableReactiveProperty<string>(
			SeriesTagDisplayFormatter.Format(series.Tags))
			.AddTo(ref this.disposableBag);

		// Tags の変更を購読
		this.collectionChangedHandler = this.OnTagsCollectionChanged;
		series.Tags.CollectionChanged += this.collectionChangedHandler;
	}

	/// <summary>
	/// Tags コレクション変更時のハンドラー。
	/// </summary>
	private void OnTagsCollectionChanged(in NotifyCollectionChangedEventArgs<MangaTag> e)
	{
		this.TagDisplayText.Value = SeriesTagDisplayFormatter.Format(this.series.Tags);
	}

	/// <summary>
	/// 現在の Series インスタンスから VolumeStatus を再生成します。
	/// </summary>
	public void RefreshVolumeStatus()
	{
		this.VolumeStatus.Value = SeriesVolumeStatusViewModel.FromSeries(this.Series);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (this.collectionChangedHandler != null)
		{
			this.Series.Tags.CollectionChanged -= this.collectionChangedHandler;
		}
		this.disposableBag.Dispose();
	}
}
