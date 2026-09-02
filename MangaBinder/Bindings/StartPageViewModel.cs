using ObservableCollections;
using R3;
using Wpf.Ui;
using MangaBinder.Helpers;
using Microsoft.Extensions.DependencyInjection;

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

	/// <summary>製本開始ページ ストア。</summary>
	private readonly StartPageStore startPageStore;

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
	/// <param name="startPageStore">製本開始ページ ストア。</param>
	public StartPageViewModel(IServiceScopeFactory serviceScopeFactory, INavigationService navigationService, IContentDialogService contentDialogService, SeriesWorkspaceStore workspaceStore, BindingQueueStore bindingQueueStore, StartPageStore startPageStore)
	{
		this.serviceScopeFactory = serviceScopeFactory;
		this.navigationService = navigationService;
		this.contentDialogService = contentDialogService;
		this.workspaceStore = workspaceStore;
		this.bindingQueueStore = bindingQueueStore;
		this.startPageStore = startPageStore;

		// StartPageStore が公開する WPF バインド用一覧を使用
		this.Series = this.startPageStore.QueueCards;

		// 初期値を現在のStore.Countから取得
		this.SelectedSeriesCount = new BindableReactiveProperty<int>(this.bindingQueueStore.Queue.Count)
			.AddTo(ref this.disposableBag);

		// Store.Queue.Count の変更を監視して SelectedSeriesCount を自動更新
		this.bindingQueueStore.Queue.ObserveCountChanged()
			.Subscribe(count => this.SelectedSeriesCount.Value = count)
			.AddTo(ref this.disposableBag);

		// 初期値を現在のStore.Countから取得
		this.IsEmpty = new BindableReactiveProperty<bool>(this.bindingQueueStore.Queue.Count == 0)
			.AddTo(ref this.disposableBag);

		// Store.Queue.Count の変更を監視して IsEmpty を自動更新
		this.bindingQueueStore.Queue.ObserveCountChanged()
			.Subscribe(count => this.IsEmpty.Value = count == 0)
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

	/// <summary>
	/// 指定した作品を製本待ちから削除します。
	/// </summary>
	/// <param name="bindingSeries">削除対象の作品。</param>
	private void executeRemoveFromQueue(BindingSeries bindingSeries)
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var dispatcher = scope.ServiceProvider.GetRequiredService<BindingQueueDispatcher>();
		dispatcher.Remove(bindingSeries.Series.SeriesId);
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

		// BindingQueueDispatcher 経由でクリア
		using var scope = this.serviceScopeFactory.CreateScope();
		var dispatcher = scope.ServiceProvider.GetRequiredService<BindingQueueDispatcher>();
		dispatcher.Clear();
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


