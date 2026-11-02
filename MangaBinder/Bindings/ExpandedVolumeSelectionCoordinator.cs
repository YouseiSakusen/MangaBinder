using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 展開済み巻追加Dialogの表示をオーケストレーションするコーディネーター。
/// </summary>
public class ExpandedVolumeSelectionCoordinator
{
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
	/// </summary>
	/// <returns>非同期操作を表す ValueTask。</returns>
	public async ValueTask ShowAsync()
	{
		var dialog = new ContentDialog
		{
			Title = "展開済み巻を追加",
			Content = new ExpandedVolumeSelectionContent(),
			PrimaryButtonText = "追加",
			CloseButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Primary,
		};

		await this.contentDialogService.ShowAsync(dialog, CancellationToken.None);
	}
}
