using MangaBinder.Series;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 既存作品1件を表示するダイアログ用 ViewModel です。
/// SelectableSeriesList を利用して、作品情報を表示します。
/// </summary>
public class ExistingSeriesDialogContentViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 表示対象の MangaSeries。
	/// </summary>
	public MangaSeries Series { get; }

	/// <summary>
	/// 作品一覧表示用の共通 UserControl の ViewModel。
	/// </summary>
	public SelectableSeriesListViewModel SelectableSeriesListViewModel { get; }

	/// <summary>
	/// 既存作品を開く選択状態。
	/// </summary>
	public BindableReactiveProperty<bool> IsOpenExistingSeriesSelected { get; }

	/// <summary>
	/// 別作者の作品として追加する選択状態。
	/// </summary>
	public BindableReactiveProperty<bool> IsAddAsOtherAuthorSelected { get; }

	/// <summary>
	/// 新規作者の入力値。
	/// </summary>
	public BindableReactiveProperty<string> NewAuthorInput { get; }

	/// <summary>
	/// 作者が重複しているかどうかを示す InfoBar の表示状態。
	/// </summary>
	public BindableReactiveProperty<bool> IsAuthorDuplicateErrorVisible { get; }

	/// <summary>
	/// <see cref="ExistingSeriesDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="existingSeries">表示対象の MangaSeries。</param>
	public ExistingSeriesDialogContentViewModel(MangaSeries existingSeries)
	{
		this.Series = existingSeries;

		// SelectableSeriesListViewModel を生成
		this.SelectableSeriesListViewModel = new SelectableSeriesListViewModel()
			.AddTo(ref this.disposableBag);

		// CardSize を Large に設定（既存作品Dialog用）
		this.SelectableSeriesListViewModel.CardSize.Value = SeriesCardSize.Large;

		// ナビゲーションボタンを非表示
		this.SelectableSeriesListViewModel.ShowNavigateButton.Value = false;

		// 1件の作品を配列に格納して設定
		this.SelectableSeriesListViewModel.SetSource(new[] { existingSeries });

		// UI 選択状態を初期化
		this.IsOpenExistingSeriesSelected = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		this.IsAddAsOtherAuthorSelected = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// 作者入力値を初期化
		this.NewAuthorInput = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		// 作者重複エラー表示状態を初期化（初期値は false）
		this.IsAuthorDuplicateErrorVisible = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
