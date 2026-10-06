using System.Diagnostics;
using System.IO;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Bindings;

/// <summary>
/// 製本用の展開先作品フォルダを Explorer で開くサービスです。
/// </summary>
public class BindingFolderOpener
{
	/// <summary>スナックバーサービス。</summary>
	private readonly ISnackbarService snackbarService;

	/// <summary>
	/// <see cref="BindingFolderOpener"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="snackbarService">スナックバーサービス。</param>
	public BindingFolderOpener(ISnackbarService snackbarService)
	{
		this.snackbarService = snackbarService;
	}

	/// <summary>
	/// 指定された展開先作品フォルダを Explorer で開きます。
	/// フォルダが存在しない場合や起動に失敗した場合は Snackbar で通知します。
	/// </summary>
	/// <param name="workSeriesFolderPath">展開先作品フォルダのパス。</param>
	public async Task OpenAsync(string workSeriesFolderPath)
	{
		if (string.IsNullOrEmpty(workSeriesFolderPath) || !Directory.Exists(workSeriesFolderPath))
		{
			this.snackbarService.Show(
				"展開先フォルダを開けません",
				$"展開先フォルダが見つかりません。\n{workSeriesFolderPath}",
				ControlAppearance.Danger,
				new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
				TimeSpan.MaxValue);
			return;
		}

		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = workSeriesFolderPath,
				UseShellExecute = true,
			});
		}
		catch
		{
			this.snackbarService.Show(
				"展開先フォルダを開けません",
				$"フォルダを開く処理中にエラーが発生しました。\n{workSeriesFolderPath}",
				ControlAppearance.Danger,
				new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
				TimeSpan.MaxValue);
		}

		await Task.CompletedTask;
	}
}
