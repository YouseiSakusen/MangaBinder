using MangaBinder.Controls;
using R3;
using Reactive.Bindings.R3;

namespace MangaBinder.Series;

/// <summary>
/// EditorPage 用の巻情報編集ViewModel です。
/// 入力中の巻情報値をリアルタイム反映し、表示用の SeriesVolumeStatusViewModel を自動更新します。
/// </summary>
public class EditorSeriesVolumeStatusViewModel : IDisposable
{
	private DisposableBag disposableBag;

	/// <summary>完結巻入力中の表示値です。リアルタイム表示用です。</summary>
	private string? displayEndVolumeText;

	/// <summary>所持推定巻数入力中の表示値です。リアルタイム表示用です。</summary>
	private string? displayOwnedMaxVolumeText;

	/// <summary>開始巻を取得または設定します。</summary>
	public BindableReactiveProperty<double> StartVolume { get; }

	/// <summary>完結巻を取得または設定します。null の場合は未入力です。</summary>
	public BindableReactiveProperty<double?> EndVolume { get; }

	/// <summary>所持推定巻数を取得または設定します。</summary>
	public BindableReactiveProperty<double?> OwnedMaxVolume { get; }

	/// <summary>全巻所持しているかどうかを取得または設定します。</summary>
	public BindableReactiveProperty<bool> IsOwnedCompleted { get; }

	/// <summary>全巻所持が編集可能かどうかを取得または設定します。完結巻が null ではない場合のみ編集可能です。</summary>
	public BindableReactiveProperty<bool> CanEditOwnedCompleted { get; }

	/// <summary>製本済み最終巻を取得または設定します。表示用です。</summary>
	public BindableReactiveProperty<double> BoundEndVolume { get; }

	/// <summary>完結巻入力中テキストを処理し、CanEditOwnedCompleted を制御するコマンドを取得します。</summary>
	public ReactiveCommand<string?> EndVolumeTextInputCommand { get; }

	/// <summary>所持推定巻数入力中テキストを処理し、リアルタイム表示を更新するコマンドを取得します。</summary>
	public ReactiveCommand<string?> OwnedMaxVolumeTextInputCommand { get; }

	/// <summary>表示用の巻情報ViewModel を取得または設定します。入力値変更時に自動更新されます。</summary>
	public BindableReactiveProperty<SeriesVolumeStatusViewModel?> DisplayStatus { get; }

	/// <summary>
	/// <see cref="EditorSeriesVolumeStatusViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	public EditorSeriesVolumeStatusViewModel()
	{
		this.StartVolume = new BindableReactiveProperty<double>(1.0)
			.AddTo(ref this.disposableBag);

		this.EndVolume = new BindableReactiveProperty<double?>(null)
			.AddTo(ref this.disposableBag);

		this.OwnedMaxVolume = new BindableReactiveProperty<double?>(null)
			.AddTo(ref this.disposableBag);

		this.IsOwnedCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.CanEditOwnedCompleted = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.BoundEndVolume = new BindableReactiveProperty<double>(0)
			.AddTo(ref this.disposableBag);

		this.DisplayStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel?>(null)
			.AddTo(ref this.disposableBag);

		// EndVolume の変更を監視し、CanEditOwnedCompleted を制御
		this.EndVolume
			.Subscribe(endVolume =>
			{
				if (endVolume == null)
				{
					// 完結巻が null になった場合
					this.CanEditOwnedCompleted.Value = false;
					this.IsOwnedCompleted.Value = false;
				}
				else
				{
					// 完結巻が入力された場合
					this.CanEditOwnedCompleted.Value = true;
				}

				// DisplayStatus を更新
				this.updateDisplayStatus();
			})
			.AddTo(ref this.disposableBag);

		// OwnedMaxVolume の変更を監視して DisplayStatus を更新
		this.OwnedMaxVolume
			.Subscribe(_ => this.updateDisplayStatus())
			.AddTo(ref this.disposableBag);

		// IsOwnedCompleted の変更を監視して DisplayStatus を更新
		this.IsOwnedCompleted
			.Subscribe(_ => this.updateDisplayStatus())
			.AddTo(ref this.disposableBag);

		// BoundEndVolume の変更を監視して DisplayStatus を更新
		this.BoundEndVolume
			.Subscribe(_ => this.updateDisplayStatus())
			.AddTo(ref this.disposableBag);

		// EndVolumeTextInputCommand: 完結巻入力中テキストを処理するコマンド
		this.EndVolumeTextInputCommand = new ReactiveCommand<string?>()
			.AddTo(ref this.disposableBag);
		this.EndVolumeTextInputCommand.Subscribe(text =>
		{
			this.handleEndVolumeTextInput(text);
		});

		// OwnedMaxVolumeTextInputCommand: 所持推定巻数入力中テキストを処理するコマンド
		this.OwnedMaxVolumeTextInputCommand = new ReactiveCommand<string?>()
			.AddTo(ref this.disposableBag);
		this.OwnedMaxVolumeTextInputCommand.Subscribe(text =>
		{
			this.handleOwnedMaxVolumeTextInput(text);
		});
	}

	/// <summary>
	/// MangaSeries から巻情報を読み込みます。
	/// </summary>
	/// <param name="series">読み込み対象の MangaSeries。</param>
	public void LoadFromSeries(MangaSeries series)
	{
		this.StartVolume.Value = series.StartVolume;
		this.EndVolume.Value = series.EndVolume > 0 ? series.EndVolume : null;
		this.OwnedMaxVolume.Value = series.OwnedMaxVolume > 0 ? series.OwnedMaxVolume : null;
		this.BoundEndVolume.Value = series.BoundEndVolume;
		this.IsOwnedCompleted.Value = series.IsOwnedCompleted;

		// 入力中の表示値をクリア
		this.displayEndVolumeText = null;
		this.displayOwnedMaxVolumeText = null;

		// CanEditOwnedCompleted は、VolumeStatus 側へ設定した EndVolume 値を基準に判定
		this.CanEditOwnedCompleted.Value = this.EndVolume.Value != null;

		// DisplayStatus を更新
		this.updateDisplayStatus();
	}

	/// <summary>
	/// 表示用の巻情報ViewModel を更新します。
	/// </summary>
	private void updateDisplayStatus()
	{
		// 表示用の一時的なデータを組み立て
		// 入力中の表示値がある場合はそれを優先し、ない場合は保存値を使用

		// 完結巻の表示テキスト
		string totalVolumeText;
		if (this.displayEndVolumeText != null)
		{
			// 入力中の表示値を使用
			if (double.TryParse(this.displayEndVolumeText, out var value) && value >= 1.0)
			{
				totalVolumeText = $"全{value}巻";
			}
			else
			{
				// 無効な入力は"-"で表示
				totalVolumeText = "-";
			}
		}
		else
		{
			// 保存値を使用
			totalVolumeText = this.EndVolume.Value.HasValue
				? $"全{this.EndVolume.Value}巻"
				: "-";
		}

		// 所持推定巻数の表示テキスト
		string ownedEstimatedVolumeText;
		if (this.displayOwnedMaxVolumeText != null)
		{
			// 入力中の表示値を使用
			if (double.TryParse(this.displayOwnedMaxVolumeText, out var value) && value > 0)
			{
				ownedEstimatedVolumeText = $"所持推定：{value}";
			}
			else
			{
				// 無効な入力は"所持推定：-"で表示
				ownedEstimatedVolumeText = "所持推定：-";
			}
		}
		else
		{
			// 保存値を使用
			ownedEstimatedVolumeText = this.OwnedMaxVolume.Value.HasValue && this.OwnedMaxVolume.Value > 0
				? $"所持推定：{this.OwnedMaxVolume.Value}"
				: "所持推定：-";
		}

		var boundEndVolumeText = this.BoundEndVolume.Value > 0
			? $"製本済み：{this.BoundEndVolume.Value}"
			: "製本済み：-";

		var seriesCompleted = this.EndVolume.Value.HasValue;

		this.DisplayStatus.Value = new SeriesVolumeStatusViewModel
		{
			TotalVolumeText = totalVolumeText,
			OwnedEstimatedVolumeText = ownedEstimatedVolumeText,
			BoundEndVolumeText = boundEndVolumeText,
			SeriesCompleted = seriesCompleted,
			IsOwnedCompleted = this.IsOwnedCompleted.Value,
		};
	}

	/// <summary>
	/// 完結巻入力中のテキストを処理し、CanEditOwnedCompleted を制御し、リアルタイム表示を更新します。
	/// </summary>
	/// <param name="text">入力中のテキスト。</param>
	private void handleEndVolumeTextInput(string? text)
	{
		// 入力中テキストが1以上の数値なら CanEditOwnedCompleted = true
		// 空、無効値、1未満なら CanEditOwnedCompleted = false、IsOwnedCompleted = false
		if (double.TryParse(text, out var value) && value >= 1.0)
		{
			this.CanEditOwnedCompleted.Value = true;
		}
		else
		{
			this.CanEditOwnedCompleted.Value = false;
			this.IsOwnedCompleted.Value = false;
		}

		// 入力中の表示値を保持
		this.displayEndVolumeText = text;

		// リアルタイム表示を更新
		this.updateDisplayStatus();
	}

	/// <summary>
	/// 所持推定巻数入力中のテキストを処理し、リアルタイム表示を更新します。
	/// </summary>
	/// <param name="text">入力中のテキスト。</param>
	private void handleOwnedMaxVolumeTextInput(string? text)
	{
		// 入力中の表示値を保持
		this.displayOwnedMaxVolumeText = text;

		// リアルタイム表示を更新
		this.updateDisplayStatus();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
