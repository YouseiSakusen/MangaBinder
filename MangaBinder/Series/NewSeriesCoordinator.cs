using System.Windows;
using MangaBinder.Bindings;
using MangaBinder.Controls;
using Microsoft.Extensions.DependencyInjection;
using R3;
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

		// 既存作品1件表示用ViewModel（往復時に破棄の管理用）
		ExistingSeriesDialogContentViewModel? existingSeriesViewModel = null;

		// 複数候補表示用ViewModel（往復時に破棄の管理用）
		MultipleExistingSeriesDialogContentViewModel? multipleExistingViewModel = null;

		// 1件候補画面での購読管理（Dialog内で一時的に保持）
		DisposableBag singleExistingSubscriptions = new();

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
						existingSeriesViewModel = new ExistingSeriesDialogContentViewModel(existingSeries);
						this.switchToExistingSeriesContent(
							dialog,
							existingSeriesViewModel,
							contentWidth,
							ref singleExistingSubscriptions);
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
			// 既存作品1件表示状態での Primary
			else if (dialogState == NewSeriesDialogState.SingleExistingSeries && e.Result == ContentDialogResult.Primary)
			{
				// RadioButton 状態に応じた処理を実行
				if (existingSeriesViewModel?.IsOpenExistingSeriesSelected.Value == true)
				{
					// 「既存作品を開く」が選択されている場合
					// 既存作品を最終確定とする
					if (existingSeriesViewModel?.Series is MangaSeries selectedSeries)
					{
						confirmedSeries = selectedSeries;
					}

					// Dialog を正常に閉じる
				}
				else if (existingSeriesViewModel?.IsAddAsOtherAuthorSelected.Value == true)
				{
					// 「別作者の作品として追加」が選択されている場合
					// 作者重複を再度判定
					var isDuplicate = IsAuthorsDuplicate(
						existingSeriesViewModel.Series.Author ?? string.Empty,
						existingSeriesViewModel.NewAuthorInput.Value ?? string.Empty);

					if (isDuplicate)
					{
						// 同一作者の場合はエラー表示して Dialog を閉じない
						existingSeriesViewModel.IsAuthorDuplicateErrorVisible.Value = true;
						e.Cancel = true;
					}
					else
					{
						// 別作者の場合は新規 MangaSeries を作成
						var titleValue = titleViewModel.Title.Value ?? string.Empty;
						confirmedSeries = new MangaSeries
						{
							Title = titleValue,
							Author = existingSeriesViewModel.NewAuthorInput.Value
						};

						// Dialog を正常に閉じる
					}
				}
			}
			// 既存作品1件表示状態での Secondary（キャンセル）
			else if (dialogState == NewSeriesDialogState.SingleExistingSeries && e.Result == ContentDialogResult.Secondary)
			{
				// 新規作品登録フロー全体を終了
				// confirmedSeries は null のままなので、後の処理で EditorPage へ進まない
			}
			// 既存作品1件表示状態での Close（タイトルを修正する）
			else if (dialogState == NewSeriesDialogState.SingleExistingSeries && e.Result == ContentDialogResult.None)
			{
				// Closing をキャンセル
				e.Cancel = true;

				// 1件候補画面の購読をクリア
				singleExistingSubscriptions.Dispose();
				singleExistingSubscriptions = new();

				this.returnToTitleInputState(
					dialog,
					titleViewModel,
					titleContent,
					contentWidth,
					ref existingSeriesViewModel,
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
					ref existingSeriesViewModel,
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

			// 1件候補画面の購読をクリア
			singleExistingSubscriptions.Dispose();

			// 既存作品ViewModel を破棄
			existingSeriesViewModel?.Dispose();
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
	/// 2つの作者文字列が同一であるかを判定します。
	/// 両方を Trim() して StringComparison.Ordinal で比較します。
	/// 空文字列は同一作者として扱われます。
	/// </summary>
	/// <param name="existingAuthor">既存作品の作者。</param>
	/// <param name="newAuthor">新規入力の作者。</param>
	/// <returns>同一作者の場合 true、異なる場合 false。</returns>
	private static bool IsAuthorsDuplicate(string existingAuthor, string newAuthor)
	{
		var trimmedExisting = existingAuthor.Trim();
		var trimmedNew = newAuthor.Trim();

		return string.Equals(trimmedExisting, trimmedNew, StringComparison.Ordinal);
	}

	/// <summary>
	/// 既存作品1件確認ContentへContentDialogのコンテンツを切り替える。
	/// RadioButton状態を監視し、PrimaryButtonTextを動的に更新します。
	/// </summary>
	private void switchToExistingSeriesContent(
		ContentDialog dialog,
		ExistingSeriesDialogContentViewModel viewModel,
		double contentWidth,
		ref DisposableBag subscriptions)
	{
		// 既存作品確認コンテンツを生成
		var existingContent = new ExistingSeriesDialogContent
		{
			DataContext = viewModel,
			Width = contentWidth,
		};

		// Dialog の設定を既存作品確認用に変更
		dialog.Content = existingContent;
		dialog.Title = "同一タイトルの作品が登録済みです。";
		dialog.SecondaryButtonText = "キャンセル";
		dialog.CloseButtonText = "タイトルを修正する";
		dialog.DefaultButton = ContentDialogButton.Primary;

		// 初期状態では「既存作品を開く」選択なので PrimaryButtonText を設定
		this.updatePrimaryButtonText(dialog, viewModel);

		// RadioButton 状態を監視して PrimaryButtonText を動的に更新
		viewModel.IsOpenExistingSeriesSelected
			.Subscribe(_ =>
			{
				this.updatePrimaryButtonText(dialog, viewModel);
			})
			.AddTo(ref subscriptions);

		// 作者入力値を監視して、重複判定と InfoBar 表示を更新
		viewModel.NewAuthorInput
			.Subscribe(_ =>
			{
				this.reevaluateAuthorDuplicateError(viewModel);
			})
			.AddTo(ref subscriptions);

		// RadioButton 状態の変更を監視
		viewModel.IsAddAsOtherAuthorSelected
			.Subscribe(_ =>
			{
				this.reevaluateAuthorDuplicateError(viewModel);
			})
			.AddTo(ref subscriptions);
	}

	/// <summary>
	/// Primary ボタンのテキストを現在の RadioButton 選択状態に基づいて更新します。
	/// </summary>
	private void updatePrimaryButtonText(
		ContentDialog dialog,
		ExistingSeriesDialogContentViewModel viewModel)
	{
		dialog.PrimaryButtonText = viewModel.IsOpenExistingSeriesSelected.Value
			? "既存作品を開く"
			: "新規作品追加";
	}

	/// <summary>
	/// 作者重複エラーの表示状態を再評価します。
	/// 「別作者として追加」が選択されている場合のみ判定を行います。
	/// </summary>
	private void reevaluateAuthorDuplicateError(
		ExistingSeriesDialogContentViewModel viewModel)
	{
		if (viewModel.IsAddAsOtherAuthorSelected.Value)
		{
			var isDuplicate = IsAuthorsDuplicate(
				viewModel.Series.Author ?? string.Empty,
				viewModel.NewAuthorInput.Value ?? string.Empty);
			viewModel.IsAuthorDuplicateErrorVisible.Value = isDuplicate;
		}
		else
		{
			// 「既存作品を開く」へ切り替え時は即座に非表示
			viewModel.IsAuthorDuplicateErrorVisible.Value = false;
		}
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
		ref ExistingSeriesDialogContentViewModel? existingSeriesViewModel,
		ref MultipleExistingSeriesDialogContentViewModel? multipleExistingViewModel)
	{
		// 古い既存作品1件ViewModel を破棄
		existingSeriesViewModel?.Dispose();
		existingSeriesViewModel = null;

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
