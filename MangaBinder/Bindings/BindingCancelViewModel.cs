using System.IO;
using MangaBinder.Controls;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本キャンセル処理を担当する ViewModel。
/// ContentDialog 表示、Dialog 初期化、キャンセル実行、エラー通知、Navigation を管理します。
/// </summary>
public class BindingCancelViewModel : IDisposable
{
	/// <summary>製本工程の正本状態ストア。</summary>
	private readonly BindingStore bindingStore;

	/// <summary>製本工程のオーケストレーション。</summary>
	private readonly BindingManager bindingManager;

	/// <summary>ContentDialog サービス。</summary>
	private readonly IContentDialogService contentDialogService;

	/// <summary>Snackbar サービス。</summary>
	private readonly ISnackbarService snackbarService;

	/// <summary>ローディングサービス。</summary>
	private readonly LoadingService loadingService;

	/// <summary>ナビゲーションサービス。</summary>
	private readonly INavigationService navigationService;

	/// <summary>リソース破棄管理。</summary>
	private DisposableBag disposableBag;

	/// <summary>
	/// 製本対象作品のタイトルを取得します。
	/// XAML の SeriesTitle.Value へ Binding されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> SeriesTitle { get; }

	/// <summary>
	/// Work 作品フォルダを削除するかどうかを取得または設定します。
	/// 初期値は false です。
	/// XAML の DeleteWorkFolder.Value へ Binding されます。
	/// </summary>
	public BindableReactiveProperty<bool> DeleteWorkFolder { get; }

	/// <summary>
	/// Work 作品フォルダのパスを取得します。
	/// BindingStore.WorkSeriesFolderPath を参照します。
	/// XAML の WorkSeriesFolderPath.Value へ Binding されます。
	/// </summary>
	public IReadOnlyBindableReactiveProperty<string> WorkSeriesFolderPath { get; }

	/// <summary>
	/// Work 作品フォルダが削除可能かどうかを取得します。
	/// Dialog 表示時に Directory.Exists() で設定されます。
	/// XAML の CanDeleteWorkFolder.Value へ Binding されます。
	/// </summary>
	public BindableReactiveProperty<bool> CanDeleteWorkFolder { get; }

	/// <summary>
	/// <see cref="BindingCancelViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="bindingStore">製本工程の正本状態ストア。</param>
	/// <param name="bindingManager">製本工程のオーケストレーション。</param>
	/// <param name="contentDialogService">ContentDialog サービス。</param>
	/// <param name="snackbarService">Snackbar サービス。</param>
	/// <param name="loadingService">ローディングサービス。</param>
	/// <param name="navigationService">ナビゲーションサービス。</param>
	public BindingCancelViewModel(
		BindingStore bindingStore,
		BindingManager bindingManager,
		IContentDialogService contentDialogService,
		ISnackbarService snackbarService,
		LoadingService loadingService,
		INavigationService navigationService)
	{
		this.bindingStore = bindingStore;
		this.bindingManager = bindingManager;
		this.contentDialogService = contentDialogService;
		this.snackbarService = snackbarService;
		this.loadingService = loadingService;
		this.navigationService = navigationService;

		this.disposableBag = new DisposableBag();

		// SeriesTitle: BindingTarget.Value?.Series.Title から Reactive に導出
		this.SeriesTitle = this.bindingStore.BindingTarget
			.Select(bindingTarget => bindingTarget?.Series?.Title ?? string.Empty)
			.ToReadOnlyBindableReactiveProperty(string.Empty)
			.AddTo(ref this.disposableBag);

		// DeleteWorkFolder: 初期値 false
		this.DeleteWorkFolder = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);

		// WorkSeriesFolderPath: BindingStore.WorkSeriesFolderPath を直接参照
		// BindingStore 所有のため Dispose 対象外
		this.WorkSeriesFolderPath = this.bindingStore.WorkSeriesFolderPath;

		// CanDeleteWorkFolder: 初期値 false
		this.CanDeleteWorkFolder = new BindableReactiveProperty<bool>(false)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 製本キャンセル ContentDialog を表示して、キャンセル処理を実行します。
	/// Dialog の初期化、CancelAsync() の呼び出し、エラー通知、Navigation を管理します。
	/// </summary>
	public async ValueTask ShowAsync()
	{
		// Dialog 表示時の初期化：DeleteWorkFolder を false にリセット
		this.DeleteWorkFolder.Value = false;

		// CanDeleteWorkFolder を設定：WorkSeriesFolderPath が存在するかを確認
		var workSeriesFolderPath = this.bindingStore.WorkSeriesFolderPath.Value;
		this.CanDeleteWorkFolder.Value = !string.IsNullOrEmpty(workSeriesFolderPath) && Directory.Exists(workSeriesFolderPath);

		// BindingCancelDialogContent を DataContext として ContentDialog を構築
		var dialog = new ContentDialog
		{
			Title = "製本を中止",
			Content = new BindingCancelDialogContent { DataContext = this },
			PrimaryButtonText = "製本を中止",
			CloseButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Close
		};

		// ContentDialog を表示
		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);

		// Primary ボタンが選択された場合のみ、BindingManager.CancelAsync() を実行
		if (result != ContentDialogResult.Primary)
		{
			return;
		}

		// キャンセル処理を実行
		var cancelCompleted = false;
		try
		{
			using (this.loadingService.Begin("製本を中止しています..."))
			{
				// UI 描画機会を確保
				await Task.Yield();

				// BindingManager.CancelAsync() を呼び出し
				await this.bindingManager.CancelAsync(
					this.DeleteWorkFolder.Value,
					CancellationToken.None);
			}

			cancelCompleted = true;
		}
		catch (Exception ex)
		{
			// エラーが発生した場合は Snackbar で表示
			this.snackbarService.Show(
				"製本中止エラー",
				$"製本の中止中にエラーが発生しました: {ex.Message}",
				ControlAppearance.Danger,
				new SymbolIcon { Symbol = SymbolRegular.Warning24 },
				TimeSpan.MaxValue);
		}
		finally
		{
			// 成功・失敗に関わらず、最後に必ず StartPage へ戻す
			this.navigationService.GetNavigationControl().ClearJournal();
			this.navigationService.Navigate(typeof(StartPage));

			if (cancelCompleted)
			{
				_ = System.Windows.Application.Current.Dispatcher.InvokeAsync(
					() => GC.Collect(),
					System.Windows.Threading.DispatcherPriority.ApplicationIdle);
			}
		}
	}

	/// <summary>
	/// リソースを破棄します。
	/// </summary>
	public void Dispose()
	{
		this.disposableBag.Dispose();
		GC.SuppressFinalize(this);
	}
}
