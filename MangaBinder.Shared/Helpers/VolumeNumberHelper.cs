using System.Text;
using System.Text.RegularExpressions;

namespace MangaBinder.Helpers;

/// <summary>
/// 素材名から巻情報を解釈するヘルパークラスです。
/// 純粋な文字列解析のみを行います。
/// </summary>
public static partial class VolumeNumberHelper
{
	/// <summary>
	/// 素材名から巻情報を解析します。
	/// </summary>
	/// <param name="materialName">解析対象の素材名。</param>
	/// <param name="sourceType">解析元の種別。</param>
	/// <returns>解析結果。</returns>
	public static VolumeNumberParseResult Parse(string materialName, VolumeNumberSourceType sourceType)
	{
		if (string.IsNullOrWhiteSpace(materialName))
		{
			return new VolumeNumberParseResult { Kind = VolumeNumberParseKind.Unknown };
		}

		// FormKC 正規化：全角数字→半角、全角英字→半角、互換文字→標準形
		var normalized = materialName.Normalize(NormalizationForm.FormKC);

		// 1. 最初に「巻ではない」表現を判定
		var notVolumeResult = tryParseNotVolume(normalized);
		if (notVolumeResult != null)
		{
			return notVolumeResult;
		}

		// 2. 日付を除外
		var withoutDates = RemoveDatePatterns(normalized);

		// 3. Range パターンを先に評価（v01-08 を v01 と誤認しないため）
		var rangeResult = tryParseRange(withoutDates);
		if (rangeResult != null)
		{
			return rangeResult;
		}

		// 4. 明示的な単巻パターンを評価
		var singleResult = tryParseSingle(withoutDates);
		if (singleResult != null)
		{
			return singleResult;
		}

		// 5. SourceType別の弱いパターンを評価
		var weakResult = tryParseWeakPattern(withoutDates, sourceType);
		if (weakResult != null)
		{
			return weakResult;
		}

		// 6. パターンに該当しなかった
		return new VolumeNumberParseResult { Kind = VolumeNumberParseKind.Unknown };
	}

	/// <summary>
	/// 「巻ではない」表現を判定します。
	/// </summary>
	private static VolumeNumberParseResult? tryParseNotVolume(string normalized)
	{
		// Chapter / Chap / 話 の除外パターン
		if (RegexNotVolume().IsMatch(normalized))
		{
			return new VolumeNumberParseResult { Kind = VolumeNumberParseKind.NotVolume, MatchedPattern = "NotVolume" };
		}

		return null;
	}

	/// <summary>
	/// Range パターン（v01-08 等）を解析します。
	/// </summary>
	private static VolumeNumberParseResult? tryParseRange(string normalized)
	{
		// 全26巻 パターン（1～26を表す情報として Range として扱う）
		var completeMatch = RegexCompleteVolume().Match(normalized);
		if (completeMatch.Success)
		{
			var numStr = completeMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var endNum))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Range,
					RangeStart = 1,
					RangeEnd = endNum,
					MatchedPattern = "CompleteVolume",
				};
			}
		}

		// vl 01-07 パターン（vl プレフィックス + 範囲）
		var vlMatch = RegexVlRange().Match(normalized);
		if (vlMatch.Success)
		{
			var startStr = vlMatch.Groups["start"].Value.Replace('_', '.');
			var endStr = vlMatch.Groups["end"].Value.Replace('_', '.');

			if (TryParseVolumeNumber(startStr, out var startNum) &&
				TryParseVolumeNumber(endStr, out var endNum))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Range,
					RangeStart = startNum,
					RangeEnd = endNum,
					MatchedPattern = "VlRange",
				};
			}
		}

		// vol パターンの lose バージョン (vol -01-03) 対応
		var volLooseMatch = RegexVolRangeLoose().Match(normalized);
		if (volLooseMatch.Success)
		{
			var startStr = volLooseMatch.Groups["start"].Value.Replace('_', '.');
			var endStr = volLooseMatch.Groups["end"].Value.Replace('_', '.');

			if (TryParseVolumeNumber(startStr, out var startNum) &&
				TryParseVolumeNumber(endStr, out var endNum))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Range,
					RangeStart = startNum,
					RangeEnd = endNum,
					MatchedPattern = "VolRangeLoose",
				};
			}
		}

		// v01-08 / v01-08b / vol01-06 / vol 01-06 / vol.01-06 パターン
		var match = RegexVolumeRange().Match(normalized);
		if (match.Success)
		{
			var startStr = match.Groups["start"].Value.Replace('_', '.');
			var endStr = match.Groups["end"].Value.Replace('_', '.');

			if (TryParseVolumeNumber(startStr, out var startNum) &&
				TryParseVolumeNumber(endStr, out var endNum))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Range,
					RangeStart = startNum,
					RangeEnd = endNum,
					MatchedPattern = "VolumeRange",
				};
			}
		}

		return null;
	}

	/// <summary>
	/// 明示的な単巻パターンを解析します。
	/// </summary>
	private static VolumeNumberParseResult? tryParseSingle(string normalized)
	{
		// 日本語パターン：第01巻 / 第1巻 / 第1.5巻 / 01巻 / 1巻 / 1.5巻 / 0巻
		var jpMatch = RegexJapaneseVolume().Match(normalized);
		if (jpMatch.Success)
		{
			var numStr = jpMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "JapaneseVolume",
				};
			}
		}

		// vol パターン：vol01 / vol.01 / vol 01 / vol.1 / vol1.5
		var volMatch = RegexVolPattern().Match(normalized);
		if (volMatch.Success)
		{
			var numStr = volMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "VolPattern",
				};
			}
		}

		// v パターン：v01 / v1 / v1.5 / v01s / v03w（英字サフィックス対応）
		var vMatch = RegexVPattern().Match(normalized);
		if (vMatch.Success)
		{
			var numStr = vMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "VPattern",
				};
			}
		}

		// _v パターン：_v01 / _v1 / _v001
		var undeRscoreVMatch = RegexUnderscoreVPattern().Match(normalized);
		if (undeRscoreVMatch.Success)
		{
			var numStr = undeRscoreVMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "UnderscoreVPattern",
				};
			}
		}

		// 括弧パターン：(1) / (01) / (1.5) / （１） / （１.５）
		var parenMatch = RegexParenPattern().Match(normalized);
		if (parenMatch.Success)
		{
			var numStr = parenMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "ParenPattern",
				};
			}
		}

		// ローマ数字パターン：I～XV
		var romanMatch = RegexRomanNumeral().Match(normalized);
		if (romanMatch.Success)
		{
			var numStr = romanMatch.Groups["num"].Value;
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "RomanNumeral",
				};
			}
		}

		return null;
	}

	/// <summary>
	/// 巻番号文字列を decimal に変換します。
	/// 通常の数値とローマ数字の両方に対応します。
	/// </summary>
	/// <param name="raw">巻番号の文字列表現。</param>
	/// <param name="number">変換結果。</param>
	/// <returns>変換が成功した場合は true。</returns>
	public static bool TryParseVolumeNumber(string raw, out decimal number)
	{
		if (decimal.TryParse(raw, out number))
		{
			return true;
		}

		if (RomanNumeralMap.TryGetValue(raw.ToUpperInvariant(), out var roman))
		{
			number = roman;
			return true;
		}

		return false;
	}

	/// <summary>
	/// 日付パターンを素材名から除外します。
	/// 8桁数字（YYYYMMDD）と YYYY-MM-DD 形式を除去します。
	/// </summary>
	private static string RemoveDatePatterns(string normalized)
	{
		// YYYY-MM-DD 形式を除去（例：2020-02-25）
		var withoutDash = RegexDateDashFormat().Replace(normalized, " ");

		// 8桁数字の日付を除去（例：20210625）
		var withoutEightDigits = RegexDateEightDigits().Replace(withoutDash, " ");

		return withoutEightDigits;
	}

	/// <summary>
	/// SourceType別の弱いパターンを解析します。
	/// </summary>
	private static VolumeNumberParseResult? tryParseWeakPattern(string normalized, VolumeNumberSourceType sourceType)
	{
		return sourceType switch
		{
			VolumeNumberSourceType.Folder => tryParseWeakPatternFolder(normalized),
			VolumeNumberSourceType.Epub => tryParseWeakPatternEpub(normalized),
			VolumeNumberSourceType.Archive => tryParseWeakPatternArchive(normalized),
			_ => null,
		};
	}

	/// <summary>
	/// Folder の弱いパターンを解析します（後方優先）。
	/// </summary>
	private static VolumeNumberParseResult? tryParseWeakPatternFolder(string normalized)
	{
		// Folder では後方の有効候補を優先
		// タイトル末尾付近の数字から後ろを探す

		// タイトル末尾の裸数字：「3月のライオン 12」
		var trailingMatch = RegexTitleTrailingNumber().Match(normalized);
		if (trailingMatch.Success && isValidWeakVolumeNumber(trailingMatch.Groups["num"].Value))
		{
			var numStr = trailingMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "TitleTrailingNumber",
				};
			}
		}

		// コロン区切り：「タイトル：6」
		var colonMatch = RegexColonNumber().Match(normalized);
		if (colonMatch.Success && isValidWeakVolumeNumber(colonMatch.Groups["num"].Value))
		{
			var numStr = colonMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "ColonNumber",
				};
			}
		}

		// 「その N」表記
		var sonoMatch = RegexSonoNumber().Match(normalized);
		if (sonoMatch.Success && isValidWeakVolumeNumber(sonoMatch.Groups["num"].Value))
		{
			var numStr = sonoMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "SonoNumber",
				};
			}
		}

		// 残存する角括弧内の数値
		var bracketMatch = RegexBracketNumber().Match(normalized);
		if (bracketMatch.Success && isValidWeakVolumeNumber(bracketMatch.Groups["num"].Value))
		{
			var numStr = bracketMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "BracketNumber",
				};
			}
		}

		// 丸数字
		var circledMatch = RegexCircledNumber().Match(normalized);
		if (circledMatch.Success)
		{
			if (TryParseCircledNumber(circledMatch.Groups["vol"].Value, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "CircledNumber",
				};
			}
		}

		// 記号直後の巻数（! の直後）
		var exclamationMatch = RegexTitleSeparatorNumber().Match(normalized);
		if (exclamationMatch.Success && isValidWeakVolumeNumber(exclamationMatch.Groups["num"].Value))
		{
			var numStr = exclamationMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "TitleSeparatorNumber",
				};
			}
		}

		// タイトル + 空白 + 数字 + [ または (
		var titleSpaceNumberMatch = RegexTitleSpaceNumberBeforeParen().Match(normalized);
		if (titleSpaceNumberMatch.Success && isValidWeakVolumeNumber(titleSpaceNumberMatch.Groups["num"].Value))
		{
			var numStr = titleSpaceNumberMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "TitleSpaceNumberBeforeParen",
				};
			}
		}

		return null;
	}

	/// <summary>
	/// Epub の弱いパターンを解析します（後方優先、拡張子を除去して解析）。
	/// </summary>
	private static VolumeNumberParseResult? tryParseWeakPatternEpub(string normalized)
	{
		// .epub 拡張子を除去して解析
		var withoutExtension = Regex.Replace(normalized, @"\.epub$", "", RegexOptions.IgnoreCase);

		// Folder と同様の後方優先処理
		// タイトル末尾の裸数字
		var trailingMatch = RegexTitleTrailingNumber().Match(withoutExtension);
		if (trailingMatch.Success && isValidWeakVolumeNumber(trailingMatch.Groups["num"].Value))
		{
			var numStr = trailingMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "TitleTrailingNumber",
				};
			}
		}

		// コロン区切り
		var colonMatch = RegexColonNumber().Match(withoutExtension);
		if (colonMatch.Success && isValidWeakVolumeNumber(colonMatch.Groups["num"].Value))
		{
			var numStr = colonMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "ColonNumber",
				};
			}
		}

		// 「その N」
		var sonoMatch = RegexSonoNumber().Match(withoutExtension);
		if (sonoMatch.Success && isValidWeakVolumeNumber(sonoMatch.Groups["num"].Value))
		{
			var numStr = sonoMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "SonoNumber",
				};
			}
		}

		// 括弧付き
		var bracketMatch = RegexBracketNumber().Match(withoutExtension);
		if (bracketMatch.Success && isValidWeakVolumeNumber(bracketMatch.Groups["num"].Value))
		{
			var numStr = bracketMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "BracketNumber",
				};
			}
		}

		// 丸数字
		var circledMatch = RegexCircledNumber().Match(withoutExtension);
		if (circledMatch.Success)
		{
			if (TryParseCircledNumber(circledMatch.Groups["vol"].Value, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "CircledNumber",
				};
			}
		}

		// タイトル + 空白 + 数字 + [ または (
		var titleSpaceNumberMatch = RegexTitleSpaceNumberBeforeParen().Match(withoutExtension);
		if (titleSpaceNumberMatch.Success && isValidWeakVolumeNumber(titleSpaceNumberMatch.Groups["num"].Value))
		{
			var numStr = titleSpaceNumberMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "TitleSpaceNumberBeforeParen",
				};
			}
		}

		return null;
	}

	/// <summary>
	/// Archive の弱いパターンを解析します（タイトル有無両対応）。
	/// </summary>
	private static VolumeNumberParseResult? tryParseWeakPatternArchive(string normalized)
	{
		// Archive では前方の強いパターンで確定できなければ、
		// タイトル付きでも無しでも取得できるようにする

		// 括弧内の数値
		var parenMatch = RegexParenNumber().Match(normalized);
		if (parenMatch.Success && isValidWeakVolumeNumber(parenMatch.Groups["num"].Value))
		{
			var numStr = parenMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "ParenNumber",
				};
			}
		}

		// 先頭が数字のみ（タイトルなし）
		var leadingMatch = RegexLeadingNumber().Match(normalized);
		if (leadingMatch.Success && isValidWeakVolumeNumber(leadingMatch.Groups["num"].Value))
		{
			var numStr = leadingMatch.Groups["num"].Value.Replace('_', '.');
			if (TryParseVolumeNumber(numStr, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "LeadingNumber",
				};
			}
		}

		// 丸数字
		var circledMatch = RegexCircledNumber().Match(normalized);
		if (circledMatch.Success)
		{
			if (TryParseCircledNumber(circledMatch.Groups["vol"].Value, out var number))
			{
				return new VolumeNumberParseResult
				{
					Kind = VolumeNumberParseKind.Single,
					SingleVolume = number,
					MatchedPattern = "CircledNumber",
				};
			}
		}

		return null;
	}

	/// <summary>
	/// 弱い数値候補が有効か判定します。
	/// 日付・年として誤認される値は除外します。
	/// </summary>
	private static bool isValidWeakVolumeNumber(string numStr)
	{
		if (!decimal.TryParse(numStr, out var num))
		{
			return false;
		}

		// 8桁はYYYYMMDD日付の可能性
		if (numStr.Length == 8 && num >= 10000000 && num <= 99999999)
		{
			return false;
		}

		// 1900～2099 の4桁年を除外
		if (numStr.Length == 4 && num >= 1900 && num <= 2099)
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// 丸数字 ①～⑳ を decimal に変換します。
	/// </summary>
	/// <param name="circledChar">丸数字の文字。</param>
	/// <param name="number">変換結果。</param>
	/// <returns>変換が成功した場合は true。</returns>
	private static bool TryParseCircledNumber(string circledChar, out decimal number)
	{
		number = 0;

		if (string.IsNullOrEmpty(circledChar) || circledChar.Length != 1)
		{
			return false;
		}

		// 丸数字 ①(U+2460) ～ ⑳(U+2473) を decimal に変換
		// ① = 1, ② = 2, ..., ⑳ = 20
		var charCode = (int)circledChar[0];
		if (charCode >= 0x2460 && charCode <= 0x2473)
		{
			number = charCode - 0x2460 + 1;
			return true;
		}

		return false;
	}

	// ---- ローマ数字マッピング ----

	/// <summary>ローマ数字 → decimal の変換テーブル（I～XV 対応）。</summary>
	private static readonly IReadOnlyDictionary<string, decimal> RomanNumeralMap =
		new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
		{
			["I"] = 1,
			["II"] = 2,
			["III"] = 3,
			["IV"] = 4,
			["V"] = 5,
			["VI"] = 6,
			["VII"] = 7,
			["VIII"] = 8,
			["IX"] = 9,
			["X"] = 10,
			["XI"] = 11,
			["XII"] = 12,
			["XIII"] = 13,
			["XIV"] = 14,
			["XV"] = 15,
		};

	// ---- 正規表現パターン ----

	/// <summary>
	/// 「巻ではない」表現（Chapter / Chap / 話 等）。
	/// 大文字小文字を問わず、「Chapter 013」「chapter 013」「Chap 076」「第26話」「26話」等に対応。
	/// </summary>
	[GeneratedRegex(@"(?i)(chapter|chap|raw\s+chapter)\s*\d+|第\d+話|\d+話")]
	private static partial Regex RegexNotVolume();

	/// <summary>
	/// Range パターン：v01-08 / v01-08b / v01-02ss / vol01-06 / vol 01-06 / vol.01-06
	/// Range 末尾の英字サフィックス（s, w, b, ss, 等）は無視します。
	/// </summary>
	[GeneratedRegex(@"(?i)(?:vol[\s._]?|(?<![a-z])v[\s.]?)\s*(?<start>\d+(?:[._]\d+)?)\s*[-]\s*(?<end>\d+(?:[._]\d+)?)(?:\s*[a-z]+)?", RegexOptions.IgnoreCase)]
	private static partial Regex RegexVolumeRange();

	/// <summary>
	/// 日本語パターン：第01巻 / 第1巻 / 第1.5巻 / 01巻 / 1巻 / 1.5巻 / 0巻
	/// </summary>
	[GeneratedRegex(@"(?:第)?(?<num>\d+(?:[._]\d+)?)巻")]
	private static partial Regex RegexJapaneseVolume();

	/// <summary>
	/// vol パターン：vol01 / vol.01 / vol 01 / vol.1 / vol1.5
	/// </summary>
	[GeneratedRegex(@"(?i)vol[\s._]?\s*(?<num>\d+(?:[._]\d+)?)(?![a-z0-9])", RegexOptions.IgnoreCase)]
	private static partial Regex RegexVolPattern();

	/// <summary>
	/// v パターン：v01 / v1 / v1.5 / v01s / v03w（英字サフィックス対応）
	/// 単語の一部を誤検出しないよう、v の直前に非字母字を要求し、
	/// 巻番号直後に英字サフィックスがあってもよい形式。
	/// </summary>
	[GeneratedRegex(@"(?i)(?<![a-z])v\s*(?<num>\d+(?:[._]\d+)?)(?:[a-z]+)?(?![a-z0-9])", RegexOptions.IgnoreCase)]
	private static partial Regex RegexVPattern();

	/// <summary>
	/// 括弧パターン：(1) / (01) / (1.5) / （１） / （１.５）
	/// </summary>
	[GeneratedRegex(@"[（(](?<num>\d+(?:[._]\d+)?)[）)]")]
	private static partial Regex RegexParenPattern();

	/// <summary>
	/// ローマ数字パターン：I～XV
	/// 単語の一部を誤検出しないよう、直前が空白等で直後が空白・記号・末尾を要求。
	/// </summary>
	[GeneratedRegex(@"(?<= )(?<num>XV|XIV|XIII|XII|XI|X|IX|VIII|VII|VI|V|IV|III|II|I)(?=[^\p{L}]|$)")]
	private static partial Regex RegexRomanNumeral();

	/// <summary>
	/// YYYY-MM-DD 形式の日付を除去します。
	/// </summary>
	[GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
	private static partial Regex RegexDateDashFormat();

	/// <summary>
	/// 8桁数字の日付（YYYYMMDD 形式）を除去します。
	/// </summary>
	[GeneratedRegex(@"\d{8}")]
	private static partial Regex RegexDateEightDigits();

	/// <summary>
	/// タイトル末尾付近の単独数字。
	/// 直前が空白または文字列先頭、直後が文字列末尾・空白・[ のいずれか。
	/// </summary>
	[GeneratedRegex(@"(?<!\S)(?<num>\d+(?:[._]\d+)?)(?=[ \[【]|$)")]
	private static partial Regex RegexTitleTrailingNumber();

	/// <summary>
	/// コロン区切り巖数: タイトル末尾：N
	/// </summary>
	[GeneratedRegex(@":(?<num>\d+(?:[._]\d+)?)$")]
	private static partial Regex RegexColonNumber();

	/// <summary>
	/// 「その N」表記。
	/// </summary>
	[GeneratedRegex(@"その(?<num>\d+(?:[._]\d+)?)")]
	private static partial Regex RegexSonoNumber();

	/// <summary>
	/// 残存する角括弧内の数値のみタグ。
	/// </summary>
	[GeneratedRegex(@"\[(?<num>\d+(?:[._]\d+)?)\]")]
	private static partial Regex RegexBracketNumber();

	/// <summary>
	/// 記号直後の巻数（! ? の直後）。
	/// </summary>
	[GeneratedRegex(@"(?<=[!?])(?<num>\d+(?:[._]\d+)?)(?=[^\p{L}\p{N}]|$)")]
	private static partial Regex RegexTitleSeparatorNumber();

	/// <summary>
	/// タイトル + 空白 + 数字 + ( または [
	/// </summary>
	[GeneratedRegex(@"(?<= )(?<num>\d+(?:[._]\d+)?)(?=[\[\(]|$)")]
	private static partial Regex RegexTitleSpaceNumberBeforeParen();

	/// <summary>
	/// 括弧内の数値。
	/// </summary>
	[GeneratedRegex(@"[（(](?<num>\d+(?:[._]\d+)?)[）)]")]
	private static partial Regex RegexParenNumber();

	/// <summary>
	/// 先頭が数字のみ。
	/// </summary>
	[GeneratedRegex(@"^(?<num>\d+(?:[._]\d+)?)")]
	private static partial Regex RegexLeadingNumber();

	/// <summary>
	/// _v パターン：_v01 / _v1 / _v001
	/// </summary>
	[GeneratedRegex(@"(?i)_v(?<num>\d+(?:[._]\d+)?)", RegexOptions.IgnoreCase)]
	private static partial Regex RegexUnderscoreVPattern();

	/// <summary>
	/// 全n巻パターン：全26巻 → Range 1-26 として解釈
	/// </summary>
	[GeneratedRegex(@"全(?<num>\d+(?:[._]\d+)?)巻")]
	private static partial Regex RegexCompleteVolume();

	/// <summary>
	/// vl 範囲パターン：vl 01-07 / vl01-07 / vl.01-07
	/// </summary>
	[GeneratedRegex(@"(?i)vl[\s._]?\s*(?<start>\d+(?:[._]\d+)?)\s*[-~～]\s*(?<end>\d+(?:[._]\d+)?)", RegexOptions.IgnoreCase)]
	private static partial Regex RegexVlRange();

	/// <summary>
	/// vol 余分なハイフンパターン：vol -01-03 のような「vol スペース -」崩れ
	/// </summary>
	[GeneratedRegex(@"(?i)vol\s*-\s*(?<start>\d+(?:[._]\d+)?)\s*[-]\s*(?<end>\d+(?:[._]\d+)?)", RegexOptions.IgnoreCase)]
	private static partial Regex RegexVolRangeLoose();

	/// <summary>
	/// 丸数字 ①～⑳
	/// </summary>
	[GeneratedRegex(@"(?<vol>[①-⑳])")]
	private static partial Regex RegexCircledNumber();
}
