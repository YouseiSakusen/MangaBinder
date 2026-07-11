using MangaBinder.Controls;
using MangaBinder.Core.Formatters;
using MangaBinder.Tags;
using ObservableCollections;
using R3;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本開始ページの ListView アイテム表示用 ViewModel です。BindingSeries をラップしています。
/// </summary>
public class StartPageSeriesCardViewModel : IDisposable
{
	private DisposableBag disposableBag = new();
	private NotifyCollectionChangedEventHandler<MangaTag>? collectionChangedHandler;
	private BindingSeries bindingSeries = null!;

	/// <summary>
	/// 基になった BindingSeries です。
	/// </summary>
	public BindingSeries BindingSeries { get; }

	/// <summary>
	/// BindingSeries に含まれる MangaSeries です。
	/// </summary>
	public MangaSeries Series => this.BindingSeries.Series;

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// </summary>
	public SeriesVolumeStatusViewModel VolumeStatus { get; }

	/// <summary>
	/// あらすじが存在するかどうかを示します。
	/// </summary>
	public bool HasSynopsis => this.BindingSeries.HasSynopsis;

	/// <summary>
	/// 製本開始キュー内での表示用タグテキスト。
	/// </summary>
	public BindableReactiveProperty<string> TagDisplayText { get; }

	/// <summary>
	/// 製本開始キューの進行状態を取得します。
	/// </summary>
	public BindingStartStatus Status => this.BindingSeries.Status;

	/// <summary>
	/// 現在の製本工程番号を取得します。
	/// </summary>
	public int CurrentStep => this.BindingSeries.CurrentStep;

	/// <summary>
	/// キューに追加した日時を取得します。
	/// </summary>
	public DateTime AddedAt => this.BindingSeries.AddedAt;

	/// <summary>
	/// 最終更新日時を取得します。
	/// </summary>
	public DateTime UpdatedAt => this.BindingSeries.UpdatedAt;

	/// <summary>
	/// <see cref="StartPageSeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingSeries">ラップする BindingSeries。</param>
	public StartPageSeriesCardViewModel(BindingSeries bindingSeries)
	{
		this.BindingSeries = bindingSeries;
		this.bindingSeries = bindingSeries;
		this.VolumeStatus = SeriesVolumeStatusViewModel.FromSeries(bindingSeries.Series);

		// TagDisplayText の初期化とタグ変更購読
		this.TagDisplayText = new BindableReactiveProperty<string>(
			SeriesTagDisplayFormatter.FormatForStartPage(bindingSeries.Series.Tags))
			.AddTo(ref this.disposableBag);

		// Tags の変更を購読
		this.collectionChangedHandler = this.OnTagsCollectionChanged;
		bindingSeries.Series.Tags.CollectionChanged += this.collectionChangedHandler;
	}

	/// <summary>
	/// Tags コレクション変更時のハンドラー。
	/// </summary>
	private void OnTagsCollectionChanged(in NotifyCollectionChangedEventArgs<MangaTag> e)
	{
		this.TagDisplayText.Value = SeriesTagDisplayFormatter.FormatForStartPage(this.bindingSeries.Series.Tags);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (this.collectionChangedHandler != null)
		{
			this.BindingSeries.Series.Tags.CollectionChanged -= this.collectionChangedHandler;
		}
		this.disposableBag.Dispose();
	}
}
