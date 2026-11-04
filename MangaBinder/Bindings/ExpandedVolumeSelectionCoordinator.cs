using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 展開済み巻追加Dialogの表示をオーケストレーションするコーディネーター。
/// </summary>
public class ExpandedVolumeSelectionCoordinator
{
	/// <summary>Dialog内の表示状態。</summary>
	private enum DialogState
	{
		/// <summary>展開済み巻の選択一覧。</summary>
		Selection,

		/// <summary>置換確認。</summary>
		ReplaceConfirm,
	}

	/// <summary>コンテントダイアログサービス。</summary>
	private readonly IContentDialogService contentDialogService;

	/// <summary>スコープファクトリー。</summary>
	private readonly IServiceScopeFactory serviceScopeFactory;

	/// <summary>
	/// <see cref="ExpandedVolumeSelectionCoordinator"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="serviceScopeFactory">スコープファクトリー。</param>
	public ExpandedVolumeSelectionCoordinator(
		IContentDialogService contentDialogService,
		IServiceScopeFactory serviceScopeFactory)
	{
		this.contentDialogService = contentDialogService;
		this.serviceScopeFactory = serviceScopeFactory;
	}

	/// <summary>
	/// 展開済み巻追加Dialogを表示します。
	/// 重複する巻番号の既存巻がある場合は、同一Dialog内で置換確認へ切り替えます。
	/// </summary>
	/// <returns>非同期操作を表す ValueTask。</returns>
	public async ValueTask ShowAsync()
	{
		using var scope = this.serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<VolumeSelectionManager>();

		var selectionContent = new ExpandedVolumeSelectionContent
		{
			DataContext = new ExpandedVolumeSelectionViewModel(manager.PrepareExpandedVolumeSelection()),
		};

		var dialog = new ContentDialog
		{
			Title = "展開済み巻を追加",
			Content = selectionContent,
			PrimaryButtonText = "追加",
			CloseButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Primary,
		};

		var state = DialogState.Selection;

		void handleClosing(ContentDialog sender, ContentDialogClosingEventArgs e)
		{
			if (state == DialogState.Selection && e.Result == ContentDialogResult.Primary)
			{
				var replacements = manager.FindExpandedVolumeReplacements();
				if (replacements.Count == 0)
				{
					manager.ApplyExpandedVolumeSelection();
					return;
				}

				e.Cancel = true;
				state = DialogState.ReplaceConfirm;
				dialog.Content = new ExpandedVolumeReplaceConfirmContent { DataContext = replacements };
				dialog.Title = "既存の巻を置換します";
				dialog.PrimaryButtonText = "置換";
				dialog.CloseButtonText = "戻る";
			}
			else if (state == DialogState.ReplaceConfirm && e.Result == ContentDialogResult.Primary)
			{
				manager.ApplyExpandedVolumeSelection();
			}
			else if (state == DialogState.ReplaceConfirm)
			{
				// 戻る：チェック状態を維持したまま選択一覧へ戻る
				e.Cancel = true;
				state = DialogState.Selection;
				dialog.Content = selectionContent;
				dialog.Title = "展開済み巻を追加";
				dialog.PrimaryButtonText = "追加";
				dialog.CloseButtonText = "キャンセル";
			}
		}

		dialog.Closing += handleClosing;
		try
		{
			await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
		}
		finally
		{
			dialog.Closing -= handleClosing;
		}
	}
}
