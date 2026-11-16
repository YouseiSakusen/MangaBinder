using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MangaBinder.Helpers;

/// <summary>
/// 編集開始前の判定結果に対する UI 通知を共通化するヘルパークラスです。
/// </summary>
public static class EditPrepareNotificationHelper
{
	/// <summary>
	/// 素材フォルダが複数登録されている場合のエラー Snackbar を表示します。
	/// ユーザーが閉じるまで表示し続けます。
	/// </summary>
	/// <param name="snackbarService">Snackbar 通知サービス。</param>
	public static void ShowMultipleMaterialFoldersSnackbar(ISnackbarService snackbarService)
	{
		snackbarService.Show(
			"素材フォルダが複数登録されています",
			"MangaBinderでは、1つの作品の素材を複数フォルダに分けて管理できません。\nエクスプローラー等で1つのフォルダに統合した後に再度実行してください。\n※使用しないフォルダは削除してください",
			ControlAppearance.Danger,
			new SymbolIcon { Symbol = SymbolRegular.Warning24 },
			TimeSpan.MaxValue);
	}
}
