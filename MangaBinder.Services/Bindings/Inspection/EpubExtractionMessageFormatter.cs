namespace MangaBinder.Bindings.Inspection;

/// <summary>
/// EPUB 展開結果のエラーと警告を UI 表示用の日本語文字列へ変換する Formatter です。
/// </summary>
public static class EpubExtractionMessageFormatter
{
	/// <summary>
	/// EPUB 展開エラーを UI 表示用の文字列へ変換します。
	/// </summary>
	/// <param name="error">変換対象のエラー。</param>
	/// <param name="detailMessage">UnexpectedError の場合の詳細メッセージ（省略可能）。</param>
	/// <returns>UI 表示用のエラー文字列。</returns>
	/// <exception cref="ArgumentOutOfRangeException">定義されていないエラー値が渡された場合。</exception>
	public static string FormatError(
		EpubExtractionError error,
		string? detailMessage = null)
	{
		return error switch
		{
			EpubExtractionError.None => string.Empty,
			EpubExtractionError.InvalidArchive => "EPUBファイルを展開できません。",
			EpubExtractionError.InvalidXml => "EPUB内のXMLが不正です。",
			EpubExtractionError.IoError => "EPUBの読み込み中にI/Oエラーが発生しました。",
			EpubExtractionError.AccessDenied => "EPUBファイルへアクセスできません。",
			EpubExtractionError.BodyImageNotFound => "EPUB内に本文画像が見つかりません。",
			EpubExtractionError.UnexpectedError =>
				string.IsNullOrWhiteSpace(detailMessage)
					? "EPUBの処理中に予期しないエラーが発生しました。"
					: $"EPUBの処理中に予期しないエラーが発生しました：{detailMessage}",
			_ => throw new ArgumentOutOfRangeException(
				nameof(error),
				error,
				$"対応していない EpubExtractionError 値です：{error}"),
		};
	}

	/// <summary>
	/// EPUB 展開の警告フラグを UI 表示用の文字列リストへ変換します。
	/// </summary>
	/// <param name="warnings">変換対象の警告フラグ。</param>
	/// <returns>UI 表示用の警告文字列一覧。</returns>
	/// <exception cref="ArgumentOutOfRangeException">定義されていない警告ビットが含まれる場合。</exception>
	public static IReadOnlyList<string> FormatWarnings(EpubExtractionWarning warnings)
	{
		if (warnings == EpubExtractionWarning.None)
		{
			return [];
		}

		// 既知のすべてのWarningフラグをORしたマスク
		const EpubExtractionWarning knownMask =
			EpubExtractionWarning.CoverImageNotFound
			| EpubExtractionWarning.ManifestItemNotFound
			| EpubExtractionWarning.XhtmlFileNotFound
			| EpubExtractionWarning.ReferencedImageFileNotFound
			| EpubExtractionWarning.UnsupportedImageFormat
			| EpubExtractionWarning.DuplicateImageRemoved;

		// 未知のビットが含まれているかチェック
		if ((warnings & ~knownMask) != 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(warnings),
				warnings,
				$"対応していない EpubExtractionWarning ビットが含まれています：{warnings}");
		}

		var result = new List<string>();

		// 固定順で明示的にビット判定
		if ((warnings & EpubExtractionWarning.CoverImageNotFound) != 0)
		{
			result.Add("カバー画像を特定できませんでした");
		}

		if ((warnings & EpubExtractionWarning.ManifestItemNotFound) != 0)
		{
			result.Add("EPUB本文の一部を特定できませんでした");
		}

		if ((warnings & EpubExtractionWarning.XhtmlFileNotFound) != 0)
		{
			result.Add("本文ファイルの一部が見つかりません");
		}

		if ((warnings & EpubExtractionWarning.ReferencedImageFileNotFound) != 0)
		{
			result.Add("参照画像の一部が見つかりません");
		}

		if ((warnings & EpubExtractionWarning.UnsupportedImageFormat) != 0)
		{
			result.Add("未対応形式の画像を除外しました");
		}

		if ((warnings & EpubExtractionWarning.DuplicateImageRemoved) != 0)
		{
			result.Add("重複画像を除外しました");
		}

		return result.AsReadOnly();
	}
}
