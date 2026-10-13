using MangaBinder.Series;
using Microsoft.Extensions.DependencyInjection;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Controls;

/// <summary>
/// <see cref="MemoEditButton"/> の ViewModel です。
/// メモ登録状態の表示と、メモ編集 Dialog の表示・保存を担います。
/// </summary>
public class MemoEditButtonViewModel : IDisposable
{
	/// <summary>コンテントダイアログサービス。</summary>
	private readonly IContentDialogService contentDialogService;

	/// <summary>Scoped サービスを必要時に生成するためのファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>共有 Series の購読。作品切り替え時に差し替えます。</summary>
	private IDisposable? seriesSubscription;

	/// <summary>編集対象の共有 MangaSeriesViewModel。</summary>
	private MangaSeriesViewModel? targetViewModel;

	/// <summary>リソース管理用の DisposableBag。</summary>
	private DisposableBag disposableBag;

	/// <summary>メモ表示テキスト（「メモ：未登録」「メモ：登録済み」）を取得します。</summary>
	public BindableReactiveProperty<string> MemoStatusText { get; }

	/// <summary>メモ編集コマンドを取得します。</summary>
	public ReactiveCommand<Unit> EditCommand { get; }

	/// <summary>
	/// <see cref="MemoEditButtonViewModel"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="serviceScopeFactory">Scoped サービスを生成するためのファクトリー。</param>
	public MemoEditButtonViewModel(
		IContentDialogService contentDialogService,
		IServiceScopeFactory serviceScopeFactory)
	{
		this.contentDialogService = contentDialogService;
		this.serviceScopeFactory = serviceScopeFactory;

		this.MemoStatusText = new BindableReactiveProperty<string>(getStatusText(null))
			.AddTo(ref this.disposableBag);

		this.EditCommand = new ReactiveCommand<Unit>()
			.AddTo(ref this.disposableBag);

		this.EditCommand
			.SubscribeAwait(async (_, cancellationToken) => await this.editAsync(), AwaitOperation.Drop)
			.AddTo(ref this.disposableBag);
	}

	/// <summary>
	/// 編集対象の作品を設定します。
	/// 共有 MangaSeriesViewModel.Series の通知を購読し、表示を自動更新します。
	/// </summary>
	/// <param name="seriesViewModel">編集対象の共有VM。未設定の場合は null。</param>
	public void SetSeriesViewModel(MangaSeriesViewModel? seriesViewModel)
	{
		this.seriesSubscription?.Dispose();
		this.seriesSubscription = null;
		this.targetViewModel = seriesViewModel;

		if (seriesViewModel is null)
		{
			this.MemoStatusText.Value = getStatusText(null);
			return;
		}

		// 共有 Series の通知（ForceNotify を含む）でメモ状態を更新する
		this.seriesSubscription = seriesViewModel.Series
			.Subscribe(current => this.MemoStatusText.Value = getStatusText(current));
	}

	/// <summary>
	/// メモ編集 Dialog を表示し、保存が選択された場合にメモを保存します。
	/// </summary>
	private async ValueTask editAsync()
	{
		var targetViewModel = this.targetViewModel;
		var series = targetViewModel?.Series.Value;
		if (targetViewModel is null || series is null)
			return;

		using var dialogViewModel = new MemoEditDialogContentViewModel(series.Memo);

		var content = new MemoEditDialogContent
		{
			DataContext = dialogViewModel,
		};

		var dialog = new ContentDialog
		{
			Title = "メモ編集",
			Content = content,
			PrimaryButtonText = "保存",
			CloseButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Primary,
		};

		var result = await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
		if (result != ContentDialogResult.Primary)
			return;

		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<SeriesMemoManager>();
		SaveMemoAsync(targetViewModel, 
	}

	/// <summary>
	/// 作品のメモ登録状態から表示テキストを生成します。
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <returns>表示テキスト。</returns>
	private static string getStatusText(MangaSeries? series)
		=> series is not null && series.HasMemo ? "登録済み" : "未登録";

	/// <summary>
	/// リソースを破棄します。
	/// </summary>
	public void Dispose()
	{
		this.seriesSubscription?.Dispose();
		this.disposableBag.Dispose();
	}
}
