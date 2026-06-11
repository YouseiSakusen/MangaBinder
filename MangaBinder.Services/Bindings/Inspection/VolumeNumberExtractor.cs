using System.Text;
using System.Text.RegularExpressions;

namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// フォルダ名から巻番号を抽出するクラスです。
/// 完全対応ではなく、実データ観測用として設計されています。
/// </summary>
public sealed partial class VolumeNumberExtractor
{
	/// <summary>
	/// フォルダ名から巻番号を抽出します。
	/// </summary>
	/// <param name="folderName">抽出対象のフォルダ名。</param>
	/// <returns>抽出結果。</returns>
	public VolumeNumberExtractResult Extract(string folderName)
	{
		// FormKC 正規化 + 先頭/末尾ブラケット除去した抽出用文字列を作成
		var target = NormalizeTarget(folderName);

		foreach (var (pattern, name) in Patterns)
		{
			var match = pattern.Match(target);
			if (!match.Success)
				continue;

			var raw = match.Groups["num"].Value.Replace('_', '.');
			if (!TryParseVolumeNumber(raw, out var number))
				continue;

			return new VolumeNumberExtractResult
			{
				SourceName = folderName,
				Success = true,
				VolumeNumber = number,
				PatternName = name,
				Message = $"マッチ: '{match.Value}', Target='{target}'",
			};
		}

		return new VolumeNumberExtractResult
		{
			SourceName = folderName,
			Success = false,
			Message = $"いずれのパターンにもマッチしませんでした。Target='{target}'",
		};
	}

	/// <summary>
	/// 巻番号文字列を <see cref="decimal"/> へ変換します。
	/// 通常の数値と、ローマ数字（I〜X）の両方に対応します。
	/// </summary>
	private static bool TryParseVolumeNumber(string raw, out decimal number)
	{
		if (decimal.TryParse(raw, out number))
			return true;

		if (RomanNumeralMap.TryGetValue(raw.ToUpperInvariant(), out var roman))
		{
			number = roman;
			return true;
		}

		return false;
	}

	/// <summary>ローマ数字 → 整数の変換テーブルです（I〜X 対応）。</summary>
	private static readonly IReadOnlyDictionary<string, int> RomanNumeralMap =
		new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			["I"]    = 1,
			["II"]   = 2,
			["III"]  = 3,
			["IV"]   = 4,
			["V"]    = 5,
			["VI"]   = 6,
			["VII"]  = 7,
			["VIII"] = 8,
			["IX"]   = 9,
			["X"]    = 10,
		};

	/// <summary>
	/// フォルダ名をパターン適用前に正規化します。
	/// <list type="bullet">
	/// <item>Unicode FormKC 正規化（全角数字・全角括弧・全角英字 → 半角）</item>
	/// <item>先頭に連続する <c>[]</c> ブラケット群を除去</item>
	/// <item>末尾に連続する <c>[]</c> ブラケット群を除去</item>
	/// <item>【】などの全角隅付き括弧はそのまま保持</item>
	/// </list>
	/// </summary>
	/// <param name="source">元のフォルダ名。</param>
	/// <returns>正規化後の文字列。</returns>
	private static string NormalizeTarget(string source)
	{
		// FormKC: 全角数字→半角, 全角括弧→半角, 全角英字→半角 など
		var normalized = source.Normalize(NormalizationForm.FormKC);

		// 先頭の連続する [xxx] を除去（スペースを挟んでいても除去）
		var trimmed = RegexLeadingBrackets().Replace(normalized, string.Empty);

		// 末尾の連続する非数値 [xxx] を除去（[5] のような数値のみタグは保持）
		trimmed = RegexTrailingBrackets().Replace(trimmed, string.Empty);

		return trimmed.Trim();
	}

	/// <summary>
	/// 適用するパターンの一覧です。優先度順に並べています。
	/// </summary>
	private static readonly IReadOnlyList<(Regex Pattern, string Name)> Patterns =
	[
		// 第01巻 / 第1巻 / 第1.5巻
		(RegexJpVolumeWithPrefix(), "第N巻"),
		// 01巻 / 1巻 / 1.5巻 / 0巻
		(RegexJpVolume(), "N巻"),
		// vol.01 / Vol.1 / VOL.01
		(RegexVolDot(), "vol.N"),
		// vol01 / Vol01 / VOL1
		(RegexVolNoDot(), "volN"),
		// v01 / v1 / v1.5
		(RegexVLower(), "vN"),
		// 拡張子直前の巻番号: 「タイトル 1.epub」「タイトル 35.cbz」など
		(RegexNumberBeforeExtension(), "ExtSuffix"),
		// (1) / (01) / (1.5) ― 括弧内が数値のみ
		(RegexParenNumber(), "(N)"),
		// タイトル末尾：1 ― 悪夢から目覚めた傲慢令嬢：1
		(RegexColonNumber(), "ColonNumber"),
		// その1 ― 悪役令嬢レベル99 その1
		(RegexSonoNumber(), "SonoNumber"),
		// [5] ― 残存する角括弧内の数値のみ
		(RegexBracketNumber(), "BracketNumber"),
		// 末尾 I〜X ― 変態公爵による困った溺愛結婚生活 I
		(RegexRomanNumber(), "RomanNumber"),
		// タイトル末尾付近の単独数字: 「推しの子】 1」「COMIC 3」など（空白区切り）
		(RegexTitleTrailingNumber(), "TitleTrailingNumber"),
		// 記号直後の巻数: 「ですって!1 ～」など
		(RegexTitleSeparatorNumber(), "TitleSeparatorNumber"),
		// タイトル途中に巻数が直結し、その後に副題区切りが続く: 「はじめます1 ～副題～」
		(RegexTitleEmbeddedSeparatorNumber(), "TitleEmbeddedSeparatorNumber"),
		// タイトル + 空白 + 数字 + (の形式: 「プロローグから 1(※...」「旗を叩き折る 8(アリアン」
		(RegexTitleSpaceNumberBeforeParen(), "TitleSpaceNumberBeforeParen"),
		// タイトル末尾に数字が直結: 「斯く戦えり１」→「斯く戦えり1」(FormKC後)
		(RegexTitleAttachedTrailingNumber(), "TitleAttachedTrailingNumber"),
		// 末尾「数字 + 英字サフィックス」: Gran Familia_02s → 2 / Title 05w → 5
		(RegexTrailingNumberWithSuffix(), "TrailingNumberWithSuffix"),
		// 先頭が数字のみ: "01" "1" "1.5" など
		(RegexLeadingNumber(), "数字のみ"),
	];

	// ---- ブラケット除去用 ----

	/// <summary>文字列先頭の連続する [xxx] 群（間のスペース含む）にマッチします。</summary>
	[GeneratedRegex(@"^(\[[^\[\]]*\]\s*)+")]
	private static partial Regex RegexLeadingBrackets();

	/// <summary>
	/// 文字列末尾の連続する非数値 [xxx] 群（間のスペース含む）にマッチします。
	/// <c>[5]</c> や <c>[1.5]</c> のような数値のみのタグは除去対象外です。
	/// </summary>
	[GeneratedRegex(@"(\s*\[(?!\d+(?:[._]\d+)?\])[^\[\]]*\])+$")]
	private static partial Regex RegexTrailingBrackets();

	// ---- 巻番号抽出パターン ----

	[GeneratedRegex(@"第(?<num>\d+(?:[._]\d+)?)巻", RegexOptions.IgnoreCase)]
	private static partial Regex RegexJpVolumeWithPrefix();

	[GeneratedRegex(@"(?<![.\d])(?<num>\d+(?:[._]\d+)?)巻", RegexOptions.IgnoreCase)]
	private static partial Regex RegexJpVolume();

	[GeneratedRegex(@"[Vv][Oo][Ll]\.(?<num>\d+(?:[._]\d+)?)", RegexOptions.IgnoreCase)]
	private static partial Regex RegexVolDot();

	[GeneratedRegex(@"[Vv][Oo][Ll](?<num>\d+(?:[._]\d+)?)", RegexOptions.IgnoreCase)]
	private static partial Regex RegexVolNoDot();

	[GeneratedRegex(@"(?<![A-Za-z])v(?:ol)?(?<num>\d+(?:[._]\d+)?)s?(?![A-Za-z0-9])", RegexOptions.IgnoreCase)]
	private static partial Regex RegexVLower();

	/// <summary>
	/// ファイル名拡張子の直前にある巻番号。
	/// 例: 「タイトル 1.epub」「タイトル 35.cbz」「タイトル1.zip」
	/// 直後が「.英数字2〜5文字＋文字列末尾」であるものを対象とします。
	/// 「v01.zip」「vol01.zip」は v/vol パターンが先にマッチするため除外されます。
	/// </summary>
	[GeneratedRegex(@"(?<!\d)(?<num>\d+(?:[._]\d+)?)(?=\.[A-Za-z0-9]{2,5}$)")]
	private static partial Regex RegexNumberBeforeExtension();

	/// <summary>
	/// 丸括弧巻数: (1) / (01) / (1.5) ― 括弧内が数値のみ。
	/// </summary>
	[GeneratedRegex(@"\((?<num>\d+(?:[._]\d+)?)\)")]
	private static partial Regex RegexParenNumber();

	/// <summary>
	/// タイトル末尾付近の単独数字。
	/// 直前が空白または文字列先頭、直後が文字列末尾・空白・[ のいずれか。
	/// 直後に日本語文字（回・話・年・巻以外を含む一般文字）が来る場合は除外。
	/// </summary>
	[GeneratedRegex(@"(?<!\S)(?<num>\d+(?:[._]\d+)?)(?=[ \[【]|$)")]
	private static partial Regex RegexTitleTrailingNumber();

	/// <summary>
	/// 記号直後の巻数: 「ですって!1 ～...」など。
	/// 直前が ! ? ！ ？ のいずれか、直後が空白・記号・～ など（日本語文字は除外）。
	/// </summary>
	[GeneratedRegex(@"(?<=[!?])(?<num>\d+(?:[._]\d+)?)(?=[^\p{L}\p{N}]|$)")]
	private static partial Regex RegexTitleSeparatorNumber();

	/// <summary>
	/// タイトル途中に巻数が直結し、その後に副題区切り文字が続くパターン。
	/// 例: 「異世界でのんびり癒し手はじめます1 ～毒にも～」
	/// 直前が非数字・非空白（タイトル文字に直結）、直後が空白＋区切りまたは区切り文字。
	/// 「99回」「レベル99」のような中間数値は区切り文字が続かないため除外されます。
	/// </summary>
	[GeneratedRegex(@"(?<=[^\d\s])(?<num>\d+(?:[._]\d+)?)(?=\s*[\u301c~\u2015\-\uff1a:（(])")]
	private static partial Regex RegexTitleEmbeddedSeparatorNumber();

	/// <summary>
	/// 空白 + 数字 + ( の形式。
	/// FormKC 正規化後に半角化された ( を直後に持つ巻数を対象とします。
	/// 例: 「プロローグから 1(※ただし...)」「旗を叩き折る 8(アリアン...)」
	/// </summary>
	[GeneratedRegex(@"(?<= )(?<num>\d+(?:[._]\d+)?)(?=\()")]
	private static partial Regex RegexTitleSpaceNumberBeforeParen();

	/// <summary>
	/// コロン区切り巻数: タイトル末尾：1 ― FormKC 後に ： → : となったものを対象とします。
	/// </summary>
	[GeneratedRegex(@":(?<num>\d+(?:[._]\d+)?)$")]
	private static partial Regex RegexColonNumber();

	/// <summary>
	/// 「その N」表記: 「その」の直後の数値のみを対象とします。
	/// 「レベル99」のような中間数値は除外されます。
	/// </summary>
	[GeneratedRegex(@"その(?<num>\d+(?:[._]\d+)?)")]
	private static partial Regex RegexSonoNumber();

	/// <summary>
	/// 残存する角括弧内の数値のみタグ: [5] など。
	/// NormalizeTarget 後に残っている [数値] を対象とします。
	/// </summary>
	[GeneratedRegex(@"\[(?<num>\d+(?:[._]\d+)?)\]")]
	private static partial Regex RegexBracketNumber();

	/// <summary>
	/// 末尾付近の独立したローマ数字 (I〜X)。
	/// 直前が空白、直後が文字列末尾・空白・記号のいずれか。
	/// 英単語の一部を誤検出しないよう直前後の文字を厳密にチェックします。
	/// </summary>
	[GeneratedRegex(@"(?<= )(?<num>VIII|VII|VI|IV|IX|III|II|I|V|X)(?=[^\p{L}]|$)")]
	private static partial Regex RegexRomanNumber();

	/// <summary>
	/// タイトル末尾に数字が直結しているパターン。
	/// FormKC 正規化後に全角数字が半角化された末尾の数値を対象とします。
	/// 直後が文字列末尾のみを対象とし、「回」「話」「年」「日」などの日本語単位は除外します。
	/// </summary>
	[GeneratedRegex(@"(?<=[^\d\s])(?<num>\d+(?:[._]\d+)?)$")]
	private static partial Regex RegexTitleAttachedTrailingNumber();

	/// <summary>
	/// 末尾「数字 + 英字サフィックス」パターン。
	/// 例: Gran Familia_02s → 2 / Title 05w → 5 / v07s → 7
	/// 数字の直後に英字のみが続き文字列末尾に到達するパターンを対象とします。
	/// 英字サフィックスの意味は解釈しません。
	/// </summary>
	[GeneratedRegex(@"(?<num>\d+(?:[._]\d+)?)(?=[A-Za-z]+$)")]
	private static partial Regex RegexTrailingNumberWithSuffix();

	[GeneratedRegex(@"^(?<num>\d+(?:[._]\d+)?)")]
	private static partial Regex RegexLeadingNumber();
}
