using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Helpers;

/// <summary>
/// ContentDialog の生成と表示を統一するヘルパークラスです。
/// </summary>
public static class ContentDialogHelper
{
	/// <summary>
	/// エラーダイアログを表示します。
	/// </summary>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="message">表示するメッセージ。</param>
	/// <param name="title">ダイアログのタイトル。既定値は "エラー"。</param>
	public static async Task ShowErrorAsync(
		IContentDialogService contentDialogService,
		string message,
		string title = "エラー")
	{
		var dialog = new ContentDialog
		{
			Title = title,
			Content = message,
			CloseButtonText = "OK",
		};
		await contentDialogService.ShowAsync(dialog, CancellationToken.None);
	}

	/// <summary>
	/// 確認ダイアログを表示します。
	/// </summary>
	/// <param name="contentDialogService">コンテントダイアログサービス。</param>
	/// <param name="title">ダイアログのタイトル。</param>
	/// <param name="message">表示するメッセージ。</param>
	/// <param name="primaryButtonText">プライマリボタンのテキスト。</param>
	/// <param name="secondaryButtonText">セカンダリボタンのテキスト。既定値は "キャンセル"。</param>
	/// <returns>プライマリボタンが選択された場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
	public static async Task<bool> ShowConfirmAsync(
		IContentDialogService contentDialogService,
		string title,
		string message,
		string primaryButtonText,
		string secondaryButtonText = "キャンセル")
	{
		var dialog = new ContentDialog
		{
			Title = title,
			Content = message,
			PrimaryButtonText = primaryButtonText,
			SecondaryButtonText = string.Empty,
			CloseButtonText= secondaryButtonText,
		};
		var result = await contentDialogService.ShowAsync(dialog, CancellationToken.None);
		return result == ContentDialogResult.Primary;
	}
}
