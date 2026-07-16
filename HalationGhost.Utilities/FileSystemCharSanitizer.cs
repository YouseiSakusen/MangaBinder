namespace HalationGhost.Utilities;

/// <summary>
/// ファイルシステムの禁則文字をサニタイズするユーティリティです。
/// Windowsで使用できない文字を対応する全角文字へ変換します。
/// </summary>
public static class FileSystemCharSanitizer
{
	/// <summary>Windows ファイルシステムの禁則文字を全角文字へマッピングする対応表です。</summary>
	private static readonly Dictionary<char, char> ForbiddenCharMap = new()
	{
		{ '\\', '＼' },
		{ '/', '／' },
		{ ':', '：' },
		{ '*', '＊' },
		{ '?', '？' },
		{ '"', '＂' },
		{ '<', '＜' },
		{ '>', '＞' },
		{ '|', '｜' },
	};

	/// <summary>
	/// 文字列内の Windows ファイルシステムの禁則文字を全角文字へ変換して返します。
	/// </summary>
	/// <param name="value">変換対象の文字列。null または空文字の場合はそのまま返します。</param>
	/// <returns>サニタイズ後の文字列。</returns>
	public static string Sanitize(string value)
	{
		// null または空文字の場合はそのまま返す
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		// 禁則文字が含まれていない場合は元の文字列を返す
		if (!value.Any(c => ForbiddenCharMap.ContainsKey(c)))
		{
			return value;
		}

		// 禁則文字を全角文字に変換
		var result = new System.Text.StringBuilder(value.Length);
		foreach (var c in value)
		{
			if (ForbiddenCharMap.TryGetValue(c, out var sanitized))
			{
				result.Append(sanitized);
			}
			else
			{
				result.Append(c);
			}
		}

		return result.ToString();
	}
}
