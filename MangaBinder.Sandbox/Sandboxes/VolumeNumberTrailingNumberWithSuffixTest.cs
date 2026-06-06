using MangaBinder.Binding.Inspection;

namespace MangaBinder.Sandbox.Sandboxes;

/// <summary>
/// 新規の末尾「数字 + 英字」パターンをテストするクラスです。
/// </summary>
public class VolumeNumberTrailingNumberWithSuffixTest
{
	private readonly VolumeNumberExtractor extractor = new();

	/// <summary>
	/// 末尾「数字 + 英字」パターンの基本的なテスト。
	/// </summary>
	[Fact]
	public void Extract_GranFamiliaPattern_Success()
	{
		var result = extractor.Extract("Gran Familia_02s");
		Assert.True(result.Success, result.Message);
		Assert.Equal(2, result.VolumeNumber);
	}

	/// <summary>
	/// 複数のサフィックスパターンをテスト。
	/// </summary>
	[Fact]
	public void Extract_VariousSuffixes_Success()
	{
		var testCases = new[]
		{
			("Gran Familia_03s", 3),
			("Gran Familia_04s", 4),
			("LIFE MAKER 01s", 1),
			("Title 05w", 5),
			("Title_06b", 6),
			("Title_06A", 6),
			("Title_07abc", 7),
			("v07s", 7),
			("vol08s", 8),
		};

		foreach (var (folderName, expected) in testCases)
		{
			var result = extractor.Extract(folderName);
			Assert.True(result.Success, $"Failed to extract from '{folderName}': {result.Message}");
			Assert.Equal(expected, result.VolumeNumber);
		}
	}

	/// <summary>
	/// 対象外パターン（末尾に英字でなく数字が続く場合）は抽出しないことを確認。
	/// </summary>
	[Fact]
	public void Extract_TrailingWithExtraChars_DoNotMatchTrailingPattern()
	{
		var result = extractor.Extract("Title_01s_extra");

		// TrailingNumberWithSuffix パターンではマッチしないことを確認
		if (result.Success && result.PatternName == "TrailingNumberWithSuffix")
			Assert.Fail($"'Title_01s_extra' should not match TrailingNumberWithSuffix pattern, but matched: {result.Message}");
	}
}
