using ObservableCollections;
using R3;
using Wpf.Ui;
using MangaBinder.Helpers;
using Microsoft.Extensions.DependencyInjection;
using MangaBinder.Series;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本工程対象作品一覧画面の ViewModel です。
/// </summary>
public class StartPageViewModel : IDisposable, IDataInitializable
{
	/// <summary>スコープファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>コンテントダイアログサービス。</summary>
	private readonly IContentDialogService contentDialogService;

	/// <summary>製本ワークスペース ストア。</summary>
	private readonly SeriesWorkspaceStore workspaceStore;

	/// <summary>製本開始キュー ストア。</summary>
	private readonly BindingQueueStore bindingQueueStore;

	/// <summary>表示用作品一覧の内部バッファ。</summary>
	private readonly ObservableList<StartPageSeriesCardViewModel> displaySeriesSource = new();

	private DisposableBag disposableBag;

	/// <summary>
	/// ListView にバインドする表示用アイテムの一覧を取得します。
	/// </summary>
	public NotifyCollectionChangedSynchronizedViewList<StartPageSeriesCardViewModel> Series { get; }

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
	/// 素材フォルダを開くコマンドです。<see cref="MangaSource"/> をパラメータとして受け取ります。
	/// </summary>
	public ReactiveCommand<MangaSource> OpenMaterialFolderCommand { get; }

	/// <summary>
	/// 製本待ちから削除するコマンドです。<see cref="BindingSeries"/> をパラメータとして受け取ります。
	/// </summary>
	public ReactiveCommand<BindingSeries> RemoveFromQueueCommand { get; }

	/// <summary>
	/// 作品一覧 ListView の VerticalOffset の保存値を取得します。
	/// </summary>
	public BindableReactiveProperty<double> SavedBindingListVerticalOffset { get; }

	/// <summary>
	/// <see cref="StartPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="workspaceStore">製本ワークスペース ストア。</param>
	/// <param name="bindingQueueStore">製本開始キュー ストア。</param>
	public StartPageViewModel(IServiceScopeFactory serviceScopeFactory, INavigationService navigationService, IContentDialogService contentDialogService, SeriesWorkspaceStore workspaceStore, BindingQueueStore bindingQueueStore)
	{
		this.serviceScopeFactory = serviceScopeFactory;
		this.navigationService = navigationService;
		this.contentDialogService = contentDialogService;
		this.workspaceStore = workspaceStore;
		this.bindingQueueStore = bindingQueueStore;

		// bindingQueueStore.Queue の変更を監視して displaySeriesSource へ反映
		this.bindingQueueStore.Queue.ObserveAdd()
			.Subscribe(x => this.displaySeriesSource.Add(new StartPageSeriesCardViewModel(x.Value)))
			.AddTo(ref this.disposableBag);
		this.bindingQueueStore.Queue.ObserveRemove()
			.Subscribe(x => this.displaySeriesSource.RemoveAt(x.Index))
			.AddTo(ref this.disposableBag);
		this.bindingQueueStore.Queue.ObserveReset()
			.Subscribe(_ => this.displaySeriesSource.Clear())
			.AddTo(ref this.disposableBag);

		// 初期要素を追加
		foreach (var bindingSeries in this.bindingQueueStore.Queue)
		{
			this.displaySeriesSource.Add(new StartPageSeriesCardViewModel(bindingSeries));
		}

		this.Series = this.displaySeriesSource.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
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

		this.OpenMaterialFolderCommand = new ReactiveCommand<MangaSource>()
			.AddTo(ref this.disposableBag);
		this.OpenMaterialFolderCommand.Subscribe(source =>
		{
			_ = this.openMaterialFolderAsync(source);
		});

		this.RemoveFromQueueCommand = new ReactiveCommand<BindingSeries>()
			.AddTo(ref this.disposableBag);
		this.RemoveFromQueueCommand.Subscribe(bindingSeries => this.executeRemoveFromQueue(bindingSeries));

		this.SavedBindingListVerticalOffset = new BindableReactiveProperty<double>(0)
			.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public ValueTask InitializeDataAsync()
	{
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
		var count = this.bindingQueueStore.Queue.Count;
		this.SelectedSeriesCount.Value = count;
		this.IsEmpty.Value = count == 0;
		this.IsNotEmpty.Value = count > 0;
	}

	/// <summary>
	/// 指定した作品を製本待ちから削除します。
	/// </summary>
	/// <param name="bindingSeries">削除対象の作品。</param>
	private void executeRemoveFromQueue(BindingSeries bindingSeries)
	{
		this.bindingQueueStore.Remove(bindingSeries.Series.SeriesId);
		this.updateState();
	}

	/// <summary>
	/// 製本待ち一覧をクリアする処理を実行します。
	/// </summary>
	private async void executeClearBindingQueueAsync()
	{
		var confirmed = await ContentDialogHelper.ShowConfirmAsync(
			this.contentDialogService,
			"製本待ちをクリア",
			"製本待ちの作品をすべてクリアしますか？",
			"クリア");
		if (!confirmed)
			return;

		// 全件削除（BindingQueueStore の Queue を直接クリア）
		this.bindingQueueStore.Queue.Clear();
		this.updateState();
	}

	/// <summary>
	/// 素材フォルダを開きます。
	/// </summary>
	/// <param name="source">開くフォルダの情報。</param>
	private async Task openMaterialFolderAsync(MangaSource source)
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var opener = scope.ServiceProvider.GetRequiredService<MaterialFolderOpener>();
		await opener.OpenAsync(source);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.disposableBag.Dispose();
	}
}


