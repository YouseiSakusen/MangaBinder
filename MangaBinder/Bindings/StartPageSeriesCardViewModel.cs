using MangaBinder.Controls;
using MangaBinder.Core.Formatters;
using MangaBinder.Series;
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

	/// <summary>
	/// 基になった BindingSeries です。
	/// </summary>
	public BindingSeries BindingSeries { get; }

	/// <summary>
	/// BindingSeries に含まれる MangaSeries です。
	/// ReactiveProperty として、値の変更を監視できます。
	/// </summary>
	public BindableReactiveProperty<MangaSeries> Series { get; set; } = null!;

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// ReactiveProperty として、値の変更を監視できます。
	/// </summary>
	public BindableReactiveProperty<SeriesVolumeStatusViewModel> VolumeStatus { get; set; } = null!;

	/// <summary>
	/// メモ表示用の ViewModel です。
	/// ReactiveProperty として、値の変更を監視できます。
	/// </summary>
	public BindableReactiveProperty<SeriesMemoViewModel> MemoStatus { get; set; } = null!;

	/// <summary>
	/// あらすじが存在するかどうかを示します。
	/// Series の変更に応じて自動更新されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> HasSynopsis { get; set; } = null!;

	/// <summary>
	/// 製本開始キュー内での表示用タグテキスト。
	/// 共有 MangaSeriesNotifier.Tags の追加・削除と、共有 TagNotifier の名称変更から導出されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> TagDisplayText { get; private set; } = null!;

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
	/// BindingSeries が保持する共有 MangaSeriesNotifier の Series BindableReactiveProperty を使用します。
	/// 共有 Notifier は MangaSeriesStore の所有物のため、このクラスでは Dispose しません。
	/// </summary>
	/// <param name="bindingSeries">ラップする BindingSeries。</param>
	public StartPageSeriesCardViewModel(BindingSeries bindingSeries)
	{
		this.BindingSeries = bindingSeries;

		// Series の初期化（共有 Notifier から直接参照）
		this.Series = bindingSeries.SeriesNotifier.Series;

		this.initializeCommon(bindingSeries);
	}

	/// <summary>
	/// VolumeStatus、MemoStatus、HasSynopsis、TagDisplayText、Tags購読を初期化する共通処理です。
	/// </summary>
	private void initializeCommon(BindingSeries bindingSeries)
	{
		// 巻情報表示用の SeriesVolumeStatusViewModel を生成（1インスタンスのみ保持）
		var volumeStatus = SeriesVolumeStatusViewModel.FromSeries(bindingSeries.Series);
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(volumeStatus)
			.AddTo(ref this.disposableBag);

		// volumeStatus は BindableReactiveProperty に保持されるため、ここでは管理不要だが
		// IDisposable であるため、Dispose 時に破棄されるよう disposableBag に登録
		volumeStatus.AddTo(ref this.disposableBag);

		// メモ表示用の SeriesMemoViewModel を生成（1インスタンスのみ保持）
		var memoStatus = SeriesMemoViewModel.FromSeries(bindingSeries.Series);
		this.MemoStatus = new BindableReactiveProperty<SeriesMemoViewModel>(memoStatus)
			.AddTo(ref this.disposableBag);

		// memoStatus は BindableReactiveProperty に保持されるため、ここでは管理不要だが
		// IDisposable であるため、Dispose 時に破棄されるよう disposableBag に登録
		memoStatus.AddTo(ref this.disposableBag);

		// HasSynopsis を Series から計算される ReactiveProperty として公開
		// Series が変更される（ForceNotify を含む）たびに、現在の Description から再計算される
		this.HasSynopsis = this.Series
			.Select(series => !string.IsNullOrWhiteSpace(series.Description))
			.ToReadOnlyBindableReactiveProperty(false)
			.AddTo(ref this.disposableBag);

		// Series 通知を SeriesVolumeStatusViewModel.Series へ流す
		// 同一インスタンスの ForceNotify() にも対応するため、通知が来たら内容をチェック
		this.Series.Subscribe(newSeries =>
		{
			// 新しい Series インスタンスが設定された場合は、VolumeStatus.Value.Series.Value に設定
			// 同一インスタンスの ForceNotify() 時も、ここに到達する
			if (this.VolumeStatus.Value.Series.Value != newSeries)
			{
				this.VolumeStatus.Value.Series.Value = newSeries;
			}
			else if (this.VolumeStatus.Value.Series.Value == newSeries)
			{
				// 同一インスタンスの場合は ForceNotify() で再通知させる
				this.VolumeStatus.Value.Series.ForceNotify();
			}

			// メモ表示用の通知も同じ形式で流す
			if (this.MemoStatus.Value.Series.Value != newSeries)
			{
				this.MemoStatus.Value.Series.Value = newSeries;
			}
			else if (this.MemoStatus.Value.Series.Value == newSeries)
			{
				// 同一インスタンスの場合は ForceNotify() で再通知させる
				this.MemoStatus.Value.Series.ForceNotify();
			}
		})
		.AddTo(ref this.disposableBag);

		// TagDisplayText を共有 Notifier から導出する
		// タグの追加・削除は Tags の変更通知、名称変更は先頭の共有 TagNotifier.Tag の通知で再計算する
		// 書式は先頭タグ名と件数のみに依存するため、先頭タグだけを購読すれば足りる
		var seriesNotifier = bindingSeries.SeriesNotifier;
		this.TagDisplayText = seriesNotifier.Tags
			.ObserveChanged()
			.Select(_ => Unit.Default)
			.Prepend(Unit.Default)
			.Select(_ => seriesNotifier.Tags.Count == 0
				? Observable.Return(Unit.Default)
				: seriesNotifier.Tags[0].Tag.AsObservable().Select(_ => Unit.Default))
			.Switch()
			.Select(_ => SeriesTagDisplayFormatter.FormatForStartPage(
				seriesNotifier.Tags.Select(tagNotifier => tagNotifier.Tag.Value)))
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
