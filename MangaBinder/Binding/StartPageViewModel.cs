using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using Wpf.Ui;

namespace MangaBinder.Binding;

/// <summary>
/// 製本工程対象作品一覧画面の ViewModel です。
/// </summary>
public class StartPageViewModel : IDisposable, IDataInitializable
{
	/// <summary>スコープファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	private DisposableBag disposableBag;

	/// <summary>内部保持する BindingStartSeries コレクション。</summary>
	private readonly ObservableList<BindingStartSeries> series;

	/// <summary>
	/// ListView にバインドする BindingStartSeries の一覧を取得します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<BindingStartSeries> Series { get; }

	/// <summary>
	/// BindingQueue 登録件数を取得します。
	/// </summary>
	public ReadOnlyReactiveProperty<int> SelectedSeriesCount { get; }

	/// <summary>
	/// BindingQueue が空かどうかを取得します。
	/// </summary>
	public ReadOnlyReactiveProperty<bool> IsEmpty { get; }

	/// <summary>
	/// BindingQueue に1件以上登録されているかどうかを取得します。
	/// </summary>
	public ReadOnlyReactiveProperty<bool> IsNotEmpty { get; }

	/// <summary>HomePage へ遷移するコマンドです。</summary>
	public ReactiveCommand<Unit> NavigateToHomeCommand { get; }

	/// <summary>
	/// <see cref="StartPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	public StartPageViewModel(IServiceScopeFactory serviceScopeFactory, INavigationService navigationService)
	{
		this.serviceScopeFactory = serviceScopeFactory;
		this.navigationService = navigationService;

		this.series = new ObservableList<BindingStartSeries>();

		this.Series = this.series
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.SelectedSeriesCount = this.series
			.ObserveCountChanged(notifyCurrentCount: true)
			.ToReadOnlyReactiveProperty(this.series.Count)
			.AddTo(ref this.disposableBag);

		this.IsEmpty = this.SelectedSeriesCount
			.Select(c => c == 0)
			.ToReadOnlyReactiveProperty(true)
			.AddTo(ref this.disposableBag);

		this.IsNotEmpty = this.SelectedSeriesCount
			.Select(c => c > 0)
			.ToReadOnlyReactiveProperty(false)
			.AddTo(ref this.disposableBag);

		this.NavigateToHomeCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.NavigateToHomeCommand.Subscribe(_ => this.navigationService.Navigate(typeof(HomePage)));
	}

	/// <inheritdoc/>
	public async ValueTask InitializeDataAsync()
	{
		this.series.Clear();

		using var scope = this.serviceScopeFactory.CreateScope();
		var repository = scope.ServiceProvider.GetRequiredService<BindingStartRepository>();

		var result = await repository.GetQueuedSeriesAsync();
		this.series.AddRange(result);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}


