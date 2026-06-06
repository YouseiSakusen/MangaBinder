using ObservableCollections;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Binding;

/// <summary>
/// 製本工程対象作品一覧画面の ViewModel です。
/// </summary>
public class StartPageViewModel : IDisposable, IDataInitializable
{
	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>コンテントダイアログサービス。</summary>
	private readonly IContentDialogService contentDialogService;

	/// <summary>製本開始状態 Dispatcher。</summary>
	private readonly BindingQueueDispatcher bindingQueueDispatcher;

	/// <summary>製本ワークスペース ストア。</summary>
	private readonly SeriesWorkspaceStore workspaceStore;

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

	/// <summary>VolumeSelectionPage へ遷移するコマンドです。</summary>
	public ReactiveCommand<BindingSeries> NavigateToVolumeSelectionCommand { get; }

	/// <summary>製本待ちをクリアするコマンドです。</summary>
	public ReactiveCommand ClearBindingQueueCommand { get; }

	/// <summary>
	/// <see cref="StartPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	public StartPageViewModel(INavigationService navigationService, IContentDialogService contentDialogService, BindingQueueDispatcher bindingQueueDispatcher, SeriesWorkspaceStore workspaceStore)
	{
		this.navigationService = navigationService;
		this.contentDialogService = contentDialogService;
		this.bindingQueueDispatcher = bindingQueueDispatcher;
		this.workspaceStore = workspaceStore;

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

		this.NavigateToVolumeSelectionCommand = new ReactiveCommand<BindingSeries>()
			.AddTo(ref this.disposableBag);
		this.NavigateToVolumeSelectionCommand.Subscribe(bindingSeries => this.NavigateToVolumeSelection(bindingSeries));

		this.ClearBindingQueueCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.ClearBindingQueueCommand.Subscribe(_ => this.executeClearBindingQueueAsync());
	}

	/// <inheritdoc/>
	public ValueTask InitializeDataAsync()
	{
		this.series.Clear();
		this.series.AddRange(this.bindingQueueDispatcher.GetAll());
		this.updateState();
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// VolumeSelectionPage へ遷移します。指定された作品を製本対象として設定します。
	/// </summary>
	/// <param name="bindingSeries">遷移対象の作品。</param>
	private void NavigateToVolumeSelection(BindingSeries bindingSeries)
	{
		var series = bindingSeries.Series;

		// BindingTarget を設定
		this.workspaceStore.SetBindingTarget(series);

		// 互換維持のため SelectedSeries にも同じ1作品をセット
		this.workspaceStore.SelectedSeries.Clear();
		this.workspaceStore.SelectedSeries.Add(series);

		// VolumeSelectionPage へナビゲート
		this.navigationService.NavigateWithHierarchy(typeof(VolumeSelectionPage));
	}

	private void updateState()
	{
		var count = this.series.Count;
		this.SelectedSeriesCount.Value = count;
		this.IsEmpty.Value = count == 0;
		this.IsNotEmpty.Value = count > 0;
	}

	/// <summary>
	/// 製本待ち一覧をクリアする処理を実行します。
	/// </summary>
	private async void executeClearBindingQueueAsync()
	{
		var result = await this.showConfirmationDialogAsync();
		if (result != ContentDialogResult.Primary)
			return;

		// 全件削除
		this.bindingQueueDispatcher.ReplaceAll(new List<BindingSeries>());
		this.series.Clear();
		this.updateState();
	}

	/// <summary>
	/// 製本待ち一覧クリアの確認ダイアログを表示します。
	/// </summary>
	private async Task<ContentDialogResult> showConfirmationDialogAsync()
	{
		var dialog = new ContentDialog
		{
			Title = "確認",
			Content = "製本待ちの作品をすべてクリアしますか？",
			PrimaryButtonText = "クリア",
			SecondaryButtonText = "戻る",
		};
		return await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}


