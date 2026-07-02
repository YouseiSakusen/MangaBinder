using System.Text.RegularExpressions;

namespace MangaBinder.Jobs.FolderScanners;

/// <summary>
/// 1つのファイル名またはフォルダ名から巻数候補を抽出する static クラスです。
/// </summary>
public static class OwnedVolumeCandidateExtractor
{
    // ---- パターン定義 ----

    /// <summary>vol 範囲: vol 01-06 / vol_01-08 / vol.01-14</summary>
    private static readonly Regex VolRangePattern = new(
        @"(?i)vol[\s_.]\s*\d+\s*[-~～]\s*(?<vol>[０-９\d]+)",
        RegexOptions.Compiled);

    /// <summary>v 範囲: v01-04 / v01_08 / v01～12 / _v01-10 DL-Raw.Se.rar</summary>
    private static readonly Regex VRangePattern = new(
        @"(?i)(?<![a-zA-Z])v[\s.]?\s*\d+\s*[-_~～]\s*(?<vol>[０-９\d]+)",
        RegexOptions.Compiled);

    /// <summary>vol 単巻: vol 08 / vol_08 / vol.14</summary>
    private static readonly Regex VolSinglePattern = new(
        @"(?i)vol[\s_.]\s*(?<vol>[０-９\d]+)",
        RegexOptions.Compiled);

    /// <summary>v 単巻: v08 / v 11 / v.17</summary>
    private static readonly Regex VSinglePattern = new(
        @"(?i)\bv[\s.]?\s*(?<vol>[０-９\d]+)",
        RegexOptions.Compiled);

    /// <summary>第n巻: 第22巻 / 第14巻 / 第０７巻</summary>
    private static readonly Regex JapaneseVolumePattern = new(
        @"第(?<vol>[０-９\d]+)巻",
        RegexOptions.Compiled);

    /// <summary>
    /// 括弧数字: 末尾寄り。
    /// ・括弧数字の直後が文字列末尾
    /// ・括弧数字の直後に拡張子が続く
    /// ・括弧数字の直後に空白 + [ が続く
    /// </summary>
    private static readonly Regex ParenNumberPattern = new(
        @"[（(](?<vol>[０-９\d]{1,4})[）)](?=\s*\[|\.[a-zA-Z0-9]{1,5}$|$)",
        RegexOptions.Compiled);

    /// <summary>EPUB系: タイトル n (出版社...) — 空白+数字+空白+(</summary>
    private static readonly Regex NumberBeforeParenPattern = new(
        @"\s(?<vol>\d{1,4})\s*[（(]",
        RegexOptions.Compiled);

    /// <summary>単純範囲数字: 拡張子直前の nn-nn 形式。例: 01-03.rar → 3</summary>
    private static readonly Regex SimpleRangePattern = new(
        @"\d+[-]\s*(?<vol>[０-９\d]+)\.[a-zA-Z0-9]{1,5}$",
        RegexOptions.Compiled);

    /// <summary>n巻: 単純な「数字+巻」。例: ２巻.epub / 1巻.epub</summary>
    private static readonly Regex KanjiVolumeSuffixPattern = new(
        @"(?<vol>[０-９\d]+)巻",
        RegexOptions.Compiled);

    /// <summary>括弧なし単独数字+拡張子: 例: １.epub / 001.epub</summary>
    private static readonly Regex NumberBeforeExtensionPattern = new(
        @"(?<![０-９\d])(?<vol>[０-９\d]+)\.[a-zA-Z0-9]{1,5}$",
        RegexOptions.Compiled);

    /// <summary>タイトル + 数字 + [: 例: 31番目のお妃様 ６ [B's-LOG COMICS] / 解雇ですか？２ [PASH!...]</summary>
    private static readonly Regex NumberBeforeBracketPattern = new(
        @"(?<![０-９\d])(?<vol>[０-９\d]{1,4})\s+\[",
        RegexOptions.Compiled);

    /// <summary>v範囲+末尾サフィックス: 例: v01-02s / v01-03w (_v 形式も対応)</summary>
    private static readonly Regex VRangeSuffixPattern = new(
        @"(?i)(?<![a-zA-Z])v[\s.]?\s*\d+\s*[-]\s*(?<vol>[０-９\d]+)[a-z]",
        RegexOptions.Compiled);

    /// <summary>vl 範囲: 例: vl 01-07</summary>
    private static readonly Regex VlRangePattern = new(
        @"(?i)vl[\s_.]\s*\d+\s*[-~～]\s*(?<vol>[０-９\d]+)",
        RegexOptions.Compiled);

    /// <summary>vol 後ろに余分な "-" が入る崩れ: 例: vol -01-03 → 3</summary>
    private static readonly Regex VolRangeLoosePattern = new(
        @"(?i)vol\s*-\s*\d+\s*[-]\s*(?<vol>[０-９\d]+)",
        RegexOptions.Compiled);

    /// <summary>全n巻: 例: 全26巻</summary>
    private static readonly Regex CompleteVolumePattern = new(
        @"全(?<vol>[０-９\d]+)巻",
        RegexOptions.Compiled);

    /// <summary>数字 + 【宣伝文句】: 例: 1【イラスト特典付】.epub / 4【電子限定描き下ろし付き】.epub</summary>
    private static readonly Regex NumberBeforePromoBracketPattern = new(
        @"(?<![０-９\d])(?<vol>[０-９\d]{1,4})【",
        RegexOptions.Compiled);

    /// <summary>全角括弧数字 + 空白 + -: 例: 彼女（３） - 作者.epub</summary>
    private static readonly Regex FullWidthParenVolumePattern = new(
        @"（(?<vol>[０-９\d]{1,4})）\s*-",
        RegexOptions.Compiled);

    /// <summary>数字 + 空白 + -: 例: こういうのがいい 12 - 双龍.epub</summary>
    private static readonly Regex NumberBeforeAuthorPattern = new(
        @"\s(?<vol>[０-９\d]{1,4})\s+-",
        RegexOptions.Compiled);

    /// <summary>半角括弧数字 + 空白 + -: 例: (4) - 中乃空.epub</summary>
    private static readonly Regex HalfWidthParenVolumePattern = new(
        @"\((?<vol>\d{1,4})\)\s*-",
        RegexOptions.Compiled);

    /// <summary>_v 単巻: _v01 / _v1 / _v001</summary>
    private static readonly Regex VSingleUnderscorePattern = new(
        @"(?i)_v(?<vol>[０-９\d]+)",
        RegexOptions.Compiled);

    /// <summary>単純範囲（拡張子・末尾不要）: 例: 001-107 文字列末尾</summary>
    private static readonly Regex SimpleRangeLoosePattern = new(
        @"(?<!\d)\d+[-](?<vol>\d+)$",
        RegexOptions.Compiled);

    /// <summary>数字 + 空白 + (または～（緩め）: 例: 2 (ノヴァコミックス) / ７ (ポルカコミ) / 7　～</summary>
    private static readonly Regex NumberBeforeParenLoosePattern = new(
        @"(?<![０-９\d])(?<vol>[０-９\d]{1,4})(?:[\s　]+[(（～])",
        RegexOptions.Compiled);

    /// <summary>全角括弧数字（緩め）: 例: 【合本版】音色が愛しむ资娘（１）</summary>
    private static readonly Regex FullWidthParenLoosePattern = new(
        @"（(?<vol>[０-９\d]{1,4})）",
        RegexOptions.Compiled);

    /// <summary>丸数字 ①～⑳ (①=1、⑳=20)</summary>
    private static readonly Regex CircledNumberPattern = new(
        @"(?<vol>[①-⑳])",
        RegexOptions.Compiled);

    /// <summary>ローマ数字（限定: I～XV）: 例: XIII → 13</summary>
    private static readonly Regex RomanNumeralPattern = new(
        @"(?<![A-Za-z])(?<vol>X(?:IV|V?I{0,3})|V?I{1,3}|IV)(?![A-Za-z])",
        RegexOptions.Compiled);

    /// <summary>
    /// 1つのファイル名またはフォルダ名から巻数候補をすべて抽出します。
    /// </summary>
    /// <param name="name">ファイル名またはフォルダ名。</param>
    /// <returns>抽出された候補一覧。候補がなければ空配列。</returns>
    public static IReadOnlyList<OwnedVolumeEstimateCandidate> Extract(string name)
    {
        var results = new List<OwnedVolumeEstimateCandidate>();

        // 高優先度：v/vol 系の範囲パターン（末尾寄りの明確な巻数表現）
        results.AddRange(extractByPattern(name, VolRangePattern, "VolRange", "vol", 11));
        results.AddRange(extractByPattern(name, VRangePattern, "VRange", "vol", 11));
        results.AddRange(extractByPattern(name, VlRangePattern, "VlRange", "vol", 11));
        results.AddRange(extractByPattern(name, VRangeSuffixPattern, "VRangeSuffix", "vol", 11));
        results.AddRange(extractByPattern(name, SimpleRangePattern, "SimpleRange", "vol", 11));
        results.AddRange(extractByPattern(name, VolRangeLoosePattern, "VolRangeLoose", "vol", 11));

        // 高優先度：括弧末尾パターン（末尾寄り、拡張子直前など明確）
        results.AddRange(extractByPattern(name, ParenNumberPattern, "ParenNumber", "vol", 10));

        // 高優先度：日本語の「第n巻」「全n巻」
        results.AddRange(extractByPattern(name, CompleteVolumePattern, "CompleteVolume", "vol", 10));
        results.AddRange(extractByPattern(name, JapaneseVolumePattern, "JapaneseVolume", "vol", 10));

        // 中優先度：v/vol 単巻パターン
        results.AddRange(extractByPattern(name, VolSinglePattern, "VolSingle", "vol", 8));
        results.AddRange(extractByPattern(name, VSinglePattern, "VSingle", "vol", 8));
        results.AddRange(extractByPattern(name, VSingleUnderscorePattern, "VSingleUnderscore", "vol", 8));

        // 中優先度：その他の括弧パターン
        results.AddRange(extractByPattern(name, FullWidthParenVolumePattern, "FullWidthParenVolume", "vol", 8));
        results.AddRange(extractByPattern(name, HalfWidthParenVolumePattern, "HalfWidthParenVolume", "vol", 8));

        // 中優先度：タイトル + 数字 + [
        results.AddRange(extractByPattern(name, NumberBeforeBracketPattern, "NumberBeforeBracket", "vol", 7));
        results.AddRange(extractByPattern(name, NumberBeforePromoBracketPattern, "NumberBeforePromoBracket", "vol", 7));

        // 低優先度：数字 + 空白 + -（著者名区切り）
        results.AddRange(extractByPattern(name, NumberBeforeAuthorPattern, "NumberBeforeAuthor", "vol", 3));

        // 低優先度：緩い括弧パターン（タイトル中の数字を拾いやすい）
        results.AddRange(extractByPattern(name, NumberBeforeParenLoosePattern, "NumberBeforeParenLoose", "vol", 2));
        results.AddRange(extractByPattern(name, FullWidthParenLoosePattern, "FullWidthParenLoose", "vol", 2));

        // 中優先度：EPUB系の明確な括弧パターン（数字 + 括弧で直後が拡張子）
        results.AddRange(extractByPattern(name, NumberBeforeParenPattern, "NumberBeforeParen", "vol", 8));

        // 低優先度：丸数字（ほかにネタがない場合に参照）
        results.AddRange(extractByPattern(name, CircledNumberPattern, "CircledNumber", "vol", 4));

        // 中優先度：拡張子直前の単独数字（末尾寄り）
        results.AddRange(extractByPattern(name, NumberBeforeExtensionPattern, "NumberBeforeExtension", "vol", 9));

        // 低優先度：単純な範囲パターン（拡張子・末尾判定なし）
        results.AddRange(extractByPattern(name, SimpleRangeLoosePattern, "SimpleRangeLoose", "vol", 2));

        // 中優先度：n巻パターン
        results.AddRange(extractByPattern(name, KanjiVolumeSuffixPattern, "KanjiVolumeSuffix", "vol", 6));

        // ローマ数字は、通常の数字系候補が1件も取得できなかった場合のみ評価
        // これにより、「v01-07」で７を取得した場合に「X」の誤検出を避ける
        if (results.Count == 0)
        {
            results.AddRange(extractRomanNumerals(name));
        }

        return results;
    }

    // ---- private ヘルパー ----
    private static IReadOnlyList<OwnedVolumeEstimateCandidate> extractByPattern(
        string name, Regex regex, string patternName, string groupName, int priority)
    {
        var list = new List<OwnedVolumeEstimateCandidate>();
        foreach (Match m in regex.Matches(name))
        {
            var raw = m.Groups[groupName].Value;
            if (isDateLike(raw))
                continue;

            var volume = parseVolumeNumber(raw);
            if (volume <= 0)
                continue;

            // 年っぽい4桁 (1900–2099) は除外
            if (raw.Length == 4 && volume >= 1900 && volume <= 2099)
                continue;

            list.Add(new OwnedVolumeEstimateCandidate
            {
                Name = name,
                Volume = volume,
                PatternName = patternName,
                Priority = priority,
            });
        }
        return list;
    }

    /// <summary>
    /// 数字文字列が 8桁日付相当かどうかを判定します。
    /// </summary>
    private static bool isDateLike(string value)
    {
        // 全角 → 半角変換後の数字列が8桁なら日付と見なす
        var halfWidth = toHalfWidth(value);
        return halfWidth.Length == 8 && halfWidth.All(char.IsDigit);
    }

    /// <summary>
    /// 全角数字を含む文字列を int に変換します。丸数字 ①～⑳ にも対応します。
    /// </summary>
    private static int parseVolumeNumber(string value)
    {
        // 丸数字 ①～⑳ (U+2460～U+2473)
        if (value.Length == 1 && value[0] is >= '\u2460' and <= '\u2473')
            return value[0] - '\u2460' + 1;

        var halfWidth = toHalfWidth(value);
        return int.TryParse(halfWidth, out var result) ? result : 0;
    }

    /// <summary>
    /// ローマ数字（限定: I～XV）を抽出して候補リストを返します。
    /// </summary>
    private static IReadOnlyList<OwnedVolumeEstimateCandidate> extractRomanNumerals(string name)
    {
        var list = new List<OwnedVolumeEstimateCandidate>();
        foreach (Match m in RomanNumeralPattern.Matches(name))
        {
            var roman = m.Groups["vol"].Value;
            var volume = tryParseRomanNumeral(roman);
            if (volume is null or <= 0)
                continue;

            list.Add(new OwnedVolumeEstimateCandidate
            {
                Name = name,
                Volume = volume.Value,
                PatternName = "RomanNumeral",
                Priority = 5,
            });
        }
        return list;
    }

    /// <summary>
    /// ローマ数字文字列（I～XV）を int に変換します。変換不能な場合は null を返します。
    /// </summary>
    private static int? tryParseRomanNumeral(string value) => value.ToUpperInvariant() switch
    {
        "I"    => 1,
        "II"   => 2,
        "III"  => 3,
        "IV"   => 4,
        "V"    => 5,
        "VI"   => 6,
        "VII"  => 7,
        "VIII" => 8,
        "IX"   => 9,
        "X"    => 10,
        "XI"   => 11,
        "XII"  => 12,
        "XIII" => 13,
        "XIV"  => 14,
        "XV"   => 15,
        _      => null,
    };

    /// <summary>
    /// 全角英数字・記号 (U+FF01–U+FF5E) を半角に変換します。
    /// </summary>
    private static string toHalfWidth(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var chars = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            chars[i] = c is >= '\uFF01' and <= '\uFF5E' ? (char)(c - 0xFEE0) : c;
        }
        return new string(chars);
    }
}
