using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 巻情報表示用の ViewModel です。
/// MangaSeries からの入力を受け取り、その変更を自動的に表示値に反映します。
/// </summary>
public class SeriesVolumeStatusViewModel : IDisposable
{
	/// <summary>リソース管理用の DisposableBag。</summary>
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 表示対象となる MangaSeries を取得・設定します。
	/// 値が変更（または ForceNotify() が呼ばれた）際に、表示値が自動更新されます。
	/// </summary>
	public BindableReactiveProperty<MangaSeries?> Series { get; }

	/// <summary>全巻数テキストです。完結していない場合は "-" を返します。</summary>
	public BindableReactiveProperty<string> TotalVolumeText { get; }

	/// <summary>所持推定巻数テキストです。</summary>
	public BindableReactiveProperty<string> OwnedEstimatedVolumeText { get; }

	/// <summary>製本済み最終巻テキストです。</summary>
	public BindableReactiveProperty<string> BoundEndVolumeText { get; }

	/// <summary>作品が完結しているかを示します。バッジ背景色判定用です。</summary>
	public BindableReactiveProperty<bool> SeriesCompleted { get; }

	/// <summary>全巻所持済みかを示します。バッジ背景色判定用です。</summary>
	public BindableReactiveProperty<bool> IsOwnedCompleted { get; }

	/// <summary>
	/// <see cref="SeriesVolumeStatusViewModel"/> の新しいインスタンスを初期化します。
	/// 初期状態は空の表示値を持ちます。
	/// </summary>
	public SeriesVolumeStatusViewModel()
	{
		this.Series = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		this.TotalVolumeText = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.OwnedEstimatedVolumeText = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.BoundEndVolumeText = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.SeriesCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.IsOwnedCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// Series の値変更を監視し、表示値を更新する
		this.Series
			.Subscribe(series => this.updateDisplayValues(series))
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// MangaSeries から表示値を生成し、各プロパティに設定します。
	/// </summary>
	/// <param name="series">表示対象の MangaSeries。null の場合は空の表示値に設定されます。</param>
	private void updateDisplayValues(MangaSeries? series)
	{
		if (series == null)
		{
			this.TotalVolumeText.Value = string.Empty;
			this.OwnedEstimatedVolumeText.Value = string.Empty;
			this.BoundEndVolumeText.Value = string.Empty;
			this.SeriesCompleted.Value = false;
			this.IsOwnedCompleted.Value = false;
			return;
		}

		this.TotalVolumeText.Value = series.SeriesCompleted
			? $"全{series.EndVolume}巻"
			: "-";

		this.OwnedEstimatedVolumeText.Value = series.OwnedMaxVolume > 0
			? $"所持推定：{series.OwnedMaxVolume}"
			: "所持推定：-";

		this.BoundEndVolumeText.Value = series.BoundEndVolume > 0
			? $"製本済み：{series.BoundEndVolume}"
			: "製本済み：-";

		this.SeriesCompleted.Value = series.SeriesCompleted;

		this.IsOwnedCompleted.Value = series.IsOwnedCompleted;
	}

	/// <summary>
	/// <see cref="MangaSeries"/> から <see cref="SeriesVolumeStatusViewModel"/> を生成します。
	/// このメソッドは従来の利用方法との互換性を保つため提供されています。
	/// </summary>
	/// <param name="series">変換元の MangaSeries。</param>
	/// <returns>生成された ViewModel。Series プロパティに series が設定された状態。</returns>
	public static SeriesVolumeStatusViewModel FromSeries(MangaSeries series)
	{
		var viewModel = new SeriesVolumeStatusViewModel();
		viewModel.Series.Value = series;
		return viewModel;
	}

	/// <summary>リソースを解放します。</summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
