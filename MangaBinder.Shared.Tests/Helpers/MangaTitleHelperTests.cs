using Xunit;
using MangaBinder.Helpers;

namespace MangaBinder.Tests.Helpers;

/// <summary>
/// MangaTitleHelper.NormalizeTitleInternal() の正規化仕様の単体テストです。
/// 機械的な表記差の吸収（空白除去、英字大小文字統一、波ダッシュ統一）を検証します。
/// </summary>
public class MangaTitleHelperTests
{
	/// <summary>
	/// A. 空白の吸収：タイトル内部のすべての空白が除去されることを確認します。
	/// </summary>
	[Fact]
	public void NormalizeTitleInternal_WhitespaceRemoval_RemovesAllSpaces()
	{
		// Arrange
		var title1 = "FATE / ZERO";
		var title2 = "FATE/ZERO";
		var title3 = "FATE　/　ZERO";  // 全角スペース

		// Act
		var result1 = MangaTitleHelper.NormalizeTitleInternal(title1);
		var result2 = MangaTitleHelper.NormalizeTitleInternal(title2);
		var result3 = MangaTitleHelper.NormalizeTitleInternal(title3);

		// Assert - すべて同じ正規化結果
		Assert.Equal("FATE/ZERO", result1);
		Assert.Equal("FATE/ZERO", result2);
		Assert.Equal("FATE/ZERO", result3);
		Assert.Equal(result1, result2);
		Assert.Equal(result2, result3);
	}

	/// <summary>
	/// B. 英字大小文字＋全角ASCII＋空白の複合ケース：
	/// 複数の正規化ルールが組み合わさった場合、結果が統一されることを確認します。
	/// </summary>
	[Fact]
	public void NormalizeTitleInternal_CombinedCaseFullwidthAndSpace_UnifiesResult()
	{
		// Arrange
		var title1 = "Fate/Zero";           // 小文字・半角
		var title2 = "fate／zero";          // 小文字・全角スラッシュ
		var title3 = "FATE / ZERO";         // 大文字・スペース
		var title4 = "Ｆａｔｅ／Ｚｅｒｏ";     // 全角英字・全角スラッシュ

		// Act
		var result1 = MangaTitleHelper.NormalizeTitleInternal(title1);
		var result2 = MangaTitleHelper.NormalizeTitleInternal(title2);
		var result3 = MangaTitleHelper.NormalizeTitleInternal(title3);
		var result4 = MangaTitleHelper.NormalizeTitleInternal(title4);

		// Assert - すべて同じ結果
		var expected = "FATE/ZERO";
		Assert.Equal(expected, result1);
		Assert.Equal(expected, result2);
		Assert.Equal(expected, result3);
		Assert.Equal(expected, result4);
	}

	/// <summary>
	/// C. 波ダッシュ3種類の統一：
	/// U+007E (ASCII TILDE) / U+301C (WAVE DASH) / U+FF5E (FULLWIDTH TILDE)
	/// がすべて同一に正規化されることを確認します。
	/// </summary>
	[Fact]
	public void NormalizeTitleInternal_WaveDashUnification_UnifiesAllVariants()
	{
		// Arrange
		var title1 = "ドラゴンボール~パフパフ~";    // U+007E ASCII TILDE
		var title2 = "ドラゴンボール〜パフパフ〜";   // U+301C WAVE DASH
		var title3 = "ドラゴンボール～パフパフ～";   // U+FF5E FULLWIDTH TILDE

		// Act
		var result1 = MangaTitleHelper.NormalizeTitleInternal(title1);
		var result2 = MangaTitleHelper.NormalizeTitleInternal(title2);
		var result3 = MangaTitleHelper.NormalizeTitleInternal(title3);

		// Assert - すべて同じ結果 (U+301C に統一)
		Assert.Equal(result1, result2);
		Assert.Equal(result2, result3);
		// 正規化結果が U+301C を含むことを確認
		Assert.Contains('\u301C', result1);
	}

	/// <summary>
	/// D. 情報差は吸収しない：
	/// 末尾の文字が異なる場合、異なる正規化結果になることを確認します。
	/// </summary>
	[Fact]
	public void NormalizeTitleInternal_InformationDifference_DoesNotAbsorb()
	{
		// Arrange
		var title1 = "ドラゴンボール～パフパフ～";
		var title2 = "ドラゴンボール～パフパフ";   // 末尾の～が無い

		// Act
		var result1 = MangaTitleHelper.NormalizeTitleInternal(title1);
		var result2 = MangaTitleHelper.NormalizeTitleInternal(title2);

		// Assert - 異なる正規化結果
		Assert.NotEqual(result1, result2);
	}

	/// <summary>
	/// E. NFC正規化の既存仕様：
	/// NFD (濁点分離) と NFC (合成済み) が同じ文字を表現した場合、
	/// 同一の正規化結果になることを確認します。
	/// </summary>
	[Fact]
	public void NormalizeTitleInternal_NFCNormalization_UnifiesDecomposedAndComposed()
	{
		// Arrange
		// 同じタイトルをNFDとNFCで表現
		var composed = "ガンダム";  // 合成済み (NFC)
		var decomposed = "ガンダム".Normalize(System.Text.NormalizationForm.FormD);  // 分解形 (NFD)

		// Act
		var resultComposed = MangaTitleHelper.NormalizeTitleInternal(composed);
		var resultDecomposed = MangaTitleHelper.NormalizeTitleInternal(decomposed);

		// Assert - 同じ正規化結果
		Assert.Equal(resultComposed, resultDecomposed);
	}

	/// <summary>
	/// F. 空白のみで構成された文字列：
	/// 空白だけの文字列を正規化した場合、空文字列になることを確認します。
	/// </summary>
	[Fact]
	public void NormalizeTitleInternal_WhitespaceOnly_ReturnsEmptyString()
	{
		// Arrange
		var title1 = "   ";       // 半角スペースのみ
		var title2 = "　　　";    // 全角スペースのみ
		var title3 = " 　 ";      // 混在

		// Act
		var result1 = MangaTitleHelper.NormalizeTitleInternal(title1);
		var result2 = MangaTitleHelper.NormalizeTitleInternal(title2);
		var result3 = MangaTitleHelper.NormalizeTitleInternal(title3);

		// Assert - 空文字列
		Assert.Empty(result1);
		Assert.Empty(result2);
		Assert.Empty(result3);
	}

	/// <summary>
	/// 英字大小文字が統一されることの追加確認：
	/// 小文字のみのタイトルが大文字に統一されることを確認します。
	/// </summary>
	[Fact]
	public void NormalizeTitleInternal_LowercaseLetters_UnifiesUppercase()
	{
		// Arrange
		var title = "attack on titan";

		// Act
		var result = MangaTitleHelper.NormalizeTitleInternal(title);

		// Assert
		Assert.Equal("ATTACKONTITAN", result);
	}

	/// <summary>
	/// 複雑なケース：複数の波ダッシュと空白が含まれる場合
	/// </summary>
	[Fact]
	public void NormalizeTitleInternal_ComplexCase_MultiWaveDashAndSpaces()
	{
		// Arrange
		var title = "Fate ~ Zero 〜 Part II～End";

		// Act
		var result = MangaTitleHelper.NormalizeTitleInternal(title);

		// Assert
		// 空白が全て除去、英字が大文字化、波ダッシュが U+301C に統一
		Assert.Equal("FATE〜ZERO〜PARTII〜END", result);
		// 波ダッシュが複数含まれることを確認
		Assert.Equal(3, result.Count(c => c == '\u301C'));
	}
}
