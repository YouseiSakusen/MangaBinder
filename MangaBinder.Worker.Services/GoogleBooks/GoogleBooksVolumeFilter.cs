using MangaBinder.Settings;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using ZLogger;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books API レスポンス候補の正規化・評価・選定を行うフィルタクラスです。
/// </summary>
public class GoogleBooksVolumeFilter
{
	/// <summary>「第N話 / N話」系タイトルを除外するコード固定 Regex です。</summary>
	private static readonly Regex EpisodeTitleRegex = new(
		@"[ \u3000]第\s*\d{1,3}\s*話|[ \u3000]\d{1,3}\s*話",
		RegexOptions.Compiled);

	/// <summary>末尾の巻番号を分離する Regex です。</summary>
	private static readonly Regex TrailingDigitsRegex = new(
		@"[\s\u3000(\uff08](\d+)[\s\u3000)\uff09]?\s*$",
		RegexOptions.Compiled);

	/// <summary>宣伝文句を除去する Regex です。</summary>
	private static readonly Regex PromoRegex = new(
		@"[\u300c\u300e\uff08(].*?[\u300d\u300f\uff09)]",
		RegexOptions.Compiled);

	/// <summary>日本語文字を含むか判定する Regex です。</summary>
	private static readonly Regex JapaneseRegex = new(
		@"[\u3040-\u309f\u30a0-\u30ff\u4e00-\u9fff]",
		RegexOptions.Compiled);

	/// <summary>サブタイトル区切り文字の Regex です。</summary>
	private static readonly Regex SubtitleSeparatorRegex = new(
		@"[\s\u3000][-\u2015\u2014~\uff5e::\uff1a]+[\s\u3000]",
		RegexOptions.Compiled);

	/// <summary>Comics カテゴリのキーワード一覧です。</summary>
	private static readonly string[] ComicsKeywords = ["Comics", "Manga", "Comic", "\u30b3\u30df\u30c3\u30af", "\u6f2b\u753b"];

	/// <summary>除外ワード一覧。</summary>
	private IReadOnlyList<string> excludeWords;

	/// <summary>ロガー。</summary>
	private readonly ILogger<GoogleBooksVolumeFilter> logger;

	/// <summary>
	/// <see cref="GoogleBooksVolumeFilter"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="settings">Google Books API 設定。</param>
	/// <param name="logger">ロガー。</param>
	public GoogleBooksVolumeFilter(GoogleBooksSettings settings, ILogger<GoogleBooksVolumeFilter> logger)
	{
		this.excludeWords = settings.ExcludeWords;
		this.logger = logger;
	}

	/// <summary>
	/// Job 実行開始時に設定を更新します。
	/// </summary>
	/// <param name="newSettings">JSON から読み込んだ設定。</param>
	public void ApplySettings(GoogleBooksSettings newSettings)
		=> this.excludeWords = newSettings.ExcludeWords;

	/// <summary>
	/// GoogleBooksResponse の Items を NormalizedVolumeInfo のリストへ変換します。
	/// </summary>
	/// <param name="response">API レスポンス。</param>
	/// <returns>正規化済み候補一覧。</returns>
	public IReadOnlyList<NormalizedVolumeInfo> NormalizeCandidates(GoogleBooksResponse? response)
	{
		if (response?.Items is not { Count: > 0 } items)
			return [];

		return items
			.Where(i => i.VolumeInfo is not null)
			.Select(i => this.normalize(i.VolumeInfo!))
			.ToList();
	}

	/// <summary>
	/// レスポンス候補の中から「1巻あらすじ採用候補」を選定して返します。
	/// </summary>
	/// <param name="response">API レスポンス。</param>
	/// <param name="series">対象の漫画シリーズ。</param>
	/// <returns>採用候補。見つからない場合は null。</returns>
	public NormalizedVolumeInfo? SelectFirstVolumeDescriptionCandidate(
		GoogleBooksResponse? response,
		MangaSeries series)
	{
		var candidates = this.NormalizeCandidates(response);
		if (candidates.Count == 0)
			return null;

		var queryNorm = this.normalizeForMatch(series.Title);

		var evaluated = candidates
			.Select(c => (candidate: c, eval: this.evaluate(c, queryNorm)))
			.ToList();

		// 採用候補に絞る（除外でない + Description あり + 日本語あり + タイトル一致）
		var accepted = evaluated
			.Where(x => x.eval.IsAccepted)
			.ToList();

		if (accepted.Count == 0)
			return null;

		// 優先スコアリング：OrderNumber == 1 > HasSeries > HasCategory > FuzzyScore
		return accepted
			.OrderByDescending(x => x.eval.OrderNumber == 1 ? 10 : 0)
			.ThenByDescending(x => x.eval.HasSeries ? 5 : 0)
			.ThenByDescending(x => x.eval.HasCategory ? 3 : 0)
			.ThenByDescending(x => x.eval.StrictMatch ? 2 : 0)
			.ThenByDescending(x => x.eval.FuzzyScore)
			.First()
			.candidate;
	}

	/// <summary>
	/// 複数候補一覧を評価し、採用候補と集計 Reason を持つ選択結果を返します。
	/// </summary>
	/// <param name="candidates">全ページから蓄積した正規化済み候補一覧。</param>
	/// <param name="series">対象の漫画シリーズ。</param>
	/// <returns>選択結果。</returns>
	public GoogleBooksCandidateSelectionResult SelectFirstVolumeDescriptionCandidate(
		IReadOnlyList<NormalizedVolumeInfo> candidates,
		MangaSeries series)
	{
		if (candidates.Count == 0)
		{
			return new GoogleBooksCandidateSelectionResult
			{
				Candidate      = null,
				Reason         = "NoItems",
				ReasonSummary  = string.Empty,
				CandidateCount = 0,
				AcceptedCount  = 0,
			};
		}

		var queryNorm = this.normalizeForMatch(series.Title);

		var evaluated = candidates
			.Select(c => (candidate: c, eval: this.evaluate(c, queryNorm)))
			.ToList();

		var accepted = evaluated
			.Where(x => x.eval.IsAccepted)
			.ToList();

		var reasonSummary = this.buildReasonSummary(evaluated.Select(x => x.eval.Reason));

		if (accepted.Count > 0)
		{
			var best = accepted
				.OrderByDescending(x => x.eval.OrderNumber == 1 ? 10 : 0)
				.ThenByDescending(x => x.eval.HasSeries ? 5 : 0)
				.ThenByDescending(x => x.eval.HasCategory ? 3 : 0)
				.ThenByDescending(x => x.eval.StrictMatch ? 2 : 0)
				.ThenByDescending(x => x.eval.FuzzyScore)
				.First();

			return new GoogleBooksCandidateSelectionResult
			{
				Candidate      = best.candidate,
				Reason         = "Accepted",
				ReasonSummary  = reasonSummary,
				CandidateCount = candidates.Count,
				AcceptedCount  = accepted.Count,
			};
		}

		var representativeReason = this.pickRepresentativeReason(evaluated.Select(x => x.eval.Reason));

		return new GoogleBooksCandidateSelectionResult
		{
			Candidate      = null,
			Reason         = representativeReason,
			ReasonSummary  = reasonSummary,
			CandidateCount = candidates.Count,
			AcceptedCount  = 0,
		};
	}

	/// <summary>
	/// タイトルを比較用に正規化した文字列を返します（デバッグ・観察用）。
	/// </summary>
	/// <param name="title">正規化対象のタイトル文字列。</param>
	/// <returns>正規化済み文字列。</returns>
	public string NormalizeTitle(string title) => this.normalizeForMatch(title);

	/// <summary>
	/// 指定された候補を評価して <see cref="CandidateEvaluation"/> を返します。
	/// </summary>
	/// <param name="candidate">評価対象の候補。</param>
	/// <param name="queryNorm">クエリの正規化済み文字列。</param>
	/// <returns>評価結果。</returns>
	public CandidateEvaluation Evaluate(NormalizedVolumeInfo candidate, string queryNorm)
		=> this.evaluate(candidate, queryNorm);

	/// <summary>
	/// 生ボリューム情報を <see cref="NormalizedVolumeInfo"/> へ変換します。
	/// </summary>
	/// <param name="raw">変換元の生ボリューム情報。</param>
	/// <returns>正規化済みボリューム情報。</returns>
	private NormalizedVolumeInfo normalize(GoogleBooksVolumeInfo raw)
	{
		var identifiers = raw.IndustryIdentifiers?
			.Select(x => x.Identifier)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.ToArray() ?? Array.Empty<string>();

		DateTime? parsedDate = DateTime.TryParse(raw.PublishedDate, out var d) ? d : null;

		return new NormalizedVolumeInfo
		{
			Title               = raw.Title ?? string.Empty,
			Authors             = raw.Authors?.ToArray() ?? Array.Empty<string>(),
			Publisher           = raw.Publisher ?? string.Empty,
			PublishedDate       = raw.PublishedDate ?? string.Empty,
			ParsedPublishedDate = parsedDate,
			Description         = raw.Description ?? string.Empty,
			InfoLink            = raw.InfoLink ?? string.Empty,
			Categories          = raw.Categories?.ToArray() ?? Array.Empty<string>(),
			IndustryIdentifiers = identifiers,
			SeriesInfo          = raw.SeriesInfo,
			RawVolumeInfo       = raw,
			OrderNumber         = this.extractOrderNumber(raw),
		};
	}

	/// <summary>
	/// 生ボリューム情報からシリーズ内巻番号を抽出します。
	/// </summary>
	/// <param name="raw">変換元の生ボリューム情報。</param>
	/// <returns>抽出した巻番号。取得できない場合は null。</returns>
	private int? extractOrderNumber(GoogleBooksVolumeInfo raw)
	{
		// 1. SeriesInfo.bookDisplayNumber
		if (raw.SeriesInfo?.BookDisplayNumber is { } displayNum
			&& double.TryParse(displayNum, System.Globalization.NumberStyles.Any,
				System.Globalization.CultureInfo.InvariantCulture, out var dn)
			&& dn >= 1)
			return (int)dn;

		// 2. SeriesInfo.volumeSeries[0].orderNumber
		var vsOrder = raw.SeriesInfo?.VolumeSeries?.FirstOrDefault()?.OrderNumber;
		if (vsOrder is { } vo && vo >= 1)
			return (int)vo;

		// 3-7. SeriesInfo.AdditionalData 内の各フィールド
		if (raw.SeriesInfo?.AdditionalData is { } extra)
		{
			var n = this.tryGetIntFromAdditionalData(extra, "orderNumber")
				?? this.tryGetIntFromAdditionalData(extra, "bookDisplayNumber")
				?? this.tryGetIntFromAdditionalData(extra, "volume")
				?? this.tryGetIntFromAdditionalData(extra, "volumeNumber")
				?? this.tryGetIntFromAdditionalData(extra, "bookNumber")
				?? this.tryGetNestedOrderNumber(extra);

			if (n is { } val && val >= 1)
				return val;
		}

		// 8. タイトル末尾の数字から推定
		var titleTrail = TrailingDigitsRegex.Match(raw.Title ?? string.Empty);
		if (titleTrail.Success
			&& int.TryParse(titleTrail.Groups[1].Value, out var t)
			&& t >= 1 && t <= 200)
			return t;

		return null;
	}

	/// <summary>
	/// AdditionalData の指定キーから int 値を取り出します。
	/// Number 型 / String 型に対応します。
	/// </summary>
	private int? tryGetIntFromAdditionalData(
		Dictionary<string, System.Text.Json.JsonElement> data,
		string key)
	{
		if (!data.TryGetValue(key, out var elem))
			return null;

		return elem.ValueKind switch
		{
			System.Text.Json.JsonValueKind.Number
				when elem.TryGetDouble(out var d) => d >= 1 ? (int)d : null,
			System.Text.Json.JsonValueKind.String
				when double.TryParse(elem.GetString(),
					System.Globalization.NumberStyles.Any,
					System.Globalization.CultureInfo.InvariantCulture, out var s)
					=> s >= 1 ? (int)s : null,
			_ => null,
		};
	}

	/// <summary>
	/// AdditionalData 内の volumeInfo / volumeSeries オブジェクト内 orderNumber を探索します。
	/// </summary>
	private int? tryGetNestedOrderNumber(
		Dictionary<string, System.Text.Json.JsonElement> data)
	{
		foreach (var nestedKey in new[] { "volumeInfo", "volumeSeries" })
		{
			if (!data.TryGetValue(nestedKey, out var nested))
				continue;

			if (nested.ValueKind == System.Text.Json.JsonValueKind.Object)
			{
				if (nested.TryGetProperty("orderNumber", out var on))
				{
					if (on.ValueKind == System.Text.Json.JsonValueKind.Number
						&& on.TryGetDouble(out var d) && d >= 1)
						return (int)d;
					if (on.ValueKind == System.Text.Json.JsonValueKind.String
						&& double.TryParse(on.GetString(),
							System.Globalization.NumberStyles.Any,
							System.Globalization.CultureInfo.InvariantCulture, out var s)
						&& s >= 1)
						return (int)s;
				}
			}
			else if (nested.ValueKind == System.Text.Json.JsonValueKind.Array)
			{
				foreach (var item in nested.EnumerateArray())
				{
					if (item.ValueKind == System.Text.Json.JsonValueKind.Object
						&& item.TryGetProperty("orderNumber", out var on))
					{
						if (on.ValueKind == System.Text.Json.JsonValueKind.Number
							&& on.TryGetDouble(out var d) && d >= 1)
							return (int)d;
						if (on.ValueKind == System.Text.Json.JsonValueKind.String
							&& double.TryParse(on.GetString(),
								System.Globalization.NumberStyles.Any,
								System.Globalization.CultureInfo.InvariantCulture, out var s)
							&& s >= 1)
							return (int)s;
					}
				}
			}
		}

		return null;
	}

	/// <summary>
	/// 候補を評価して <see cref="CandidateEvaluation"/> を返します。
	/// </summary>
	/// <param name="c">評価対象の候補。</param>
	/// <param name="queryNorm">クエリの正規化済み文字列。</param>
	/// <returns>評価結果。</returns>
	private CandidateEvaluation evaluate(NormalizedVolumeInfo c, string queryNorm)
	{
		var hasCategory    = this.categoriesContainComics(c.Categories);
		var hasSeries      = c.SeriesInfo is not null;
		var isExcluded     = this.isExcludedTitle(c.Title);
		var hasDescription = !string.IsNullOrWhiteSpace(c.Description);
		var descJapanese   = hasDescription && this.isDescriptionContainsJapanese(c.Description);
		var titleNorm      = this.normalizeForMatch(c.Title);
		var strictMatch    = this.titleMatchesQueryStrict(titleNorm, queryNorm);
		var partialMatch   = !strictMatch && this.titleMatchesQueryPartial(titleNorm, queryNorm);
		var fuzzyScore     = this.bigramSimilarity(titleNorm, queryNorm);
		var diffSubtitle   = this.hasDifferentSubtitle(titleNorm, queryNorm);

		if (isExcluded)
			return new(hasCategory, hasSeries, true, hasDescription, descJapanese,
				strictMatch, partialMatch, diffSubtitle, fuzzyScore, c.OrderNumber, "ExcludedTitle");

		if (!hasCategory)
		{
			this.logger.ZLogDebug(
				$"NoCategory Title={c.Title} Normalized={titleNorm} Categories=[{string.Join(" | ", c.Categories)}] Strict={strictMatch} Partial={partialMatch} Fuzzy={fuzzyScore:F4}");
			return new(hasCategory, hasSeries, false, hasDescription, descJapanese,
				strictMatch, partialMatch, diffSubtitle, fuzzyScore, c.OrderNumber, "NoCategory");
		}

		if (!hasSeries)
			return new(hasCategory, hasSeries, false, hasDescription, descJapanese,
				strictMatch, partialMatch, diffSubtitle, fuzzyScore, c.OrderNumber, "NoSeries");

		if (c.OrderNumber != 1)
			return new(hasCategory, hasSeries, false, hasDescription, descJapanese,
				strictMatch, partialMatch, diffSubtitle, fuzzyScore, c.OrderNumber, "NotFirstVolume");

		if (!hasDescription)
			return new(hasCategory, hasSeries, false, false, false,
				strictMatch, partialMatch, diffSubtitle, fuzzyScore, c.OrderNumber, "NoDescription");

		if (!descJapanese)
			return new(hasCategory, hasSeries, false, true, false,
				strictMatch, partialMatch, diffSubtitle, fuzzyScore, c.OrderNumber, "DescriptionNotJapanese");

		var fuzzyMatch = fuzzyScore >= 0.7;

		if (!strictMatch && !partialMatch && !fuzzyMatch)
			return new(hasCategory, hasSeries, false, true, true,
				false, false, diffSubtitle, fuzzyScore, c.OrderNumber, "TitleMismatch");

		if (diffSubtitle)
			return new(hasCategory, hasSeries, false, true, true,
				strictMatch, partialMatch, true, fuzzyScore, c.OrderNumber, "DifferentSubtitle");

		return new(hasCategory, hasSeries, false, true, true,
			strictMatch, partialMatch, false, fuzzyScore, c.OrderNumber, "Accepted");
	}

	/// <summary>
	/// タイトルを比較用に正規化します。
	/// 全角英数を半角へ変換し、宣伝文句・記号・スペースを除去して大文字化します。
	/// </summary>
	/// <param name="title">正規化対象のタイトル文字列。</param>
	/// <returns>正規化済み文字列。</returns>
	private string normalizeForMatch(string title)
	{
		if (string.IsNullOrEmpty(title)) return string.Empty;

		// 全角英数を半角へ
		var s = System.Globalization.StringInfo.GetTextElementEnumerator(title);
		var sb = new System.Text.StringBuilder();
		while (s.MoveNext())
		{
			var c = s.GetTextElement();
			if (c.Length == 1)
			{
				var ch = c[0];
				// 全角英数 → 半角
				if (ch >= '\uff01' && ch <= '\uff5e')
					sb.Append((char)(ch - 0xFEE0));
				// 全角スペース → 半角
				else if (ch == '\u3000')
					sb.Append(' ');
				else
					sb.Append(ch);
			}
			else
			{
				sb.Append(c);
			}
		}

		var result = sb.ToString();
		// 宣伝文句除去
		result = PromoRegex.Replace(result, string.Empty);
		// 記号・スペース除去
		result = Regex.Replace(result, @"[\s\u3000\-\u30fc!!\?\?\u266a\u2605\u2606\u2665\u2666]+", string.Empty);
		return result.ToUpperInvariant();
	}

	/// <summary>
	/// タイトルから宣伝文句と末尾の巻番号を除去します。
	/// </summary>
	/// <param name="title">処理対象のタイトル文字列。</param>
	/// <returns>宣伝文句と末尾巻番号を除去した文字列。</returns>
	private string removePromoAndSeparateTrailingDigits(string title)
	{
		var s = PromoRegex.Replace(title, string.Empty).Trim();
		s = TrailingDigitsRegex.Replace(s, string.Empty).Trim();
		return s;
	}

	/// <summary>
	/// 正規化済みタイトルがクエリと完全一致するかどうかを返します。
	/// </summary>
	/// <param name="titleNorm">正規化済みタイトル。</param>
	/// <param name="queryNorm">正規化済みクエリ。</param>
	/// <returns>完全一致する場合は true。</returns>
	private bool titleMatchesQueryStrict(string titleNorm, string queryNorm)
		=> titleNorm == queryNorm;

	/// <summary>
	/// 正規化済みタイトルがクエリと部分一致するかどうかを返します。
	/// 末尾の巻番号を除いた比較および前方一致を含みます。
	/// </summary>
	/// <param name="titleNorm">正規化済みタイトル。</param>
	/// <param name="queryNorm">正規化済みクエリ。</param>
	/// <returns>部分一致する場合は true。</returns>
	private bool titleMatchesQueryPartial(string titleNorm, string queryNorm)
	{
		if (string.IsNullOrEmpty(titleNorm) || string.IsNullOrEmpty(queryNorm))
			return false;

		// タイトル末尾の巻番号を除いた比較
		var titleBase = TrailingDigitsRegex.Replace(titleNorm, string.Empty).Trim();
		var queryBase = TrailingDigitsRegex.Replace(queryNorm, string.Empty).Trim();
		return titleBase == queryBase
			|| titleNorm.StartsWith(queryNorm, StringComparison.Ordinal)
			|| queryNorm.StartsWith(titleNorm, StringComparison.Ordinal);
	}

	/// <summary>
	/// バイグラム類似度によるファジー一致を判定します。
	/// </summary>
	/// <param name="titleNorm">正規化済みタイトル。</param>
	/// <param name="queryNorm">正規化済みクエリ。</param>
	/// <param name="threshold">採用とみなす類似度の閾値（既定値 0.6）。</param>
	/// <returns>類似度が閾値以上の場合は true。</returns>
	private bool titleMatchesQueryFuzzy(string titleNorm, string queryNorm, double threshold = 0.6)
		=> this.bigramSimilarity(titleNorm, queryNorm) >= threshold;

	/// <summary>
	/// 2 つの文字列間のバイグラム類似度（Dice 係数）を算出します。
	/// </summary>
	/// <param name="a">比較文字列 A。</param>
	/// <param name="b">比較文字列 B。</param>
	/// <returns>0.0〜1.0 の類似度スコア。</returns>
	private double bigramSimilarity(string a, string b)
	{
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

		var bigramsA = this.getBigrams(a);
		var bigramsB = this.getBigrams(b);

		if (bigramsA.Count == 0 || bigramsB.Count == 0) return 0.0;

		var intersection = bigramsA.Intersect(bigramsB).Count();
		return 2.0 * intersection / (bigramsA.Count + bigramsB.Count);
	}

	/// <summary>
	/// 文字列からバイグラム（2 文字単位）のリストを生成します。
	/// </summary>
	/// <param name="s">対象文字列。</param>
	/// <returns>バイグラムのリスト。</returns>
	private List<string> getBigrams(string s)
	{
		var bigrams = new List<string>(Math.Max(0, s.Length - 1));
		for (var i = 0; i < s.Length - 1; i++)
			bigrams.Add(s.Substring(i, 2));
		return bigrams;
	}

	/// <summary>
	/// タイトルにサブタイトル区切りが含まれ、ベース部分がクエリと一致しない場合に true を返します。
	/// </summary>
	/// <param name="titleNorm">正規化済みタイトル。</param>
	/// <param name="queryNorm">正規化済みクエリ。</param>
	/// <returns>異なるサブタイトルを持つ場合は true。</returns>
	private bool hasDifferentSubtitle(string titleNorm, string queryNorm)
	{
		// タイトルにサブタイトル区切りが含まれ、ベース部分がクエリと一致しない場合に true
		var rawTitle = titleNorm;
		if (!SubtitleSeparatorRegex.IsMatch(rawTitle)) return false;

		var baseTitle = SubtitleSeparatorRegex.Split(rawTitle).FirstOrDefault() ?? rawTitle;
		return baseTitle != queryNorm;
	}

	/// <summary>
	/// Reason 一覧から集計サマリ文字列を生成します。
	/// 例: "NoCategory:14; NotFirstVolume:5; ExcludedTitle:3"
	/// </summary>
	private string buildReasonSummary(IEnumerable<string> reasons)
	{
		var grouped = reasons
			.GroupBy(r => r)
			.OrderByDescending(g => g.Count());

		return string.Join("; ", grouped.Select(g => $"{g.Key}:{g.Count()}"));
	}

	/// <summary>
	/// Reason 一覧から優先順位に従って代表 Reason を返します。
	/// </summary>
	private string pickRepresentativeReason(IEnumerable<string> reasons)
	{
		var priority = new[]
		{
			"NoItems",
			"ExcludedTitle",
			"NoCategory",
			"NoSeries",
			"NotFirstVolume",
			"NoDescription",
			"DescriptionNotJapanese",
			"TitleMismatch",
			"DifferentSubtitle",
			"NoCandidatePassed",
		};

		var reasonSet = reasons.ToHashSet();
		foreach (var p in priority)
		{
			if (reasonSet.Contains(p))
				return p;
		}

		return reasonSet.FirstOrDefault() ?? "NoCandidatePassed";
	}

	/// <summary>
	/// カテゴリ一覧に Comics / Manga 系キーワードが含まれるかどうかを返します。
	/// </summary>
	/// <param name="categories">カテゴリ一覧。</param>
	/// <returns>Comics カテゴリを含む場合は true。</returns>
	private bool categoriesContainComics(string[] categories)
		=> categories.Any(c => ComicsKeywords.Any(k =>
			c.Contains(k, StringComparison.OrdinalIgnoreCase)));

	/// <summary>
	/// あらすじに日本語文字（ひらがな・カタカナ・漢字）が含まれるかどうかを返します。
	/// </summary>
	/// <param name="description">判定対象のあらすじ文字列。</param>
	/// <returns>日本語を含む場合は true。</returns>
	private bool isDescriptionContainsJapanese(string description)
		=> JapaneseRegex.IsMatch(description);

	/// <summary>
	/// タイトルが除外対象かどうかを返します。
	/// 「第N話 / N話」形式の固定 Regex、および設定の除外ワードを判定します。
	/// </summary>
	/// <param name="title">判定対象のタイトル文字列。</param>
	/// <returns>除外対象の場合は true。</returns>
	private bool isExcludedTitle(string title)
	{
		if (EpisodeTitleRegex.IsMatch(title))
			return true;

		return this.excludeWords.Any(w =>
			title.Contains(w, StringComparison.OrdinalIgnoreCase));
	}
}
