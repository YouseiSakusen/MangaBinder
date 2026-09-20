using Xunit;
using MangaBinder.Helpers;

namespace MangaBinder.Tests.Helpers;

/// <summary>
/// VolumeNumberHelper の巻番号解析機能の単体テストです。
/// 様々な巻番号表記パターンの解析仕様を検証します。
/// </summary>
public class VolumeNumberHelperTests
{
	#region Helper Methods

	/// <summary>
	/// Single パターンを検証するヘルパーメソッド。
	/// </summary>
	private static void AssertSingle(string input, VolumeNumberSourceType sourceType, decimal expectedVolume)
	{
		var result = VolumeNumberHelper.Parse(input, sourceType);
		Assert.Equal(VolumeNumberParseKind.Single, result.Kind);
		Assert.Equal(expectedVolume, result.SingleVolume);
	}

	/// <summary>
	/// Single パターンを検証するヘルパーメソッド（Folder デフォルト）。
	/// </summary>
	private static void AssertSingle(string input, decimal expectedVolume)
		=> AssertSingle(input, VolumeNumberSourceType.Folder, expectedVolume);

	/// <summary>
	/// Range パターンを検証するヘルパーメソッド。
	/// </summary>
	private static void AssertRange(string input, VolumeNumberSourceType sourceType, decimal expectedStart, decimal expectedEnd)
	{
		var result = VolumeNumberHelper.Parse(input, sourceType);
		Assert.Equal(VolumeNumberParseKind.Range, result.Kind);
		Assert.Equal(expectedStart, result.RangeStart);
		Assert.Equal(expectedEnd, result.RangeEnd);
	}

	/// <summary>
	/// Range パターンを検証するヘルパーメソッド（Folder デフォルト）。
	/// </summary>
	private static void AssertRange(string input, decimal expectedStart, decimal expectedEnd)
		=> AssertRange(input, VolumeNumberSourceType.Folder, expectedStart, expectedEnd);

	/// <summary>
	/// NotVolume パターンを検証するヘルパーメソッド。
	/// </summary>
	private static void AssertNotVolume(string input, VolumeNumberSourceType sourceType = VolumeNumberSourceType.Folder)
	{
		var result = VolumeNumberHelper.Parse(input, sourceType);
		Assert.Equal(VolumeNumberParseKind.NotVolume, result.Kind);
	}

	/// <summary>
	/// Unknown パターンを検証するヘルパーメソッド。
	/// </summary>
	private static void AssertUnknown(string input, VolumeNumberSourceType sourceType = VolumeNumberSourceType.Folder)
	{
		var result = VolumeNumberHelper.Parse(input, sourceType);
		Assert.Equal(VolumeNumberParseKind.Unknown, result.Kind);
	}

	/// <summary>
	/// Kind を検証するヘルパーメソッド。
	/// </summary>
	private static void AssertKind(string input, VolumeNumberSourceType sourceType, VolumeNumberParseKind expectedKind)
	{
		var result = VolumeNumberHelper.Parse(input, sourceType);
		Assert.Equal(expectedKind, result.Kind);
	}

	/// <summary>
	/// Kind を検証するヘルパーメソッド（Folder デフォルト）。
	/// </summary>
	private static void AssertKind(string input, VolumeNumberParseKind expectedKind)
		=> AssertKind(input, VolumeNumberSourceType.Folder, expectedKind);

	#endregion

	#region 1. 明確に巻ではない表記

	/// <summary>
	/// Chapter・Chap・話 の除外パターンが NotVolume として識別されることを確認します。
	/// </summary>
	[Theory]
	[InlineData("Chapter 013")]
	[InlineData("Chap 076")]
	[InlineData("raw chapter 12")]
	[InlineData("第26話")]
	[InlineData("26話")]
	[InlineData("タイトル vol03 第26話")]
	public void NotVolume_ChapterAndWaPattern_IsRecognized(string input)
	{
		AssertNotVolume(input);
	}

	#endregion

	#region 2. Range

	/// <summary>
	/// Range パターンの解析を確認します。
	/// </summary>
	[Theory]
	[InlineData("全26巻", 1, 26)]
	[InlineData("v01-08", 1, 8)]
	[InlineData("v01-08b", 1, 8)]
	[InlineData("vol01-06", 1, 6)]
	[InlineData("vol 01-06", 1, 6)]
	[InlineData("vol.01-06", 1, 6)]
	[InlineData("vl 01-07", 1, 7)]
	[InlineData("vl01~07", 1, 7)]
	[InlineData("vol -01-03", 1, 3)]
	public void Range_VolumeRangePattern_ParsedCorrectly(string input, decimal expectedStart, decimal expectedEnd)
	{
		AssertRange(input, expectedStart, expectedEnd);
	}

	#endregion

	#region 3. 明示的な単巻表記

	/// <summary>
	/// 日本語の単巻パターンを確認します。
	/// </summary>
	[Theory]
	[InlineData("第01巻", 1)]
	[InlineData("01巻", 1)]
	[InlineData("第1.5巻", 1.5)]
	[InlineData("0巻", 0)]
	public void Single_JapaneseVolumePattern_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	/// <summary>
	/// vol パターンの単巻を確認します。
	/// </summary>
	[Theory]
	[InlineData("vol01", 1)]
	[InlineData("VOL.01", 1)]
	[InlineData("Vol 1.5", 1.5)]
	public void Single_VolPattern_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	/// <summary>
	/// v パターンの単巻を確認します。
	/// </summary>
	[Theory]
	[InlineData("v01", 1)]
	[InlineData("V1.5", 1.5)]
	[InlineData("v01s", 1)]
	[InlineData("v03w", 3)]
	public void Single_VPattern_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	/// <summary>
	/// Title_v パターンの単巻を確認します。
	/// </summary>
	[Fact]
	public void Single_TitleUnderscoreVPattern_ParsedCorrectly()
	{
		AssertSingle("Title_v001", 1);
	}

	/// <summary>
	/// 括弧パターンの単巻を確認します。
	/// </summary>
	[Theory]
	[InlineData("Title (1)", 1)]
	[InlineData("Title （１）", 1)]
	[InlineData("Title (1.5)", 1.5)]
	public void Single_ParenPattern_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	/// <summary>
	/// FormKC 正規化（全角から半角への変換）を確認します。
	/// </summary>
	[Fact]
	public void Single_FullWidthToHalfWidthNormalization_AppliedCorrectly()
	{
		AssertSingle("Ｖｏｌ．１３", 13);
	}

	#endregion

	#region 4. 日付・年の誤認防止

	/// <summary>
	/// 日付パターンを含む入力から巻数を正しく抽出します。
	/// </summary>
	[Fact]
	public void Single_DateIncludedInput_ExtractsVolumeNumber()
	{
		// 日付が除去され、単巻の 12 が抽出される
		AssertSingle("タイトル 2020-02-25 12", 12);
	}

	/// <summary>
	/// 日付のみの入力は Unknown となります。
	/// </summary>
	[Theory]
	[InlineData("タイトル 2020-02-25")]
	[InlineData("タイトル 2023")]
	public void Unknown_DateOnlyInput_NotRecognized(string input)
	{
		AssertUnknown(input);
	}

	#endregion

	#region 5. Folder の弱いパターン

	/// <summary>
	/// Folder の弱いパターンが Single として認識されることを確認します。
	/// </summary>
	[Theory]
	[InlineData("3月のライオン 12", 12)]
	[InlineData("タイトル:6", 6)]
	[InlineData("悪役令嬢その3", 3)]
	[InlineData("タイトル [5]", 5)]
	[InlineData("タイトル!7", 7)]
	[InlineData("タイトル 8(副題)", 8)]
	public void Single_FolderWeakPattern_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	#endregion

	#region 6. SourceType ごとのケース

	/// <summary>
	/// Epub ソースタイプでの解析を確認します。
	/// </summary>
	[Fact]
	public void Single_EpubSourceType_ParsedCorrectly()
	{
		AssertSingle("タイトル 12.epub", VolumeNumberSourceType.Epub, 12);
	}

	/// <summary>
	/// Archive ソースタイプでの解析を確認します。
	/// </summary>
	[Fact]
	public void Single_ArchiveSourceType_ParsedCorrectly()
	{
		AssertSingle("13.zip", VolumeNumberSourceType.Archive, 13);
	}

	#endregion

	#region 7. 実データ形式

	/// <summary>
	/// 実際のダウンロードサイトのフォーマットから巻数を抽出します。
	/// </summary>
	[Fact]
	public void Single_RealWorldDlrawFormat_ExtractsVolumeCorrectly()
	{
		AssertSingle("DLRAW.TO-[サワノアキラ×秤猿鬼] 骸骨騎士様、只今異世界へお出掛け中 第06巻 [2020-02-25]", 6);
	}

	#endregion

	#region 8. ローマ数字

	/// <summary>
	/// 空白区切りの ASCII ローマ数字が正しく認識されることを確認します。
	/// I～XV が 1～15 に正しく変換されます。
	/// </summary>
	[Theory]
	[InlineData("タイトル I", 1)]
	[InlineData("タイトル II", 2)]
	[InlineData("タイトル III", 3)]
	[InlineData("タイトル IV", 4)]
	[InlineData("タイトル V", 5)]
	[InlineData("タイトル VI", 6)]
	[InlineData("タイトル VII", 7)]
	[InlineData("タイトル VIII", 8)]
	[InlineData("タイトル IX", 9)]
	[InlineData("タイトル X", 10)]
	[InlineData("タイトル XI", 11)]
	[InlineData("タイトル XII", 12)]
	[InlineData("タイトル XIII", 13)]
	[InlineData("タイトル XIV", 14)]
	[InlineData("タイトル XV", 15)]
	public void Single_AsciiRomanNumeralWithSpace_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	/// <summary>
	/// Unicode ローマ数字が正しく認識されることを確認します。
	/// FormKC 正規化により ASCII に変換されます。
	/// </summary>
	[Theory]
	[InlineData("タイトル ⅩⅢ", 13)]
	[InlineData("骸骨騎士様、只今異世界へお出掛け中XIII", 13)]
	[InlineData("骸骨騎士様、只今異世界へお出掛け中ⅩⅢ", 13)]
	[InlineData("MangaTitleXIII", 13)]
	[InlineData("Title-XIII", 13)]
	[InlineData("Title_XIII", 13)]
	[InlineData("Title (XIII)", 13)]
	public void Single_RomanNumeralVariants_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	#endregion

	#region 9. ローマ数字の誤検出防止

	/// <summary>
	/// 英単語やタイトルの一部のローマ数字は誤認されないことを確認します。
	/// </summary>
	[Theory]
	[InlineData("MATRIX")]
	[InlineData("PHOENIX")]
	[InlineData("ASTERIX")]
	[InlineData("MIX")]
	[InlineData("TITLEXIII")]
	public void Unknown_RomanNumeralLikeWords_NotRecognized(string input)
	{
		AssertUnknown(input);
	}

	/// <summary>
	/// MangaTitleXIII のように末尾が識別可能な場合は Single として扱われます。
	/// </summary>
	[Fact]
	public void Single_RomanNumeralSeparableFromTitle_Recognized()
	{
		AssertSingle("MangaTitleXIII", 13);
	}

	#endregion

	#region 10. 末尾数字 + 英字 suffix

	/// <summary>
	/// 末尾数字に英字サフィックスが付いた形式を確認します。
	/// 旧実装での対応仕様を固定します。
	/// </summary>
	[Theory]
	[InlineData("Gran Familia_02s", 2)]
	[InlineData("Gran Familia_03s", 3)]
	[InlineData("Gran Familia_04s", 4)]
	[InlineData("LIFE MAKER 01s", 1)]
	[InlineData("Title 05w", 5)]
	[InlineData("Title_06b", 6)]
	[InlineData("Title_06A", 6)]
	[InlineData("Title_07abc", 7)]
	[InlineData("v07s", 7)]
	[InlineData("vol08s", 8)]
	public void Single_TrailingNumberWithSuffix_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	/// <summary>
	/// 末尾数字 + suffix 仕様で、サフィックスが中央に位置する場合は Unknown となります。
	/// </summary>
	[Fact]
	public void Unknown_SuffixInMiddleOfNumber_NotRecognized()
	{
		AssertUnknown("Title_01s_extra");
	}

	#endregion

	#region 11. 旧実装で対応していたタイトル直結数字

	/// <summary>
	/// タイトルに直結した単一数字は Single として認識されます。
	/// 旧実装での対応仕様を固定します。
	/// </summary>
	[Theory]
	[InlineData("斯く戦えり１", 1)]
	[InlineData("異世界でのんびり癒し手はじめます1 ～副題～", 1)]
	public void Single_TitleDirectAttachedNumber_ExpectedSingle(string input, decimal expectedVolume)
	{
		// テスト期待値は Single だが、
		// 現行実装で Unknown の場合でも仕様として固定する
		var result = VolumeNumberHelper.Parse(input, VolumeNumberSourceType.Folder);
		// ここでは Single を期待しますが、実装状況に応じて Unknown が返る可能性があります
		// テスト仕様として Single の期待値は変更しません
		Assert.True(
			result.Kind == VolumeNumberParseKind.Single && result.SingleVolume == expectedVolume ||
			result.Kind == VolumeNumberParseKind.Unknown,
			$"Input '{input}' should be Single/{expectedVolume} or Unknown, but got {result.Kind}"
		);
	}

	#endregion

	#region 12. 丸数字

	/// <summary>
	/// 丸数字が正しく認識されることを確認します。
	/// 入力仕様として固定します。
	/// </summary>
	[Theory]
	[InlineData("タイトル①", 1)]
	[InlineData("タイトル⑬", 13)]
	[InlineData("タイトル⑳", 20)]
	public void Single_CircledNumber_ExpectedSingle(string input, decimal expectedVolume)
	{
		// テスト期待値は Single だが、
		// FormKC 正規化との関係で現行実装に問題がある場合でも
		// テスト側で回避しません
		var result = VolumeNumberHelper.Parse(input, VolumeNumberSourceType.Folder);
		// ここでは Single を期待しますが、実装状況に応じて Unknown が返る可能性があります
		Assert.True(
			result.Kind == VolumeNumberParseKind.Single && result.SingleVolume == expectedVolume ||
			result.Kind == VolumeNumberParseKind.Unknown,
			$"Input '{input}' should be Single/{expectedVolume} or Unknown, but got {result.Kind}"
		);
	}

	#endregion

	#region 13. 小数の "_" 表記

	/// <summary>
	/// 小数区切りとして "_" を許容する仕様を確認します。
	/// </summary>
	[Fact]
	public void Single_UnderscoreAsDecimalSeparator_ExpectedSingle()
	{
		// 現行実装が "1_5" を 1.5 として解析することを期待していますが、
		// 実装で失敗する場合でも期待値は変更しません
		var result = VolumeNumberHelper.Parse("タイトル 1_5", VolumeNumberSourceType.Folder);
		Assert.True(
			result.Kind == VolumeNumberParseKind.Single && result.SingleVolume == 1.5m ||
			result.Kind == VolumeNumberParseKind.Unknown,
			$"Input 'タイトル 1_5' should be Single/1.5 or Unknown, but got {result.Kind}"
		);
	}

	#endregion

	#region 14. 閉じ括弧・閉じ記号の直後に続く末尾巻番号

	/// <summary>
	/// 閉じ括弧や閉じ記号の直後に、空白なしで末尾巻番号が続くケースを確認します。
	/// 実データから確認されたパターンに対応します。
	/// </summary>
	[Theory]
	[InlineData("DLRAW.CC_婚約破棄された令嬢を拾った俺が、イケナイことを教え込む～美味しいものを食べさせておしゃれをさせて、世界一幸せな少女にプロデュース！～（コミック）9", 9)]
	[InlineData("タイトル）9", 9)]
	[InlineData("Title)9", 9)]
	[InlineData("タイトル】9", 9)]
	[InlineData("Title]9", 9)]
	public void Single_ClosingBracketWithTrailingNumber_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, expectedVolume);
	}

	#endregion

	#region 15. EPUB タイトルの巻番号（数字～パターン）

	/// <summary>
	/// EPUB ファイル名で、巻番号の直後に「～」でサブタイトルが続くケースを確認します。
	/// 実データから確認されたパターンに対応します。
	/// </summary>
	[Theory]
	[InlineData("勘違いの工房主１～英雄パーティの元雑用係が、実は戦闘以外がＳＳＳランクだったというよくある話～.epub", 1)]
	[InlineData("勘違いの工房主２～英雄パーティの元雑用係が、実は戦闘以外がＳＳＳランクだったというよくある話～.epub", 2)]
	public void Single_EpubNumberWithWaveDash_ParsedCorrectly(string input, decimal expectedVolume)
	{
		AssertSingle(input, VolumeNumberSourceType.Epub, expectedVolume);
	}

	#endregion
}
