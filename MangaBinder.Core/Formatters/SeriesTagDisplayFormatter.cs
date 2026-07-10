using MangaBinder.Tags;

namespace MangaBinder.Core.Formatters;

/// <summary>
/// 作品のタグ表示テキストをフォーマットするユーティリティクラスです。
/// </summary>
public static class SeriesTagDisplayFormatter
{
	/// <summary>
	/// タグ一覧を表示用テキストにフォーマットします。
	/// </summary>
	/// <param name="tags">フォーマット対象のタグ一覧。</param>
	/// <returns>フォーマット済みの表示テキスト。</returns>
	public static string Format(IEnumerable<MangaTag> tags)
	{
		var tagList = tags.ToList();

		if (tagList.Count == 0)
			return "⊕ タグを付ける";

		if (tagList.Count == 1)
			return $"🏷 {tagList[0].Name}";

		return $"🏷 {tagList[0].Name} +{tagList.Count - 1}";
	}

	/// <summary>
	/// タグ一覧を StartPage 用表示テキストにフォーマットします。
	/// タグが0件の場合は「🏷 タグ無し」を返します。
	/// </summary>
	/// <param name="tags">フォーマット対象のタグ一覧。</param>
	/// <returns>フォーマット済みの表示テキスト。</returns>
	public static string FormatForStartPage(IEnumerable<MangaTag> tags)
	{
		var tagList = tags.ToList();

		if (tagList.Count == 0)
			return "🏷 タグ無し";

		return Format(tagList);
	}
}
