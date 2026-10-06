using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MangaBinder.Controls;
using MangaBinder.Settings;
using Microsoft.Extensions.DependencyInjection;
using ObservableCollections;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// 製本前確認画面の ViewModel です。
/// </summary>
public class SeriesInspectionPageViewModel : IDisposable, IDataInitializable
{
	/// <summary>製本工程の正本状態ストア。</summary>
	private readonly BindingStore bindingStore;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>コンテントダイアログサービス。</summary>
	private readonly IContentDialogService contentDialogService;

	/// <summary>スナックバーサービス。</summary>
	private readonly ISnackbarService snackbarService;

	/// <summary>スコープファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>ローディングサービス。</summary>
	private readonly LoadingService loadingService;

	private DisposableBag disposableBag;

	/// <summary>作品タイトルと作品カードの共通 ViewModel を取得します。</summary>
	public BindingSeriesViewModel BindingSeries { get; }

	/// <summary>選択中の作品著者を取得します。</summary>
	public IReadOnlyBindableReactiveProperty<string> SeriesAuthor { get; }

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

	/// <summary>今回の製本セッションでZIPを作成するかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> CreateZip => this.bindingStore.CreateZip;

	/// <summary>製本完了後に出力先を開くかどうかを取得します。</summary>
	public BindableReactiveProperty<bool> OpenOutputAfterCompletion => this.bindingStore.OpenOutputAfterCompletion;

	// コマンド

	/// <summary>戻るコマンドを取得します。</summary>
	public ReactiveCommand GoBackCommand { get; }

	/// <summary>製本開始コマンドを取得します。</summary>
	public ReactiveCommand StartBindingCommand { get; }

	/// <summary>選択した巻を見開き分割画面で開くコマンドを取得します。</summary>
	public ReactiveCommand<BindingVolume> NavigateToImageSplitterCommand { get; }

	/// <summary>展開先作品フォルダを開くコマンドを取得します。</summary>
	public ReactiveCommand OpenBindingFolderCommand { get; }

	/// <summary>展開先作品フォルダ直下の巻フォルダ数を取得します。</summary>
	public BindableReactiveProperty<int> WorkVolumeFolderCount => this.bindingStore.WorkVolumeFolderCount;

	/// <summary>製本をキャンセルするコマンドを取得します。</summary>
	public ReactiveCommand CancelCommand { get; }

	/// <summary>
	/// <see cref="SeriesInspectionPageViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="snackbarService">スナックバーサービス。</param>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	/// <param name="thumbnailImageLoader">サムネイル画像ローダー。</param>
	/// <param name="loadingService">ローディングサービス。</param>
	public SeriesInspectionPageViewModel(
		BindingStore bindingStore,
		INavigationService navigationService,
		IContentDialogService contentDialogService,
		ISnackbarService snackbarService,
		IServiceScopeFactory serviceScopeFactory,
		ThumbnailImageLoader thumbnailImageLoader,
		LoadingService loadingService)
	{
		this.bindingStore = bindingStore;
		this.navigationService = navigationService;
		this.contentDialogService = contentDialogService;
		this.snackbarService = snackbarService;
		this.serviceScopeFactory = serviceScopeFactory;
		this.loadingService = loadingService;

		this.BindingSeries = new BindingSeriesViewModel(this.bindingStore, thumbnailImageLoader)
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

		this.bindingStore.VolumeUpdated
			.SubscribeAwait(
				async (updatedVolume, _) => await this.refreshVolumeCardAsync(updatedVolume),
				AwaitOperation.Sequential)
			.AddTo(ref this.disposableBag);

		this.GoBackCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.GoBackCommand.Subscribe(_ => this.navigationService.GoBack())
			.AddTo(ref this.disposableBag);

		this.StartBindingCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.StartBindingCommand.Subscribe(async _ =>
		{
			await this.executeStartBindingAsync();
		}).AddTo(ref this.disposableBag);

		this.NavigateToImageSplitterCommand = new ReactiveCommand<BindingVolume>()
			.AddTo(ref this.disposableBag);
		this.NavigateToImageSplitterCommand.Subscribe(volume =>
		{
			// 見開き分割対象巻を設定
			this.bindingStore.SplitTargetVolume.Value = volume;

			// ImageSplitterPage へ遷移
			this.navigationService.NavigateWithHierarchy(typeof(ImageSplitterPage));
		}).AddTo(ref this.disposableBag);

		this.OpenBindingFolderCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);
		this.OpenBindingFolderCommand
			.SubscribeAwait(async (_, _) => await this.openBindingFolderAsync(), AwaitOperation.Drop)
			.AddTo(ref this.disposableBag);

		this.CancelCommand = new ReactiveCommand()
			.AddTo(ref this.disposableBag);

		this.CancelCommand.Subscribe(async _ => await this.executeCancelAsync())
			.AddTo(ref this.disposableBag);
	}

	/// <inheritdoc/>
	public async ValueTask InitializeDataAsync()
	{
		await this.executeSeriesInspectionAsync();
	}

	/// <summary>
	/// 製本キャンセル処理を実行します。
	/// Scoped な BindingCancelViewModel を動的に生成して ShowAsync() を呼び出します。
	/// </summary>
	private async ValueTask executeCancelAsync()
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var viewModel = scope.ServiceProvider.GetRequiredService<BindingCancelViewModel>();
		await viewModel.ShowAsync();
	}

	/// <summary>
	/// 展開先作品フォルダを Explorer で開きます。
	/// </summary>
	private async ValueTask openBindingFolderAsync()
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var opener = scope.ServiceProvider.GetRequiredService<BindingFolderOpener>();
		await opener.OpenAsync(this.bindingStore.WorkSeriesFolderPath.Value);
	}

	/// <summary>
	/// SeriesInspectionManager を実行して、製本前確認処理を開始します。
	/// </summary>
	private async Task executeSeriesInspectionAsync()
	{
		using (this.loadingService.Begin("展開・変換・検査中..."))
		{
			// SeriesInspectionPage へのNavigation と LoadingService.Begin() 後、
			// 高優先度Dispatcher処理（Render等）を先に処理して UI を描画させる
			await Dispatcher.Yield(DispatcherPriority.Background);

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
					cardUpdateTasks.Add(this.refreshVolumeCardAsync(volume).AsTask());
				}

				manager.VolumeInspected += OnVolumeInspected;

				try
				{
					await manager.ExecuteAsync();

					// Manager が全巻の処理を終了した後、
					// イベントから開始した全カード更新Taskの完了を待つ
					await Task.WhenAll(cardUpdateTasks);
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
	/// 正本 BindingVolume に対応する巻カードを ForceNotify し、サムネイルを再読み込みします。
	/// UI スレッドへマーシャリングし、対象カードが見つからない場合は例外をスローします。
	/// </summary>
	private async ValueTask refreshVolumeCardAsync(BindingVolume volume)
	{
		await Application.Current.Dispatcher.InvokeAsync(
			new Func<Task>(async () =>
			{
				var card = this.VolumeCards.FirstOrDefault(
					item => ReferenceEquals(item.Volume.Value, volume));

				if (card is null)
				{
					throw new InvalidOperationException(
						"BindingVolume に対応する VolumeCardViewModel が見つかりません。");
				}

				card.Volume.ForceNotify();
				await card.LoadThumbnailAsync();
			})).Task.Unwrap();
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

	/// <summary>
	/// 製本完了処理を実行します。
	/// 事前確認 → ユーザー確認 → Loading中にCompleteAsync → Explorerを開く（失敗時は別通知） → セッション終了 → StartPage遷移 → 成功Snackbar
	/// </summary>
	private async Task executeStartBindingAsync()
	{
		try
		{
			// 1. BindingManager を Scope 内から取得
			using var scope = this.serviceScopeFactory.CreateScope();
			var bindingManager = scope.ServiceProvider.GetRequiredService<BindingManager>();

			// 2. 製本完了処理を開始する前の事前確認情報を取得
			var startableStatus = await bindingManager.GetStartableStatusAsync();

			// 3. 確認が必要かどうかを判定
			if (startableStatus.RequiresConfirmation)
			{
				// ContentDialog を表示
				var dialogResult = await this.showBindingConfirmationDialogAsync(startableStatus);

				// ユーザーがキャンセルした場合は処理終了
				if (dialogResult == ContentDialogResult.None)
				{
					return;
				}
			}

			// 4. 同名ファイルの上書き許可フラグを決定
			bool allowOverwrite = startableStatus.OutputFileExists;

			// 5. Loading 表示中に CompleteAsync を実行
			BindingCompletionResult completionResult;
			using (this.loadingService.Begin("製本中..."))
			{
				completionResult = await bindingManager.CompleteAsync(allowOverwrite);
			}

			// 6. OpenOutputAfterCompletion が true ならExplorerを起動
			if (this.bindingStore.OpenOutputAfterCompletion.Value)
			{
				try
				{
					await this.openExplorerAsync(completionResult);
				}
				catch (Exception explorerEx)
				{
					// Explorer起動失敗は製本失敗ではなく、別のSnackbarで通知
					this.snackbarService.Show(
						"警告",
						$"出力先を開けませんでした：{explorerEx.Message}",
						ControlAppearance.Caution,
						new SymbolIcon { Symbol = SymbolRegular.Warning24 },
						TimeSpan.FromSeconds(5));
				}
			}

			// 7. BindingStore.BindingTarget をクリア（この時点で OpenOutputAfterCompletion が false になる）
			this.bindingStore.BindingTarget.Value = null;

			// 8. StartPage へ遷移
			this.navigationService.Navigate(typeof(StartPage));

			_ = Application.Current.Dispatcher.InvokeAsync(() => GC.Collect(), DispatcherPriority.ApplicationIdle);

			// 9.
			var outputPath = string.IsNullOrEmpty(completionResult.OutputFilePath)
				? completionResult.OpenFolderPath
				: completionResult.OutputFilePath;

			this.snackbarService.Show(
				"製本が完了しました",
				$"{completionResult.BindingSeries.Series.Title}\n{outputPath}",
				ControlAppearance.Success,
				new SymbolIcon { Symbol = SymbolRegular.CheckmarkCircle24 },
				TimeSpan.FromSeconds(5));
		}
		catch (Exception ex)
		{
			// エラー時：例外を通知
			this.snackbarService.Show(
				"製本エラー",
				$"製本処理に失敗しました：{ex.Message}",
				ControlAppearance.Danger,
				new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
				TimeSpan.MaxValue);
		}
	}

	/// <summary>
	/// 製本完了処理の確認ダイアログを表示します。
	/// </summary>
	private async Task<ContentDialogResult> showBindingConfirmationDialogAsync(BindingStartableStatus status)
	{
		string title;
		string message;
		string primaryButtonText;

		// 3パターンの ContentDialog を条件に応じて表示
		if (status.OutputFileExists && status.IsSizeOverWarningThreshold)
		{
			// パターン3：同名ZIP + 2.5GB超
			title = "製本確認";
			message = "既に同じ名前のファイルが存在します。\n" +
					  "作成するzipファイルのサイズが2.5GBを超えることが予想されます。\n\n" +
					  "上書きして続行しますか？";
			primaryButtonText = "上書きして続行";
		}
		else if (status.OutputFileExists)
		{
			// パターン1：同名ZIPのみ
			title = "製本確認";
			message = "既に同じ名前のファイルが存在します。\n\n" +
					  "上書きして続行しますか？";
			primaryButtonText = "上書きして続行";
		}
		else
		{
			// パターン2：2.5GB超のみ
			title = "製本確認";
			message = "作成するzipファイルのサイズが2.5GBを超えることが予想されます。\n\n" +
					  "このまま続行しますか？";
			primaryButtonText = "続行";
		}

		var dialog = new ContentDialog
		{
			Title = title,
			Content = message,
			PrimaryButtonText = primaryButtonText,
			CloseButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Primary
		};

		return await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
	}

	/// <summary>
	/// BindingCompletionResult の情報に基づいてWindows Explorerを起動します。
	/// SelectFilePath が空でない場合は /select オプション付きで起動、
	/// 空の場合は OpenFolderPath をそのまま開きます。
	/// </summary>
	/// <param name="result">BindingCompletionResult。</param>
	private async Task openExplorerAsync(BindingCompletionResult result)
	{
		if (!string.IsNullOrEmpty(result.SelectFilePath))
		{
			// SelectFilePath が指定されている場合：ファイルを選択して開く
			var processInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = $"/select,\"{result.SelectFilePath}\"",
				UseShellExecute = true
			};

			using var process = System.Diagnostics.Process.Start(processInfo);
			if (process is not null)
			{
				// プロセスが正常に開始されたことを確認
				await Task.Run(() => process.WaitForExit(1000));
			}
		}
		else if (!string.IsNullOrEmpty(result.OpenFolderPath))
		{
			// SelectFilePath が空の場合：フォルダを開く
			var processInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = $"\"{result.OpenFolderPath}\"",
				UseShellExecute = true
			};

			using var process = System.Diagnostics.Process.Start(processInfo);
			if (process is not null)
			{
				// プロセスが正常に開始されたことを確認
				await Task.Run(() => process.WaitForExit(1000));
			}
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
