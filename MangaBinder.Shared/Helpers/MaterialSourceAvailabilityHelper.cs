using MangaBinder.Bindings;
using MangaBinder.Settings;

namespace MangaBinder.Helpers;

/// <summary>
/// 素材フォルダの利用可否を確認するヘルパークラスです。
/// </summary>
public static class MaterialSourceAvailabilityHelper
{
	/// <summary>
	/// 指定された作品について、実在する物理素材フォルダに対応する Material ロールの MangaSource を取得します。
	/// DB の登録件数ではなく <see cref="Directory.Exists(string?)"/> による実在確認で判定し、
	/// 同じ物理フォルダを指す複数レコードは最初の 1 件に集約します。
	/// <see cref="FolderRole.Binding"/> は対象に含めません。
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <returns>実在する物理素材フォルダごとの MangaSource（物理フォルダが重複するレコードは除外）。</returns>
	public static IReadOnlyList<MangaSource> GetExistingMaterialSources(MangaSeries series)
	{
		ArgumentNullException.ThrowIfNull(series);

		var existingSources = new List<MangaSource>();
		var normalizedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var source in series.Sources.Where(s => s.Role == FolderRole.Material))
		{
			if (!Directory.Exists(source.Path))
			{
				continue;
			}

			// 末尾区切り文字を除去した絶対パスで同一フォルダを判定
			var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.Path));
			if (normalizedPaths.Add(normalizedPath))
			{
				existingSources.Add(source);
			}
		}

		return existingSources;
	}

	/// <summary>
	/// 指定された作品に、実在する物理素材フォルダが複数存在するかどうかを判定します。
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <returns>実在する物理素材フォルダが 2 件以上ある場合は true。</returns>
	public static bool HasMultipleExistingMaterialFolders(MangaSeries series)
	{
		return GetExistingMaterialSources(series).Count >= 2;
	}

	/// <summary>
	/// 編集・保存の対象とする Material ロールの MangaSource を決定します。
	/// 実在する物理素材フォルダが 1 件ならその MangaSource を返します（古い DB レコードが先頭に残っていても実在する 1 件を返します）。
	/// 実在 0 件の場合は、Material レコードがちょうど 1 件のときだけそのレコードを返し、それ以外は null を返します。
	/// </summary>
	/// <param name="series">対象の作品。</param>
	/// <returns>対象の MangaSource。素材ソースがない場合は null。</returns>
	/// <exception cref="InvalidOperationException">実在する物理素材フォルダが 2 件以上ある場合。</exception>
	public static MangaSource? ResolveEditingMaterialSource(MangaSeries series)
	{
		var existingSources = GetExistingMaterialSources(series);

		return existingSources.Count switch
		{
			0 => series.SingleMaterialSource,
			1 => existingSources[0],
			_ => throw new InvalidOperationException("実在する素材フォルダが複数存在するため、編集対象の素材ソースを特定できません。"),
		};
	}

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
