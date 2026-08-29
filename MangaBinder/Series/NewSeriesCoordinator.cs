using System.Windows;
using MangaBinder.Bindings;
using MangaBinder.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Series;

/// <summary>
/// 新規作品登録フロー全体をオーケストレーションするコーディネーター。
/// タイトル入力ダイアログの表示、タイトル判定、作品検索、EditorPage への遷移を統合管理します。
/// </summary>
public class NewSeriesCoordinator
{
	/// <summary>
	/// 新規作品登録フロー内のDialog表示状態を表します。
	/// </summary>
	private enum NewSeriesDialogState
	{
		/// <summary>タイトル入力状態。</summary>
		TitleInput,

		/// <summary>既存作品1件表示状態。</summary>
		SingleExistingSeries,

		/// <summary>既存作品複数件表示状態。</summary>
		MultipleExistingSeries,
	}

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>コンテントダイアログサービス。</summary>
	private readonly IContentDialogService contentDialogService;

	/// <summary>作品選択状態ストア。</summary>
	private readonly SeriesWorkspaceStore workspaceStore;

	/// <summary>DI スコープを作成するファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>
	/// <see cref="NewSeriesCoordinator"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="workspaceStore">作品選択状態ストア。</param>
	/// <param name="serviceScopeFactory">DI スコープを作成するファクトリー。</param>
	public NewSeriesCoordinator(
		INavigationService navigationService,
		IContentDialogService contentDialogService,
		SeriesWorkspaceStore workspaceStore,
		IServiceScopeFactory serviceScopeFactory)
	{
		this.navigationService = navigationService;
		this.contentDialogService = contentDialogService;
		this.workspaceStore = workspaceStore;
		this.serviceScopeFactory = serviceScopeFactory;
	}

	/// <summary>
	/// 新規作品登録フローを開始します。
	/// タイトル入力ダイアログを表示し、タイトル確定後に EditorPage へ遷移します。
	/// </summary>
	/// <returns>非同期操作を表す ValueTask。</returns>
	public async ValueTask StartAsync()
	{
		// Dialog 表示期間用の Scope を作成
		using var scope = this.serviceScopeFactory.CreateScope();

		// Scope から MangaSeriesManager を Resolve
		var mangaSeriesManager = scope.ServiceProvider.GetRequiredService<MangaSeriesManager>();

		// NewSeriesTitleDialogContentViewModel を生成
		var titleViewModel = new NewSeriesTitleDialogContentViewModel();

		// NewSeriesTitleDialogContent を生成
		var mainWindow = Application.Current?.MainWindow;
		var contentWidth = mainWindow?.ActualWidth * 2 / 3 ?? 800;

		var titleContent = new NewSeriesTitleDialogContent
		{
			DataContext = titleViewModel,
			Width = contentWidth,
		};

		var dialog = new ContentDialog
		{
			Title = "新規作品登録",
			Content = titleContent,
			PrimaryButtonText = "作品編集開始",
			CloseButtonText = "キャンセル",

			// UserControl の指定幅を ContentDialog の標準最大幅で切らせない
			DialogMaxWidth = double.PositiveInfinity,
		};

		// 確定された MangaSeries を格納する変数
		MangaSeries? confirmedSeries = null;

		// 現在表示中の既存作品確認用ViewModel（往復時に再利用・破棄の管理用）
		MaintenanceSeriesCardViewModel? existingSeriesCardViewModel = null;

		// 複数候補表示用ViewModel（往復時に破棄の管理用）
		MultipleExistingSeriesDialogContentViewModel? multipleExistingViewModel = null;

		// 現在のDialog表示状態
		NewSeriesDialogState dialogState = NewSeriesDialogState.TitleInput;

		// Dialog 表示前に Closing イベントハンドラを定義
		void handleDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs e)
		{
			// タイトル入力状態での Primary（作品編集開始）
			if (dialogState == NewSeriesDialogState.TitleInput && e.Result == ContentDialogResult.Primary)
			{
				this.handleTitleInputPrimary(
					titleViewModel,
					mangaSeriesManager,
					dialog,
					e,
					contentWidth,
					(existingSeries) =>
					{
						// 検索結果1件時のコールバック
						dialogState = NewSeriesDialogState.SingleExistingSeries;
						existingSeriesCardViewModel = new MaintenanceSeriesCardViewModel(existingSeries);
						this.switchToExistingSeriesContent(dialog, existingSeriesCardViewModel, contentWidth);
					},
					(multipleSeriesList) =>
					{
						// 検索結果2件以上時のコールバック
						dialogState = NewSeriesDialogState.MultipleExistingSeries;
						multipleExistingViewModel = new MultipleExistingSeriesDialogContentViewModel(multipleSeriesList);
						this.switchToMultipleExistingSeriesContent(dialog, multipleExistingViewModel, contentWidth);
					},
					(titleValue) =>
					{
						// 検索結果0件時のコールバック
						confirmedSeries = new MangaSeries
						{
							Title = titleValue
						};
						// この場合は Closing をキャンセルしない = Dialog が正常に閉じる
					});
			}
			// タイトル入力状態での Close（キャンセル）
			else if (dialogState == NewSeriesDialogState.TitleInput && e.Result == ContentDialogResult.None)
			{
				// 何もしない（Dialog正常終了 = 新規作品登録フロー終了）
			}
			// 既存作品1件表示状態での Primary（作品を開く）
			else if (dialogState == NewSeriesDialogState.SingleExistingSeries && e.Result == ContentDialogResult.Primary)
			{
				// 既存作品を最終確定とする
				if (existingSeriesCardViewModel?.Series.Value is MangaSeries selectedSeries)
				{
					confirmedSeries = selectedSeries;
				}

				// Dialog を正常に閉じる
				// e.Cancel を設定しない（自動的に閉じる）
			}
			// 既存作品1件表示状態での Secondary（新規作品として続行）
			else if (dialogState == NewSeriesDialogState.SingleExistingSeries && e.Result == ContentDialogResult.Secondary)
			{
				// 入力されたタイトルを使用して新規作品を作成
				var titleValue = titleViewModel.Title.Value ?? string.Empty;
				confirmedSeries = new MangaSeries
				{
					Title = titleValue
				};

				// Dialog を正常に閉じる
				// e.Cancel を設定しない（自動的に閉じる）
			}
			// 既存作品1件表示状態での Close（タイトルを修正する）
			else if (dialogState == NewSeriesDialogState.SingleExistingSeries && e.Result == ContentDialogResult.None)
			{
				// Closing をキャンセル
				e.Cancel = true;
				this.returnToTitleInputState(
					dialog,
					titleViewModel,
					titleContent,
					contentWidth,
					ref existingSeriesCardViewModel,
					ref multipleExistingViewModel);
				dialogState = NewSeriesDialogState.TitleInput;
			}
			// 既存作品複数件表示状態での Close（タイトルを修正する）
			else if (dialogState == NewSeriesDialogState.MultipleExistingSeries && e.Result == ContentDialogResult.None)
			{
				// Closing をキャンセル
				e.Cancel = true;
				this.returnToTitleInputState(
					dialog,
					titleViewModel,
					titleContent,
					contentWidth,
					ref existingSeriesCardViewModel,
					ref multipleExistingViewModel);
				dialogState = NewSeriesDialogState.TitleInput;
			}
		}

		// イベントハンドラを接続
		dialog.Closing += handleDialogClosing;

		try
		{
			// ContentDialog を表示
			var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);

			// Primary（作品編集開始/作品を開く）で正常終了し、作品が確定している場合
			if ((result == ContentDialogResult.Primary) && confirmedSeries != null)
			{
				// 編集対象を設定
				this.workspaceStore.EditTarget = confirmedSeries;

				// EditorPage へ遷移
				this.navigationService.NavigateWithHierarchy(typeof(EditorPage));
			}
		}
		finally
		{
			// Closing イベントの購読解除
			dialog.Closing -= handleDialogClosing;

			// 既存作品ViewModel を破棄
			existingSeriesCardViewModel?.Dispose();
			multipleExistingViewModel?.Dispose();

			// タイトル入力ViewModel を破棄
			titleViewModel.Dispose();

			// Scope は using 終了時に自動的に破棄される
		}
	}

	/// <summary>
	/// タイトル入力状態での Primary ボタン押下処理。
	/// </summary>
	private void handleTitleInputPrimary(
		NewSeriesTitleDialogContentViewModel viewModel,
		MangaSeriesManager manager,
		ContentDialog dialog,
		ContentDialogClosingEventArgs closingArgs,
		double contentWidth,
		Action<MangaSeries> onSingleSeriesFound,
		Action<IReadOnlyList<MangaSeries>> onMultipleSeriesFound,
		Action<string> onZeroMatches)
	{
		// 判定前に前回の InfoBar をクリア
		viewModel.IsErrorMessageVisible.Value = false;
		viewModel.ErrorMessage.Value = string.Empty;

		var titleValue = viewModel.Title.Value ?? string.Empty;

		// タイトル未入力判定
		if (string.IsNullOrWhiteSpace(titleValue))
		{
			// タイトルが未入力の場合
			viewModel.ErrorMessage.Value = "タイトルを入力してください。";
			viewModel.IsErrorMessageVisible.Value = true;

			// Closing をキャンセルしてダイアログを表示したままにする
			closingArgs.Cancel = true;
			return;
		}

		// タイトルが入力されている場合、同一タイトルを検索
		var sameSeriesList = manager.FindSameTitle(titleValue);

		// 検索結果によって分岐
		switch (sameSeriesList.Count)
		{
			case 0:
				// 検索結果0件：新規作品として進める
				onZeroMatches(titleValue);
				// Closing をキャンセルしない = Dialog が正常に閉じる
				break;

			case 1:
				// 検索結果1件：既存作品確認へ切り替え
				closingArgs.Cancel = true;
				onSingleSeriesFound(sameSeriesList[0]);
				break;

			default:
				// 検索結果2件以上：複数候補確認へ切り替え
				closingArgs.Cancel = true;
				onMultipleSeriesFound(sameSeriesList);
				break;
		}
	}

	/// <summary>
	/// 既存作品1件確認ContentへContentDialogのコンテンツを切り替える。
	/// </summary>
	private void switchToExistingSeriesContent(
		ContentDialog dialog,
		MaintenanceSeriesCardViewModel cardViewModel,
		double contentWidth)
	{
		// 既存作品確認コンテンツを生成
		var existingContent = new ExistingSeriesDialogContent
		{
			DataContext = cardViewModel,
			Width = contentWidth,
		};

		// Dialog の設定を既存作品確認用に変更
		dialog.Content = existingContent;
		dialog.Title = "同一タイトルの作品が登録済みです。";
		dialog.PrimaryButtonText = "作品を開く";
		dialog.SecondaryButtonText = "新規作品として続行";
		dialog.CloseButtonText = "タイトルを修正する";
		dialog.DefaultButton = ContentDialogButton.Primary;
	}

	/// <summary>
	/// 既存作品複数件確認ContentへContentDialogのコンテンツを切り替える。
	/// </summary>
	private void switchToMultipleExistingSeriesContent(
		ContentDialog dialog,
		MultipleExistingSeriesDialogContentViewModel viewModel,
		double contentWidth)
	{
		// 複数候補確認コンテンツを生成
		var multipleContent = new MultipleExistingSeriesDialogContent
		{
			DataContext = viewModel,
			Width = contentWidth,
		};

		// Dialog の設定を複数候補確認用に変更
		dialog.Content = multipleContent;
		dialog.Title = "同一タイトルの作品が登録済みです。";
		dialog.PrimaryButtonText = string.Empty;
		dialog.SecondaryButtonText = string.Empty;
		dialog.CloseButtonText = "タイトルを修正する";
		dialog.DefaultButton = ContentDialogButton.Close;
	}

	/// <summary>
	/// Dialogをタイトル入力状態へ戻す共通処理。
	/// </summary>
	private void returnToTitleInputState(
		ContentDialog dialog,
		NewSeriesTitleDialogContentViewModel titleViewModel,
		NewSeriesTitleDialogContent titleContent,
		double contentWidth,
		ref MaintenanceSeriesCardViewModel? existingSeriesCardViewModel,
		ref MultipleExistingSeriesDialogContentViewModel? multipleExistingViewModel)
	{
		// 古い既存作品1件ViewModel を破棄
		existingSeriesCardViewModel?.Dispose();
		existingSeriesCardViewModel = null;

		// 古い複数候補ViewModel を破棄
		multipleExistingViewModel?.Dispose();
		multipleExistingViewModel = null;

		// Content をタイトル入力に戻す
		dialog.Content = titleContent;
		dialog.Title = "新規作品登録";
		dialog.PrimaryButtonText = "作品編集開始";
		dialog.SecondaryButtonText = string.Empty;
		dialog.CloseButtonText = "キャンセル";
		dialog.DefaultButton = ContentDialogButton.Primary;

		// タイトルTextBoxへフォーカス＆全選択をリクエスト
		titleViewModel.TitleInputFocusRequest.Value++;
	}
}
