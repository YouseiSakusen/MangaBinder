using MangaBinder.Series;
using R3;

namespace MangaBinder.Controls;

/// <summary>
/// 既存作品候補（1件以上）を表示するダイアログ用 ViewModel です。
/// SelectableSeriesList を利用して、候補作品一覧を表示します。
/// </summary>
public class ExistingSeriesDialogContentViewModel : IDisposable
{
	private DisposableBag disposableBag = new();

	/// <summary>
	/// 表示対象の MangaSeries 候補一覧。
	/// </summary>
	public IReadOnlyList<MangaSeries> Candidates { get; }

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
	/// 作者入力欄へのフォーカス要求カウンタ。
	/// </summary>
	public BindableReactiveProperty<int> AuthorFocusRequest { get; }

	/// <summary>
	/// <see cref="ExistingSeriesDialogContentViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="candidates">表示対象の MangaSeries 候補一覧（1件以上）。</param>
	public ExistingSeriesDialogContentViewModel(IReadOnlyList<MangaSeries> candidates)
	{
		this.Candidates = candidates;

		// SelectableSeriesListViewModel を生成
		this.SelectableSeriesListViewModel = new SelectableSeriesListViewModel()
			.AddTo(ref this.disposableBag);

		// CardSize を Large に設定（既存作品Dialog用）
		this.SelectableSeriesListViewModel.CardSize.Value = SeriesCardSize.Large;

		// ナビゲーションボタンを非表示
		this.SelectableSeriesListViewModel.ShowNavigateButton.Value = false;

		// 候補作品一覧を設定
		this.SelectableSeriesListViewModel.SetSource(candidates);

		// Items に初期要素が現れたタイミングで先頭作品を選択状態に設定（初回のみ）
		this.SelectableSeriesListViewModel.Items
			.Where(items => items.Count > 0)
			.Take(1)
			.Subscribe(items =>
			{
				var firstCardViewModel = items.FirstOrDefault();
				if (firstCardViewModel != null)
				{
					this.SelectableSeriesListViewModel.SelectedItem.Value = firstCardViewModel;
				}
			})
			.AddTo(ref this.disposableBag);

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

		// 作者入力欄へのフォーカス要求カウンタを初期化（初期値は 0）
		this.AuthorFocusRequest = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		// 「別作者の作品として追加」選択時にフォーカスカウンタをインクリメント
		this.IsAddAsOtherAuthorSelected
			.Where(x => x)
			.Subscribe(_ =>
			{
				this.AuthorFocusRequest.Value++;
			})
			.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
