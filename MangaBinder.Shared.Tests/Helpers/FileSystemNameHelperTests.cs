using Xunit;
using MangaBinder.Helpers;

namespace MangaBinder.Tests.Helpers;

/// <summary>
/// FileSystemNameHelper のファイル・フォルダ名解析機能の単体テストです。
/// 製本済みファイル名と素材フォルダ名の解析仕様を検証します。
/// </summary>
public class FileSystemNameHelperTests
{
	/// <summary>
	/// 作者プレフィックスの抽出テスト：
	/// `[作者] タイトル` 形式の作者プレフィックスが正しく抽出されることを確認します。
	/// </summary>
	[Fact]
	public void TryExtractAuthorPrefix_WithAuthorSpaceFormat_ExtractsAuthor()
	{
		// Arrange
		var name = "[作者B] Love Song";

		// Act
		var success = FileSystemNameHelper.TryExtractAuthorPrefix(name, out var author, out var remaining);

		// Assert
		Assert.True(success);
		Assert.Equal("作者B", author);
		Assert.Equal("Love Song", remaining);
	}

	/// <summary>
	/// 作者プレフィックスの抽出テスト：
	/// `[作者]タイトル` 形式（スペースなし）でも作者プレフィックスが抽出されることを確認します。
	/// 既存の製本ファイル互換性を検証。
	/// </summary>
	[Fact]
	public void TryExtractAuthorPrefix_WithAuthorNoSpaceFormat_ExtractsAuthor()
	{
		// Arrange
		var name = "[作者B]Love Song";

		// Act
		var success = FileSystemNameHelper.TryExtractAuthorPrefix(name, out var author, out var remaining);

		// Assert
		Assert.True(success);
		Assert.Equal("作者B", author);
		Assert.Equal("Love Song", remaining);
	}

	/// <summary>
	/// 作者プレフィックスの抽出テスト：
	/// `[作者]    タイトル` 形式（複数スペース）でも作者プレフィックスが抽出されることを確認します。
	/// 既存の製本ファイル互換性を検証。
	/// </summary>
	[Fact]
	public void TryExtractAuthorPrefix_WithMultipleSpaces_ExtractsAuthor()
	{
		// Arrange
		var name = "[作者B]    Love Song";

		// Act
		var success = FileSystemNameHelper.TryExtractAuthorPrefix(name, out var author, out var remaining);

		// Assert
		Assert.True(success);
		Assert.Equal("作者B", author);
		Assert.Equal("Love Song", remaining);
	}

	/// <summary>
	/// 作者プレフィックスがない場合：
	/// author は空文字列、remainingName は元の名前を Trim した値になることを確認します。
	/// </summary>
	[Fact]
	public void TryExtractAuthorPrefix_NoAuthorPrefix_ReturnsEmptyAuthorAndName()
	{
		// Arrange
		var name = "  Love Song  ";

		// Act
		var success = FileSystemNameHelper.TryExtractAuthorPrefix(name, out var author, out var remaining);

		// Assert
		Assert.False(success);
		Assert.Empty(author);
		Assert.Equal("Love Song", remaining);
	}

	/// <summary>
	/// ParseAsMaterial のテスト：
	/// 表記なしの形式 `タイトル` を解析できることを確認します。
	/// </summary>
	[Fact]
	public void ParseAsMaterial_PlainTitle_ParsesCorrectly()
	{
		// Arrange
		var name = "Love Song";

		// Act
		var result = FileSystemNameHelper.ParseAsMaterial(name);

		// Assert
		Assert.Equal("Love Song", result.Title);
		Assert.Empty(result.Author);
		Assert.False(result.SeriesCompleted);
		Assert.False(result.IsOwnedCompleted);
		Assert.Equal(0, result.EndVolume);
	}

	/// <summary>
	/// ParseAsMaterial のテスト：
	/// 括弧なし形式 `タイトル 全5巻` を解析できることを確認します。
	/// </summary>
	[Fact]
	public void ParseAsMaterial_BareVolume_ParsesCorrectly()
	{
		// Arrange
		var name = "Love Song 全5巻";

		// Act
		var result = FileSystemNameHelper.ParseAsMaterial(name);

		// Assert
		Assert.Equal("Love Song", result.Title);
		Assert.Empty(result.Author);
		Assert.True(result.SeriesCompleted);
		Assert.True(result.IsOwnedCompleted);
		Assert.Equal(5, result.EndVolume);
	}

	/// <summary>
	/// ParseAsMaterial のテスト：
	/// 括弧あり形式 `タイトル （全3巻）` を解析できることを確認します。
	/// </summary>
	[Fact]
	public void ParseAsMaterial_ParenVolume_ParsesCorrectly()
	{
		// Arrange
		var name = "Love Song （全3巻）";

		// Act
		var result = FileSystemNameHelper.ParseAsMaterial(name);

		// Assert
		Assert.Equal("Love Song", result.Title);
		Assert.Empty(result.Author);
		Assert.True(result.SeriesCompleted);
		Assert.False(result.IsOwnedCompleted);
		Assert.Equal(3, result.EndVolume);
	}

	/// <summary>
	/// ParseAsMaterial のテスト：
	/// 作者付き形式 `[作者B] タイトル` を解析できることを確認します。
	/// </summary>
	[Fact]
	public void ParseAsMaterial_WithAuthor_ParsesCorrectly()
	{
		// Arrange
		var name = "[作者B] Love Song";

		// Act
		var result = FileSystemNameHelper.ParseAsMaterial(name);

		// Assert
		Assert.Equal("Love Song", result.Title);
		Assert.Equal("作者B", result.Author);
		Assert.False(result.SeriesCompleted);
		Assert.False(result.IsOwnedCompleted);
	}

	/// <summary>
	/// ParseAsMaterial のテスト：
	/// 作者付き + 括弧なし形式 `[作者B] タイトル 全5巻` を解析できることを確認します。
	/// </summary>
	[Fact]
	public void ParseAsMaterial_WithAuthorAndBareVolume_ParsesCorrectly()
	{
		// Arrange
		var name = "[作者B] Love Song 全5巻";

		// Act
		var result = FileSystemNameHelper.ParseAsMaterial(name);

		// Assert
		Assert.Equal("Love Song", result.Title);
		Assert.Equal("作者B", result.Author);
		Assert.True(result.SeriesCompleted);
		Assert.True(result.IsOwnedCompleted);
		Assert.Equal(5, result.EndVolume);
	}

	/// <summary>
	/// ParseAsMaterial のテスト：
	/// 作者付き + 括弧あり形式 `[作者B] タイトル （全3巻）` を解析できることを確認します。
	/// </summary>
	[Fact]
	public void ParseAsMaterial_WithAuthorAndParenVolume_ParsesCorrectly()
	{
		// Arrange
		var name = "[作者B] Love Song （全3巻）";

		// Act
		var result = FileSystemNameHelper.ParseAsMaterial(name);

		// Assert
		Assert.Equal("Love Song", result.Title);
		Assert.Equal("作者B", result.Author);
		Assert.True(result.SeriesCompleted);
		Assert.False(result.IsOwnedCompleted);
		Assert.Equal(3, result.EndVolume);
	}

	/// <summary>
	/// ParseAsBinding のテスト：
	/// 既存の製本ファイル形式が責務移動後も同じ結果になることを確認します。
	/// 簡潔な形式（全巻指定）をテストします。
	/// </summary>
	[Fact]
	public void ParseAsBinding_CompleteFormat_ParsesCorrectly()
	{
		// Arrange
		var filename = "Fate Zero 全10巻.zip";

		// Act
		var result = FileSystemNameHelper.ParseAsBinding(filename);

		// Assert
		Assert.Equal("Fate Zero", result.Title);
		Assert.Empty(result.Author);
		Assert.True(result.SeriesCompleted);
		Assert.Equal(10, result.EndVolume);
	}

	/// <summary>
	/// ParseAsBinding のテスト：
	/// 作者付き製本ファイルが正しく解析されることを確認します。
	/// </summary>
	[Fact]
	public void ParseAsBinding_WithAuthor_ParsesCorrectly()
	{
		// Arrange
		var filename = "[作者A] Fate Zero 全10巻.zip";

		// Act
		var result = FileSystemNameHelper.ParseAsBinding(filename);

		// Assert
		Assert.Equal("Fate Zero", result.Title);
		Assert.Equal("作者A", result.Author);
		Assert.True(result.SeriesCompleted);
		Assert.Equal(10, result.EndVolume);
	}

	/// <summary>
	/// ParseAsBinding のテスト：
	/// 全巻完結を示す形式が正しく解析されることを確認します。
	/// </summary>
	[Fact]
	public void ParseAsBinding_CompleteSeriesFormat_ParsesCorrectly()
	{
		// Arrange
		var filename = "Fate Zero 全10巻.zip";

		// Act
		var result = FileSystemNameHelper.ParseAsBinding(filename);

		// Assert
		Assert.Equal("Fate Zero", result.Title);
		Assert.True(result.SeriesCompleted);
		Assert.Equal(0, result.StartVolume);
		Assert.Equal(10, result.EndVolume);
	}
}
