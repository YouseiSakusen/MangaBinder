using ObservableCollections;
using R3;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MangaBinder.Series;

/// <summary>
/// 作品管理ページの ViewModel です。
/// </summary>
public class MaintenancePageViewModel : IDisposable, IDataInitializable
{
	private DisposableBag disposableBag;

	/// <summary>検索文字列を取得します。</summary>
	public BindableReactiveProperty<string> SearchQuery { get; }

	/// <summary>検索結果の作品一覧を取得します。</summary>
	public ObservableCollection<MangaSeries> SearchResults { get; }

	/// <summary>検索結果が空であるかを取得します。</summary>
	public BindableReactiveProperty<bool> IsSearchResultsEmpty { get; }

	/// <summary>検索結果が存在するかを取得します（IsSearchResultsEmpty の反対）。</summary>
	public BindableReactiveProperty<bool> HasSearchResults { get; }

	/// <summary>検索実行コマンドです。</summary>
	public ReactiveCommand<Unit> SearchCommand { get; }

	/// <summary>
	/// <see cref="MaintenancePageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	public MaintenancePageViewModel()
	{
		this.SearchQuery = new BindableReactiveProperty<string>(string.Empty)
			.AddTo(ref this.disposableBag);

		this.SearchResults = new ObservableCollection<MangaSeries>();

		this.IsSearchResultsEmpty = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		this.HasSearchResults = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.SearchCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		// SearchCommand の実装（現在は空実装）
		this.SearchCommand.Subscribe(_ => this.executeSearchAsync());

		// SearchResults の CollectionChanged を監視して状態を更新
		this.SearchResults.CollectionChanged += (sender, e) =>
		{
			this.UpdateEmptyState();
		};
	}

	/// <summary>
	/// 画面表示後の初期データ読み込みを非同期で実行します。
	/// </summary>
	public async ValueTask InitializeDataAsync()
	{
		// Empty State の初期状態を確実に設定
		this.UpdateEmptyState();
		await ValueTask.CompletedTask;
	}

	/// <summary>
	/// エンプティステートを更新します。
	/// </summary>
	private void UpdateEmptyState()
	{
		var isEmpty = this.SearchResults.Count == 0;
		this.IsSearchResultsEmpty.Value = isEmpty;
		this.HasSearchResults.Value = !isEmpty;
	}

	/// <summary>
	/// 検索を実行します（現在は空実装）。
	/// </summary>
	private async ValueTask executeSearchAsync()
	{
		// 検索処理はまだ実装されていません
		await ValueTask.CompletedTask;
	}

	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}
