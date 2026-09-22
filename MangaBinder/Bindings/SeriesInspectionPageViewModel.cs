using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Windows;
using MangaBinder.Bindings.Inspection;
using MangaBinder.Bindings.Prepress;
using MangaBinder.Controls;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using Wpf.Ui;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本前確認画面の ViewModel です。
/// </summary>
public class SeriesInspectionPageViewModel : IDisposable, IDataInitializable
{
	/// <summary>作品選択状態ストア。</summary>
	private readonly SeriesWorkspaceStore workspaceStore;

	/// <summary>製本工程の正本状態ストア。</summary>
	private readonly BindingStore bindingStore;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>スコープファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>サムネイル画像ローダー。</summary>
	private readonly ThumbnailImageLoader thumbnailImageLoader;

	/// <summary>ローディングサービス。</summary>
	private readonly LoadingService loadingService;

	private DisposableBag disposableBag;

	/// <summary>選択中の作品名を取得します。</summary>
	public IReadOnlyBindableReactiveProperty<string> SeriesTitle { get; }

	/// <summary>選択中の作品著者を取得します。</summary>
	public IReadOnlyBindableReactiveProperty<string> SeriesAuthor { get; }

	/// <summary>
	/// 作品サムネイルカード用の ViewModel を取得します。
	/// ThumbnailSource と VolumeStatus を管理し、左側パネルのサムネイルカードに使用されます。
	/// </summary>
	public MangaSeriesCardViewModel MangaSeriesCard { get; private set; }

	/// <summary>巻カード表示用の ViewModel 一覧を取得します。</summary>
	public NotifyCollectionChangedSynchronizedViewList<VolumeCardViewModel> VolumeCards { get; }

	/// <summary>内部保持する巻カード用 synchronized view。</summary>
	private ISynchronizedView<BindingVolume, VolumeCardViewModel>? volumeCardsView;

	// zip 設定のプロパティ

	/// <summary>出力 zip ファイル名（編集可能）を取得します。</summary>
	public BindableReactiveProperty<string> ZipOutputFileName => this.bindingStore.ZipOutputFileName;

	/// <summary>選択済み巻数テキスト（「XX巻」形式）を取得します。</summary>
	public IReadOnlyBindableReactiveProperty<string> SelectedVolumeCountText => this.bindingStore.SelectedVolumeCountText;

	/// <summary>製本完了後に対象作品を製本待ちから削除するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> RemoveFromBindingQueueAfterCompletion => this.bindingStore.RemoveFromBindingQueueAfterCompletion;

	// コマンド

	/// <summary>戻るコマンドを取得します。</summary>
	public ReactiveCommand GoBackCommand { get; }

	/// <summary>製本開始コマンドを取得します（現時点はダミー）。</summary>
	public ReactiveCommand StartBindingCommand { get; }

	/// <summary>選択した巻をPrepressで開くコマンドを取得します。</summary>
	public ReactiveCommand<BindingVolume> NavigateToPrepressCommand { get; }

	/// <summary>
	/// <see cref="SeriesInspectionPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="workspaceStore">作品選択状態ストア。</param>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	/// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
	/// <param name="loadingService">ローディングサービス。</param>
	public SeriesInspectionPageViewModel(
		SeriesWorkspaceStore workspaceStore,
		BindingStore bindingStore,
		INavigationService navigationService,
		IServiceScopeFactory serviceScopeFactory,
		ThumbnailImageLoader thumbnailImageLoader,
		LoadingService loadingService)
	{
		this.workspaceStore = workspaceStore;
		this.bindingStore = bindingStore;
		this.navigationService = navigationService;
		this.serviceScopeFactory = serviceScopeFactory;
		this.thumbnailImageLoader = thumbnailImageLoader;
		this.loadingService = loadingService;

		// SeriesTitle: BindingTarget.Value?.Series.Title から Reactive に導出
		this.SeriesTitle = this.bindingStore.BindingTarget
			.Select(bindingTarget => bindingTarget?.Series?.Title ?? string.Empty)
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		// SeriesAuthor: BindingTarget.Value?.Series.Author から Reactive に導出
		this.SeriesAuthor = this.bindingStore.BindingTarget
			.Select(bindingTarget => bindingTarget?.Series?.Author ?? string.Empty)
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		// 巻カード用の SynchronizedView を初期化
		// BindingStore.BindingVolumes から VolumeCardViewModel へ変換
		var volumeCardsView = this.bindingStore.BindingVolumes
			.CreateView(bindingVolume =>
				new VolumeCardViewModel(
					bindingVolume,
					this.serviceScopeFactory));

		this.volumeCardsView = volumeCardsView;

		this.VolumeCards = volumeCardsView
			.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current)
			.AddTo(ref this.disposableBag);

		// VolumeCardViewModel のライフタイム管理
		volumeCardsView.ViewChanged += this.onVolumeCardsViewChanged;

		// MangaSeriesCard: 作品サムネイルカード用ViewModel
		this.MangaSeriesCard = new MangaSeriesCardViewModel()
			.AddTo(ref this.disposableBag);

		// BindingTarget 変更時に MangaSeriesCard へ Series と ThumbnailSource を接続
		this.bindingStore.BindingTarget.Subscribe(bindingTarget =>
		{
			var series = bindingTarget?.Series;
			if (series is not null)
			{
				// ThumbnailImageLoader で最終表示用 ImageSource を取得
				var imageSource = this.thumbnailImageLoader.Load(series);

				// MangaSeriesCard へ設定
				this.MangaSeriesCard.ThumbnailSource.Value = imageSource;

				// MangaSeriesCard の Series へ接続
				if (!ReferenceEquals(this.MangaSeriesCard.Series.Value, series))
				{
					this.MangaSeriesCard.Series.Value = series;
				}
				else
				{
					// 同一インスタンスの場合は ForceNotify() で再通知させる
					this.MangaSeriesCard.Series.ForceNotify();
				}
			}
			else
			{
				// series が null の場合はクリア
				this.MangaSeriesCard.ThumbnailSource.Value = null;
				this.MangaSeriesCard.Series.Value = null;
			}
		}).AddTo(ref this.disposableBag);

		this.GoBackCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.GoBackCommand.Subscribe(_ => this.navigationService.GoBack())
			.AddTo(ref this.disposableBag);

		this.StartBindingCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.StartBindingCommand.Subscribe(_ =>
		{
			// TODO: 製本処理実装後に置き換える
		}).AddTo(ref this.disposableBag);

		this.NavigateToPrepressCommand = new ReactiveCommand<BindingVolume>()
			.AddTo(ref this.disposableBag);
		this.NavigateToPrepressCommand.Subscribe(volume =>
		{
			// WorkFolderPath が null / 空白でないことを確認
			if (string.IsNullOrWhiteSpace(volume.WorkFolderPath))
			{
				throw new InvalidOperationException(
					"BindingVolume.WorkFolderPath が null または空白です。");
			}

			// SeriesWorkspaceStore.PrepressVolumes から対応する VolumeInspectionResult を取得
			if (!this.workspaceStore.PrepressVolumes.TryGetValue(
				volume.WorkFolderPath,
				out var result))
			{
				throw new InvalidOperationException(
					$"SeriesWorkspaceStore.PrepressVolumes に WorkFolderPath '{volume.WorkFolderPath}' に対応する VolumeInspectionResult が見つかりません。");
			}

			// CurrentPrepressVolume を設定
			this.workspaceStore.SetCurrentPrepressVolume(result);

			// VolumeThumbnailsPage へ遷移
			this.navigationService.NavigateWithHierarchy(typeof(VolumeThumbnailsPage));
		}).AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public async ValueTask InitializeDataAsync()
	{
		_ = this.executeSeriesInspectionAsync();
	}

	/// <summary>
	/// SeriesInspectionManager を実行して、製本前確認処理を開始します。
	/// </summary>
	private async Task executeSeriesInspectionAsync()
	{
		using (this.loadingService.Begin("展開・変換・検査中..."))
		{
			try
			{
				using var scope = this.serviceScopeFactory.CreateScope();
				var manager = scope.ServiceProvider.GetRequiredService<SeriesInspectionManager>();

				// 並列処理からのUI更新Taskを安全に保持
				var cardUpdateTasks = new ConcurrentBag<Task>();

				// ローカルイベントハンドラ：BindingVolume 検査完了時の UI更新
				void OnVolumeInspected(BindingVolume volume)
				{
					// UIスレッドへマーシャリング
					var operation = Application.Current.Dispatcher.InvokeAsync(
						new Func<Task>(async () =>
						{
							// 対象 VolumeCardViewModel を ReferenceEquals で検索
							var card = this.VolumeCards.FirstOrDefault(
								item => ReferenceEquals(item.Volume.Value, volume));

							if (card is null)
							{
								throw new InvalidOperationException(
									$"検査完了の BindingVolume に対応する VolumeCardViewModel が見つかりません。");
							}

							// BindingVolume の内部プロパティ更新を反映
							card.Volume.ForceNotify();

							// サムネイル読み込み
							await card.LoadThumbnailAsync();
						}));

					// UI更新Taskを追跡
					var updateTask = operation.Task.Unwrap();
					cardUpdateTasks.Add(updateTask);
				}

				manager.VolumeInspected += OnVolumeInspected;

				try
				{
					await manager.ExecuteAsync();

					// Manager が全巻の処理を終了した後、
					// イベントから開始した全カード更新Taskの完了を待つ
					await Task.WhenAll(cardUpdateTasks);

					// SeriesInspectionManager 完了 → 全カード更新Task完了後、
					// 旧Prepress互換データを準備
					this.registerPrepressCompatibilityVolumes();
				}
				finally
				{
					manager.VolumeInspected -= OnVolumeInspected;
				}
			}
			catch
			{
				// 例外は Fail Fast で処理（DispatcherUnhandledException へ到達）
				throw;
			}
		}
	}

	/// <summary>
	/// BindingStore.BindingVolumes 全巻から VolumeInspectionResult を作成し、
	/// SeriesWorkspaceStore.PrepressVolumes へ登録して、旧Prepress互換データを準備します。
	/// </summary>
	private void registerPrepressCompatibilityVolumes()
	{
		// 登録前に旧データをクリア
		this.workspaceStore.PrepressVolumes.Clear();

		// 対象を固定するため、ToArray で複製
		var volumes = this.bindingStore.BindingVolumes.ToArray();

		foreach (var volume in volumes)
		{
			// WorkFolderPath が null / 空白でないことを確認
			if (string.IsNullOrWhiteSpace(volume.WorkFolderPath))
			{
				throw new InvalidOperationException(
					"BindingVolume.WorkFolderPath が null または空白です。");
			}

			// Path.GetFileName の結果が null / 空白でないことを確認
			var volumeName = Path.GetFileName(volume.WorkFolderPath);
			if (string.IsNullOrWhiteSpace(volumeName))
			{
				throw new InvalidOperationException(
					$"Path.GetFileName(volume.WorkFolderPath) が null または空白です。 WorkFolderPath: {volume.WorkFolderPath}");
			}

			// VolumeInspectionResult を作成
			var result = new VolumeInspectionResult
			{
				VolumeName = volumeName,
				WorkVolumeFolderPath = volume.WorkFolderPath,
				ImageFileCount = volume.ImageFileCount,
				HasLandscapeImages = volume.LandscapeImageCount > 0,
				AllLandscape = volume.ImageFileCount > 0 && volume.LandscapeImageCount == volume.ImageFileCount,
				HasSubFolders = volume.HasSubFolder,
				HasIrregularFileNameLength = false,
			};

			// 登録
			this.workspaceStore.RegisterPrepressVolume(result);
		}
	}

	/// <summary>
	/// VolumeCardViewModel のライフタイム管理を行うイベントハンドラです。
	/// </summary>
	private void onVolumeCardsViewChanged(
		in SynchronizedViewChangedEventArgs<BindingVolume, VolumeCardViewModel> e)
	{
		switch (e.Action)
		{
			case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
				// 削除された VolumeCardViewModel を Dispose
				if (e.IsSingleItem)
				{
					e.OldItem.View?.Dispose();
				}
				else
				{
					foreach (var viewModel in e.OldViews)
					{
						viewModel?.Dispose();
					}
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
				// 置換前の VolumeCardViewModel を Dispose
				if (e.IsSingleItem)
				{
					e.OldItem.View?.Dispose();
				}
				else
				{
					foreach (var viewModel in e.OldViews)
					{
						viewModel?.Dispose();
					}
				}
				break;

			case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
				// Reset は Sort / Reverse / Clear の場合が考えられる
				// IsClear で判定し、Clear の場合だけ旧 VolumeCardViewModel を Dispose
				if (e.SortOperation.IsClear)
				{
					foreach (var viewModel in e.OldViews)
					{
						viewModel?.Dispose();
					}
				}
				// Sort / Reverse の場合は現在有効な VolumeCardViewModel を Dispose しない
				break;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// VolumeCards に残っているすべての VolumeCardViewModel を Dispose
		foreach (var viewModel in this.VolumeCards)
		{
			viewModel?.Dispose();
		}

		// volumeCardsView が null でない場合、ViewChanged イベントハンドラを解除
		if (this.volumeCardsView is not null)
		{
			this.volumeCardsView.ViewChanged -= this.onVolumeCardsViewChanged;
		}

		this.disposableBag.Dispose();
	}
}
