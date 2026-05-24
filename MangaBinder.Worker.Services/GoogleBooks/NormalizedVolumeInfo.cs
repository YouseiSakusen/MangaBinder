namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books API レスポンスを正規化したボリューム情報クラスです。
/// </summary>
public class NormalizedVolumeInfo
{
	/// <summary>正規化後のタイトルを取得します。</summary>
	public string Title { get; init; } = string.Empty;

	/// <summary>著者一覧を取得します。</summary>
	public string[] Authors { get; init; } = Array.Empty<string>();

	/// <summary>出版社を取得します。</summary>
	public string Publisher { get; init; } = string.Empty;

	/// <summary>出版日文字列を取得します。</summary>
	public string PublishedDate { get; init; } = string.Empty;

	/// <summary>解析済み出版日を取得します。</summary>
	public DateTime? ParsedPublishedDate { get; init; }

	/// <summary>あらすじを取得します。</summary>
	public string Description { get; init; } = string.Empty;

	/// <summary>書籍情報 URL を取得します。</summary>
	public string InfoLink { get; init; } = string.Empty;

	/// <summary>カテゴリ一覧を取得します。</summary>
	public string[] Categories { get; init; } = Array.Empty<string>();

	/// <summary>業界識別子（ISBN 等）の一覧を取得します。</summary>
	public string[] IndustryIdentifiers { get; init; } = Array.Empty<string>();

	/// <summary>シリーズ情報を取得します。</summary>
	public GoogleBooksSeriesInfo? SeriesInfo { get; init; }

	/// <summary>シリーズ内の巻番号を取得します。</summary>
	public int? OrderNumber { get; init; }

	/// <summary>変換元の生ボリューム情報を取得します。</summary>
	public GoogleBooksVolumeInfo? RawVolumeInfo { get; init; }
}
