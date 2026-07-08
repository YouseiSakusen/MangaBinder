using MangaBinder.Bindings;
using MangaBinder.Core.Series;

namespace MangaBinder.Series;

/// <summary>
/// 素材ファイル一覧表示用の表示モデルです。
/// </summary>
public sealed class MaterialFileItemViewModel
{
	/// <summary>ファイル名を取得します。</summary>
	public string FileName { get; init; } = "";

	/// <summary>サイズ表示文字列を取得します。</summary>
	public string SizeText { get; init; } = "";

	/// <summary>素材アイテムの種別を取得します。</summary>
	public MaterialItemType ItemType { get; init; }

	/// <summary>実ファイルまたは実フォルダのフルパスを取得します。</summary>
	public string FullPath { get; init; } = "";

	/// <summary>削除可能かどうかを示します。既存素材フォルダ由来は false、D&D 追加素材は true（将来用）。</summary>
	public bool CanRemove { get; init; }

	/// <summary>
	/// MaterialFileItem から MaterialFileItemViewModel を生成します。
	/// </summary>
	/// <param name="item">DTO オブジェクト。</param>
	/// <returns>生成された ViewModel。</returns>
	public static MaterialFileItemViewModel FromDto(MaterialFileItem item)
	{
		ArgumentNullException.ThrowIfNull(item);

		// サイズ表示テキストの生成
		string sizeText = item.ItemType == MaterialItemType.Folder
			? "-"
			: FormatFileSize(item.SizeBytes ?? 0);

		return new MaterialFileItemViewModel
		{
			FileName = item.Name,
			SizeText = sizeText,
			ItemType = item.ItemType,
			FullPath = item.FullPath,
			CanRemove = item.CanRemove,
		};
	}

	/// <summary>
	/// バイトサイズを人間が読みやすい形式に変換します。
	/// </summary>
	/// <param name="bytes">バイト数。</param>
	/// <returns>フォーマット済みサイズ文字列（例："1.5 MB"）。</returns>
	private static string FormatFileSize(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB", "TB" };
		double len = bytes;
		int order = 0;

		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len /= 1024;
		}

		return len.ToString("0.##") + " " + sizes[order];
	}
}
