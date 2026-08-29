using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 複数の既存作品候補から1件を選択するダイアログ用 ViewModel です。
/// SelectableSeriesList を利用して、候補作品一覧を表示・選択できます。
/// </summary>
public class MultipleExistingSeriesDialogContentViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 作品一覧表示用の共通 UserControl の ViewModel。
	/// </summary>
	public SelectableSeriesListViewModel SelectableSeriesListViewModel { get; }

	/// <summary>
	/// <see cref="MultipleExistingSeriesDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="candidates">候補となる MangaSeries の一覧。</param>
	public MultipleExistingSeriesDialogContentViewModel(IReadOnlyList<MangaSeries> candidates)
	{
		// SelectableSeriesListViewModel を生成
		this.SelectableSeriesListViewModel = new SelectableSeriesListViewModel()
			.AddTo(ref this.disposableBag);

		// ナビゲーションボタンを非表示
		this.SelectableSeriesListViewModel.ShowNavigateButton.Value = false;

		// 候補作品一覧を設定
		this.SelectableSeriesListViewModel.SetSource(candidates);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
