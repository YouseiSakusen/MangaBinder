using ObservableCollections;
using R3;
using Wpf.Ui;

namespace MangaBinder.Binding;

/// <summary>
/// 製本工程対象作品一覧画面の ViewModel です。
/// </summary>
public class StartPageViewModel : IDisposable, IDataInitializable
{
	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>製本開始状態 Dispatcher。</summary>
	private readonly BindingQueueDispatcher bindingQueueDispatcher;

	private DisposableBag disposableBag;

	/// <summary>内部保持する BindingStartSeries コレクション。</summary>
	private readonly ObservableList<BindingSeries> series;

	/// <summary>
	/// ListView にバインドする BindingStartSeries の一覧を取得します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<BindingSeries> Series { get; }

	/// <summary>
	/// BindingQueue 登録件数を取得します。
	/// </summary>
	public BindableReactiveProperty<int> SelectedSeriesCount { get; }

	/// <summary>
	/// BindingQueue が空かどうかを取得します。
	/// </summary>
	public BindableReactiveProperty<bool> IsEmpty { get; }

	/// <summary>
	/// BindingQueue に1件以上登録されているかどうかを取得します。
	/// </summary>
	public BindableReactiveProperty<bool> IsNotEmpty { get; }

	/// <summary>HomePage へ遷移するコマンドです。</summary>
	public ReactiveCommand<Unit> NavigateToHomeCommand { get; }

	/// <summary>
	/// <see cref="StartPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	public StartPageViewModel(INavigationService navigationService, BindingQueueDispatcher bindingQueueDispatcher)
	{
		this.navigationService = navigationService;
		this.bindingQueueDispatcher = bindingQueueDispatcher;

		this.series = new ObservableList<BindingSeries>();

		this.Series = this.series
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		this.SelectedSeriesCount = new BindableReactiveProperty<int>(0)
			.AddTo(ref this.disposableBag);

		this.IsEmpty = new BindableReactiveProperty<bool>(true)
			.AddTo(ref this.disposableBag);

		this.IsNotEmpty = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		this.NavigateToHomeCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);
		this.NavigateToHomeCommand.Subscribe(_ => this.navigationService.Navigate(typeof(HomePage)));
	}

	/// <inheritdoc/>
	public ValueTask InitializeDataAsync()
	{
		this.series.Clear();
		this.series.AddRange(this.bindingQueueDispatcher.GetAll());
		this.updateState();
		return ValueTask.CompletedTask;
	}

	private void updateState()
	{
		var count = this.series.Count;
		this.SelectedSeriesCount.Value = count;
		this.IsEmpty.Value = count == 0;
		this.IsNotEmpty.Value = count > 0;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}


