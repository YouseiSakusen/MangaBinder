using Xunit;
using MangaBinder.Helpers;

namespace MangaBinder.Sandboxes;

/// <summary>
/// MangaTitleHelper.GetShortTitle() の仕様確認用 Sandbox テストです。
/// ShortTitle がファイル名として安全にサニタイズ済みであることを確認します。
/// </summary>
public class MangaTitleHelperShortTitleSandboxTest
{
	/// <summary>
	/// 通常タイトル（30文字以下、禁則文字なし）は従来と同じ ShortTitle になることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_NormalTitle_ReturnsTitleAsIs()
	{
		// Arrange
		var title = "進撃の巨人";
		var separatorChars = "";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		Assert.Equal("進撃の巨人", result);
	}

	/// <summary>
	/// ファイル名禁則文字を含むタイトル（30文字以下）は、
	/// サニタイズされることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_TitleWithForbiddenCharsUnder30_ReturnsSanitized()
	{
		// Arrange
		var title = "テスト: 試験";  // 11文字、30文字以下、コロン（禁則文字）
		var separatorChars = "";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// 30文字以下なのでそのまま候補 → サニタイズ
		// ":" → "：" に変換
		Assert.Equal("テスト： 試験", result);
	}

	/// <summary>
	/// separatorChars で分割できた場合、その先頭要素がサニタイズされることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_TitleWithSeparator_ReturnsSanitized()
	{
		// Arrange
		var title = "Part1: Very Long Extension That Goes Way Beyond Thirty Characters";
		var separatorChars = ":";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// separatorChars ":" で分割 → "Part1" (5文字) が先頭要素 → サニタイズ
		// "Part1" に禁則文字がないのでそのまま
		Assert.Equal("Part1", result);
	}

	/// <summary>
	/// separatorChars での分割結果が30文字超の場合、スペース分割へ進むことを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_LongPartFallsToSpace_ReturnsSanitized()
	{
		// Arrange
		// separatorChars "|" で分割すると "Very Long Prefix That Has No Space Split" (30字超)
		// その場合スペース分割が実行される
		var title = "Very Long Prefix That Has No Space Split | Remainder";
		var separatorChars = "|";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// separatorChars "|" で分割 → "Very Long Prefix That Has No Space Split " が先頭要素
		// 30文字超なので、スペース分割へ → "Very" が先頭要素
		Assert.Equal("Very", result);
	}

	/// <summary>
	/// スペース分割後の先頭要素も30文字超の場合、先頭30文字で短縮されることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_NoSeparatorMatches_ReturnsTruncatedTo30Chars()
	{
		// Arrange
		var title = "123456789012345678901234567890123456789";  // 39文字、スペースなし、separatorChars も含まない
		var separatorChars = "";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// スペースがない → 先頭30文字で短縮
		Assert.Equal("123456789012345678901234567890", result);
	}

	/// <summary>
	/// 先頭30文字短縮後、末尾のスペースが Trim() で除去されることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_Substring30WithTrailingSpace_Trimmed()
	{
		// Arrange
		// 先頭30文字が " " で終わる
		var title = "123456789012345678901234567890 Extra Part";  // 30文字 + " " + "Extra Part"
		var separatorChars = "";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// スペース分割 → "123456789012345678901234567890" (30文字) が先頭要素
		Assert.Equal("123456789012345678901234567890", result);
	}

	/// <summary>
	/// 全角スペースと半角スペース両方で分割されることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_FullwidthAndHalfwidthSpace_SplitBoth()
	{
		// Arrange
		// 全角スペース U+3000 を含む、30文字超のタイトル
		var title = "Short　Text This Title Is Very Very Long And Has Many Words";  // 全角スペース、30文字超
		var separatorChars = "";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// 全角スペースで分割 → "Short" (5文字) が先頭要素
		Assert.Equal("Short", result);
	}

	/// <summary>
	/// 30文字超の長いタイトルで、スペース分割可能な場合、スペース分割の先頭要素
	/// がサニタイズされることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_LongTitleSpaceSeparatedWithForbiddenChars_ReturnsSanitized()
	{
		// Arrange
		var title = "Title? Other: Content That Goes Very Long";  // スペースと禁則文字を含む、30文字超
		var separatorChars = "";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// スペース分割 → "Title?" (6文字) が先頭要素 → サニタイズ
		// "?" → "？" に変換
		Assert.Equal("Title？", result);
	}

	/// <summary>
	/// 複数の禁則文字を含む長いタイトルで、先頭30文字短縮とサニタイズが同時に行われることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_LongTitleNoSpaceWithForbiddenChars_ReturnsTruncatedAndSanitized()
	{
		// Arrange
		// スペースがなく、禁則文字を含む、30文字超
		var title = "Title*With?Invalid:Chars<And>More|Content";
		var separatorChars = "";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// スペースがない → 先頭30文字で短縮 → サニタイズ
		var substring30 = title[..30].Trim();
		var expected = HalationGhost.Utilities.FileSystemCharSanitizer.Sanitize(substring30);
		Assert.Equal(expected, result);
	}

	/// <summary>
	/// 30文字ちょうどのタイトルで禁則文字を含む場合、サニタイズされることを確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_Exactly30CharsWithForbiddenChars_ReturnsSanitized()
	{
		// Arrange
		var title = "12345678901234567: Test|Value";  // 30文字ちょうど、禁則文字含む
		var separatorChars = "";

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// 30文字以下なのでそのまま使用 → サニタイズ
		var expected = HalationGhost.Utilities.FileSystemCharSanitizer.Sanitize(title);
		Assert.Equal(expected, result);
	}

	/// <summary>
	/// separator が複数文字指定された場合で、30文字超の場合、
	/// separatorChars の分割結果がさらに30文字超の場合の処理を確認します。
	/// </summary>
	[Fact]
	public void GetShortTitle_MultiCharSeparator_SplitsByAny()
	{
		// Arrange
		// separator で分割すると長い文が返される場合
		var title = "Part1 Very Long Part That Has Space And Exceeds Thirty Chars:Rest";
		var separatorChars = ":|";  // : または | で分割

		// Act
		var result = MangaTitleHelper.GetShortTitle(title, separatorChars);

		// Assert
		// separatorChars で分割（":" で） → "Part1 Very Long Part That Has Space And Exceeds Thirty Chars" が先頭要素（30字超）
		// 30字超なので、スペース分割へ → "Part1" が先頭要素
		Assert.Equal("Part1", result);
	}
}
