using MangaBinder.Bindings;
using MangaBinder.Settings;

namespace MangaBinder.Helpers;

/// <summary>
/// 素材フォルダの利用可否を確認するヘルパークラスです。
/// </summary>
public static class MaterialSourceAvailabilityHelper
{
	/// <summary>
	/// 指定された作品の素材フォルダ利用可否を確認します。
	/// 以下の項目を同期的に確認します：
	/// ・Material ロールの MangaSource が存在するか
	/// ・対象ドライブが Ready か
	/// ・Directory.Exists() で素材フォルダが存在するか
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <returns>素材フォルダの利用可否確認結果。</returns>
	public static MaterialSourceAvailabilityResult CheckAvailability(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		// Material ロールの所在情報を取得
		var materialSource = series.Sources.FirstOrDefault(s => s.Role == FolderRole.Material);

		if (materialSource == null)
		{
			return new MaterialSourceAvailabilityResult
			{
				Status = MaterialFolderStatus.NoMaterialSource,
				TargetPath = null,
			};
		}

		var materialPath = materialSource.Path;

		// DriveInfo.IsReady をチェック
		var drive = new DriveInfo(materialPath);
		if (!drive.IsReady)
		{
			return new MaterialSourceAvailabilityResult
			{
				Status = MaterialFolderStatus.DriveNotReady,
				TargetPath = materialPath,
			};
		}

		// パスが存在するかチェック
		if (!Directory.Exists(materialPath))
		{
			return new MaterialSourceAvailabilityResult
			{
				Status = MaterialFolderStatus.MaterialSourceNotFound,
				TargetPath = materialPath,
			};
		}

		// 正常系
		return new MaterialSourceAvailabilityResult
		{
			Status = MaterialFolderStatus.Success,
			TargetPath = materialPath,
		};
	}
}
