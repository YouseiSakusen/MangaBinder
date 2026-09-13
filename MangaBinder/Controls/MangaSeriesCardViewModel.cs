using R3;
using System.Windows.Media;

namespace MangaBinder.Controls;

/// <summary>
/// 作品サムネイルカード用の ViewModel です。
/// 表示対象の作品情報（Header、ThumbnailSource、VolumeStatus）を管理します。
/// </summary>
public class MangaSeriesCardViewModel : IDisposable
{
	/// <summary>リソース管理用の DisposableBag。</summary>
	private DisposableBag disposableBag = new();

	/// <summary>
	/// カード上部に表示する見出しを取得・設定します。
	/// 呼び出し元から変更可能な ReactiveProperty です。
	/// </summary>
	public BindableReactiveProperty<string?> Header { get; }

	/// <summary>
	/// Header が null、空文字、空白のみの場合は false、それ以外は true です。
	/// Header から Reactive に導出されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<bool> HasHeader { get; }

	/// <summary>
	/// カード内に表示するサムネイル画像ソースを取得・設定します。
	/// 呼び出し元が決定した最終的な ImageSource を受け取ります。
	/// </summary>
	public BindableReactiveProperty<ImageSource?> ThumbnailSource { get; }

	/// <summary>
	/// 巻数情報表示用の ViewModel を取得・設定します。
	/// 既存の SeriesVolumeStatusViewModel をそのまま受け取る接続口です。
	/// </summary>
	public BindableReactiveProperty<SeriesVolumeStatusViewModel?> VolumeStatus { get; }

	/// <summary>
	/// カードが現在表示している MangaSeries を取得・設定します。
	/// LastUpdateStatus と MemoStatus の入力元です。
	/// </summary>
	public BindableReactiveProperty<MangaSeries?> Series { get; }

	/// <summary>
	/// SeriesMemo を表示対象とするかどうかを取得・設定します。
	/// 画面によってメモ機能の表示・非表示を制御します。
	/// デフォルト値は true（メモ表示が標準）です。
	/// </summary>
	public BindableReactiveProperty<bool> ShowMemo { get; }

	/// <summary>
	/// 作品最終更新日時表示用の ViewModel を取得します。
	/// MangaSeriesCardViewModel が生成・所有する読み取り専用インスタンスです。
	/// Series は MangaSeriesCardViewModel.Series と同期されます。
	/// </summary>
	public SeriesLastUpdateViewModel LastUpdateStatus { get; }

	/// <summary>
	/// メモ表示用の ViewModel を取得します。
	/// MangaSeriesCardViewModel が生成・所有する読み取り専用インスタンスです。
	/// Series は MangaSeriesCardViewModel.Series と同期されます。
	/// </summary>
	public SeriesMemoViewModel MemoStatus { get; }

	/// <summary>
	/// <see cref="MangaSeriesCardViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	public MangaSeriesCardViewModel()
	{
		this.Header = new BindableReactiveProperty<string?>(null)
			.AddTo(ref this.disposableBag);

		// HasHeader: Header が null、空文字、空白のみの場合は false、それ以外は true
		this.HasHeader = this.Header
			.Select(header => !string.IsNullOrWhiteSpace(header))
			.ToReadOnlyBindableReactiveProperty(false)
			.AddTo(ref this.disposableBag);

		this.ThumbnailSource = new BindableReactiveProperty<ImageSource?>(null)
			.AddTo(ref this.disposableBag);

		this.VolumeStatus = new BindableReactiveProperty<SeriesVolumeStatusViewModel?>(null)
			.AddTo(ref this.disposableBag);

		this.Series = new BindableReactiveProperty<MangaSeries?>(null)
			.AddTo(ref this.disposableBag);

		this.ShowMemo = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		// 子ViewModel の生成（MangaSeriesCardViewModel が所有）
		var lastUpdateStatusViewModel = new SeriesLastUpdateViewModel()
			.AddTo(ref this.disposableBag);
		this.LastUpdateStatus = lastUpdateStatusViewModel;

		var memoStatusViewModel = new SeriesMemoViewModel()
			.AddTo(ref this.disposableBag);
		this.MemoStatus = memoStatusViewModel;

		// Series が変更された場合、その値を子ViewModelへ流す
		this.Series
			.Subscribe(series =>
			{
				lastUpdateStatusViewModel.Series.Value = series;
				memoStatusViewModel.Series.Value = series;
			})
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// リソースを解放します。
	/// DisposableBag に登録された IDisposable オブジェクトが自動的に解放されます。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
