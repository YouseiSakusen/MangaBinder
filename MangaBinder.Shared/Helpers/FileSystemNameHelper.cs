using System.Text.RegularExpressions;

namespace MangaBinder.Helpers;

/// <summary>
/// ディスク上に実在する MangaBinder のファイル・フォルダ名を解析するヘルパークラスです。
/// 製本済みファイル名・素材フォルダ名の解析責務を担当します。
/// </summary>
public static class FileSystemNameHelper
{
	/// <summary>先頭の [作者名] を抽出する正規表現です。</summary>
	private static readonly Regex AuthorPattern =
		new(@"^\[(?<author>[^\]]+)\]\s*", RegexOptions.Compiled);

	/// <summary>
	/// 製本済みファイル名の巻数表記を抽出する正規表現です。
	/// <list type="bullet">
	///   <item><c>第n-全m巻</c>: start=n, bound=m, 完結あり</item>
	///   <item><c>第n-m巻</c>: start=n, bound=m</item>
	///   <item><c>全n巻</c>: bound=n, 完結あり（start なし）</item>
	///   <item><c>第n巻</c>: start=n（単巻）</item>
	/// </list>
	/// </summary>
	private static readonly Regex BindingVolumePattern = new(
		@"第(?<start>\d+)[-－]\s*全(?<bound>\d+)巻" +
		@"|第(?<start>\d+)[-－]\s*(?<bound>\d+)巻" +
		@"|全(?<bound>\d+)巻" +
		@"|第(?<start>\d+)巻",
		RegexOptions.Compiled);

	/// <summary>括弧あり「 （全n巻）」を末尾で捉える正規表現です。</summary>
	private static readonly Regex MaterialParenPattern =
		new(@" （全(?<vol>\d+)巻）$", RegexOptions.Compiled);

	/// <summary>括弧なし「 全n巻」を末尾で捉える正規表現です。</summary>
	private static readonly Regex MaterialBarePattern =
		new(@" 全(?<vol>\d+)巻$", RegexOptions.Compiled);

	/// <summary>
	/// ファイル・フォルダ名の先頭に存在する [作者] プレフィックスを抽出します。
	/// </summary>
	/// <remarks>
	/// <para>
	/// 作者プレフィックスの形式は以下のいずれでも解析可能です（既存の製本ファイル互換）。
	/// </para>
	/// <list type="bullet">
	///   <item><c>[作者]タイトル</c></item>
	///   <item><c>[作者] タイトル</c></item>
	///   <item><c>[作者]    タイトル</c></item>
	/// </list>
	/// <para>
	/// 作者プレフィックスが存在しない場合、author は空文字列、remainingName は元の名前を Trim した値です。
	/// </para>
	/// </remarks>
	/// <param name="name">ファイル・フォルダ名（拡張子なし）。</param>
	/// <param name="author">抽出された作者名。プレフィックスなしの場合は空文字列。</param>
	/// <param name="remainingName">先頭プレフィックスを除いた残り。</param>
	/// <returns>プレフィックスが存在したら true、それ以外は false。</returns>
	public static bool TryExtractAuthorPrefix(
		string name,
		out string author,
		out string remainingName)
	{
		var match = AuthorPattern.Match(name);
		if (match.Success)
		{
			author = match.Groups["author"].Value.Trim();
			remainingName = name[match.Length..].Trim();
			return true;
		}

		author = string.Empty;
		remainingName = name.Trim();
		return false;
	}

	/// <summary>
	/// 製本済みファイル名を解析し、<see cref="MangaSeries"/> を生成します。
	/// <para>対応形式例: <c>[著者名] タイトル 第01-10巻.zip</c>、<c>[著者名] タイトル 第01-全10巻.zip</c></para>
	/// </summary>
	/// <param name="rawName">拡張子を含む元のファイル名。</param>
	/// <returns>解析結果を格納した <see cref="MangaSeries"/>。</returns>
	public static MangaSeries ParseAsBinding(string rawName, string separatorChars = "")
	{
		// 拡張子を除去
		var stem = Path.GetFileNameWithoutExtension(rawName);

		// 先頭の [作者] を抽出
		TryExtractAuthorPrefix(stem, out var author, out var remaining);

		// 巻数表記を抽出（第n-全m巻 / 第n-m巻 / 全n巻 / 第n巻）
		var volMatch = BindingVolumePattern.Match(remaining);

		var startVolume = 0;
		var boundEndVolume = 0;
		var endVolume = 0;
		var seriesCompleted = false;

		if (volMatch.Success)
		{
			// マッチ文字列に「全」が含まれる場合は完結扱い
			seriesCompleted = volMatch.Value.Contains('全');

			if (volMatch.Groups["start"].Value is { Length: > 0 } startStr)
				startVolume = int.Parse(startStr);

			if (volMatch.Groups["bound"].Value is { Length: > 0 } boundStr)
				boundEndVolume = int.Parse(boundStr);
			else
				// 単巻（第n巻）の場合は start をそのまま BoundEndVolume にも格納
				boundEndVolume = startVolume;

			// 「全」が含まれる場合は EndVolume にも総巻数を格納
			if (seriesCompleted)
				endVolume = boundEndVolume;
		}

		// 巻数表記より前をタイトルとして切り出す
		var titleRaw = volMatch.Success
			? remaining[..volMatch.Index].Trim()
			: remaining.Trim();

		return new MangaSeries
		{
			Title = titleRaw,
			NormalizedTitleInternal = MangaTitleHelper.NormalizeTitleInternal(titleRaw),
			ShortTitle = MangaTitleHelper.GetShortTitle(titleRaw, separatorChars),
			Author = author,
			SeriesCompleted = seriesCompleted,
			StartVolume = startVolume,
			EndVolume = endVolume,
			BoundEndVolume = boundEndVolume,
			HasNestedArchive = false,
			ManuallyEditedAt = null,
			IsOwnedMaxVolumeManuallyEdited = false,
		};
	}

	/// <summary>
	/// 素材フォルダ名を解析し、<see cref="MangaSeries"/> を生成します。
	/// <para>対応形式例: <c>タイトル 全4巻</c>、<c>[作者] タイトル 全4巻</c>、<c>#タイトル （全3巻）</c></para>
	/// </summary>
	/// <param name="rawName">素材フォルダの元の名前。</param>
	/// <returns>解析結果を格納した <see cref="MangaSeries"/>。</returns>
	public static MangaSeries ParseAsMaterial(string rawName, string separatorChars = "")
	{
		// 先頭の [作者] を抽出
		TryExtractAuthorPrefix(rawName, out var author, out var remaining);

		// 括弧あり「 （全n巻）」: SeriesCompleted=true, IsOwnedCompleted=false
		var parenMatch = MaterialParenPattern.Match(remaining);
		if (parenMatch.Success)
		{
			var titleRaw = remaining[..parenMatch.Index].Trim();
			return new MangaSeries
			{
				Title = titleRaw,
				NormalizedTitleInternal = MangaTitleHelper.NormalizeTitleInternal(titleRaw),
				ShortTitle = MangaTitleHelper.GetShortTitle(titleRaw, separatorChars),
				Author = author,
				SeriesCompleted = true,
				IsOwnedCompleted = false,
				StartVolume = 0,
				EndVolume = int.Parse(parenMatch.Groups["vol"].Value),
				HasNestedArchive = false,
				ManuallyEditedAt = null,
				IsOwnedMaxVolumeManuallyEdited = false,
			};
		}

		// 括弧なし「 全n巻」: SeriesCompleted=true, IsOwnedCompleted=true
		var bareMatch = MaterialBarePattern.Match(remaining);
		if (bareMatch.Success)
		{
			var titleRaw = remaining[..bareMatch.Index].Trim();
			return new MangaSeries
			{
				Title = titleRaw,
				NormalizedTitleInternal = MangaTitleHelper.NormalizeTitleInternal(titleRaw),
				ShortTitle = MangaTitleHelper.GetShortTitle(titleRaw, separatorChars),
				Author = author,
				SeriesCompleted = true,
				IsOwnedCompleted = true,
				StartVolume = 0,
				EndVolume = int.Parse(bareMatch.Groups["vol"].Value),
				HasNestedArchive = false,
				ManuallyEditedAt = null,
				IsOwnedMaxVolumeManuallyEdited = false,
			};
		}

		// 表記なし
		var title = remaining.Trim();
		return new MangaSeries
		{
			Title = title,
			NormalizedTitleInternal = MangaTitleHelper.NormalizeTitleInternal(title),
			ShortTitle = MangaTitleHelper.GetShortTitle(title, separatorChars),
			Author = author,
			SeriesCompleted = false,
			IsOwnedCompleted = false,
			StartVolume = 0,
			EndVolume = 0,
			HasNestedArchive = false,
			ManuallyEditedAt = null,
			IsOwnedMaxVolumeManuallyEdited = false,
		};
	}
}
