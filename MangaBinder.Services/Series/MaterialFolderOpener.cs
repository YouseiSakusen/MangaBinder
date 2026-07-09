using System.Diagnostics;
using System.IO;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Series;

/// <summary>
/// 素材フォルダを Explorer で開くサービスです。
/// </summary>
public class MaterialFolderOpener
{
	/// <summary>スナックバーサービス。</summary>
	private readonly ISnackbarService snackbarService;

	/// <summary>
	/// <see cref="MaterialFolderOpener"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="snackbarService">スナックバーサービス。</param>
	public MaterialFolderOpener(ISnackbarService snackbarService)
	{
		this.snackbarService = snackbarService;
	}

	/// <summary>
	/// 指定された <see cref="MangaSource"/> の素材フォルダを Explorer で開きます。
	/// フォルダが存在しない場合は Snackbar で通知します。
	/// </summary>
	/// <param name="source">開くフォルダの情報。</param>
	public async Task OpenAsync(MangaSource source)
	{
		if (!Directory.Exists(source.Path))
		{
			this.snackbarService.Show(
				"素材フォルダを開けません",
				$"素材フォルダが見つかりません。\n{source.Path}",
				ControlAppearance.Danger,
				new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
				TimeSpan.MaxValue);
			return;
		}

		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = source.Path,
				UseShellExecute = true,
			});
		}
		catch
		{
			this.snackbarService.Show(
				"素材フォルダを開けません",
				$"フォルダを開く処理中にエラーが発生しました。\n{source.Path}",
				ControlAppearance.Danger,
				new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24 },
				TimeSpan.MaxValue);
		}

		await Task.CompletedTask;
	}
}
