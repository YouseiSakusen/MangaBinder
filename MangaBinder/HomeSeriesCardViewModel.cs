using MangaBinder.Bindings;
using MangaBinder.Controls;
using MangaBinder.Series;
using R3;

namespace MangaBinder;

/// <summary>
/// Home 画面の ListView アイテム表示用 ViewModel です。MangaSeries をラップしています。
/// </summary>
public class HomeSeriesCardViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 共有 <see cref="MangaSeriesNotifier"/> です。<see cref="MangaSeriesStore"/> が所有しているため、ここで Dispose してはいけません。
	/// タグ表示ボタンへ渡します。製本待ちのチェック状態は <see cref="MangaSeriesNotifier.IsBindingQueued"/> を参照します。
	/// </summary>
	public MangaSeriesNotifier SeriesNotifier { get; }

	/// <summary>
	/// 基になった MangaSeries です。
	/// ReactiveProperty として、値の変更を監視できます。
	/// </summary>
	public BindableReactiveProperty<MangaSeries> Series { get; }

	/// <summary>
	/// 巻情報表示用の ViewModel です。
	/// </summary>
	public BindableReactiveProperty<SeriesVolumeStatusViewModel> VolumeStatus { get; }

	/// <summary>
	/// メモ表示用の ViewModel です。
	/// </summary>
	public BindableReactiveProperty<SeriesMemoViewModel> MemoStatus { get; }

	/// <summary>
	/// 作品最終更新日時表示用の ViewModel です。
	/// </summary>
	public BindableReactiveProperty<SeriesLastUpdateViewModel> SeriesLastUpdateViewModel { get; }

	/// <summary>
	/// 素材フォルダ作成日時表示用の ViewModel です。
	/// </summary>
	public BindableReactiveProperty<MaterialFolderCreatedAtViewModel> MaterialFolderCreatedAtViewModel { get; }

	/// <summary>
	/// <see cref="HomeSeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="seriesViewModel">ラップする MangaSeriesNotifier。</param>
	public HomeSeriesCardViewModel(MangaSeriesNotifier seriesViewModel)
	{
		this.SeriesNotifier = seriesViewModel;

		// 共有 MangaSeriesNotifier.Series をそのまま参照（新規生成しない）
		this.Series = seriesViewModel.Series;

		// 巻情報表示用の SeriesVolumeStatusViewModel を生成（1インスタンスのみ保持）
		var volumeStatus = SeriesVolumeStatusViewModel.FromSeries(this.Series.Value);
		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel>(volumeStatus)
			.AddTo(ref this.disposableBag);

		// volumeStatus は BindableReactiveProperty に保持されるため、ここでは管理不要だが
		// IDisposable であるため、Dispose 時に破棄されるよう disposableBag に登録
		volumeStatus.AddTo(ref this.disposableBag);

		// メモ表示用の SeriesMemoViewModel を生成（1インスタンスのみ保持）
		var memoStatus = SeriesMemoViewModel.FromSeries(this.Series.Value);
		this.MemoStatus = new BindableReactiveProperty<SeriesMemoViewModel>(memoStatus)
			.AddTo(ref this.disposableBag);

		// memoStatus は BindableReactiveProperty に保持されるため、ここでは管理不要だが
		// IDisposable であるため、Dispose 時に破棄されるよう disposableBag に登録
		memoStatus.AddTo(ref this.disposableBag);

		// 作品最終更新日時表示用の SeriesLastUpdateViewModel を生成（1インスタンスのみ保持）
		var seriesLastUpdateViewModel = new SeriesLastUpdateViewModel();
		this.SeriesLastUpdateViewModel = new BindableReactiveProperty<SeriesLastUpdateViewModel>(seriesLastUpdateViewModel)
			.AddTo(ref this.disposableBag);

		seriesLastUpdateViewModel.AddTo(ref this.disposableBag);

		// 素材フォルダ作成日時表示用の MaterialFolderCreatedAtViewModel を生成（1インスタンスのみ保持）
		var materialFolderCreatedAtViewModel = new MaterialFolderCreatedAtViewModel();
		this.MaterialFolderCreatedAtViewModel = new BindableReactiveProperty<MaterialFolderCreatedAtViewModel>(materialFolderCreatedAtViewModel)
			.AddTo(ref this.disposableBag);

		materialFolderCreatedAtViewModel.AddTo(ref this.disposableBag);

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

			// 作品最終更新日時表示用の通知も同じ形式で流す
			if (this.SeriesLastUpdateViewModel.Value.Series.Value != newSeries)
			{
				this.SeriesLastUpdateViewModel.Value.Series.Value = newSeries;
			}
			else if (this.SeriesLastUpdateViewModel.Value.Series.Value == newSeries)
			{
				// 同一インスタンスの場合は ForceNotify() で再通知させる
				this.SeriesLastUpdateViewModel.Value.Series.ForceNotify();
			}

			// 素材フォルダ作成日時表示用の通知も同じ形式で流す
			if (this.MaterialFolderCreatedAtViewModel.Value.Series.Value != newSeries)
			{
				this.MaterialFolderCreatedAtViewModel.Value.Series.Value = newSeries;
			}
			else if (this.MaterialFolderCreatedAtViewModel.Value.Series.Value == newSeries)
			{
				// 同一インスタンスの場合は ForceNotify() で再通知させる
				this.MaterialFolderCreatedAtViewModel.Value.Series.ForceNotify();
			}
		})
		.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
