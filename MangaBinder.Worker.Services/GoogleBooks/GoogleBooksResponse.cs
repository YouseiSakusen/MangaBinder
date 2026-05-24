using System.Text.Json;
using System.Text.Json.Serialization;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books API 検索結果を raw JSON と DTO の両方で保持するレコードです。
/// </summary>
/// <param name="Response">デシリアライズ済みレスポンス DTO。取得失敗時は null。</param>
/// <param name="RawJson">生 JSON 文字列。取得失敗時は null。</param>
public sealed record GoogleBooksSearchResult(
	GoogleBooksResponse? Response,
	string? RawJson);

/// <summary>
/// Google Books API volumes エンドポイントのレスポンス DTO です。
/// </summary>
public sealed class GoogleBooksResponse
{
	/// <summary>検索ヒット総件数を取得または設定します。</summary>
	[JsonPropertyName("totalItems")]
	public int TotalItems { get; set; }

	/// <summary>書籍アイテムの一覧を取得または設定します。</summary>
	[JsonPropertyName("items")]
	public List<GoogleBooksItem>? Items { get; set; }
}

/// <summary>
/// Google Books API の書籍アイテム DTO です。
/// </summary>
public sealed class GoogleBooksItem
{
	/// <summary>書誌情報を取得または設定します。</summary>
	[JsonPropertyName("volumeInfo")]
	public GoogleBooksVolumeInfo? VolumeInfo { get; set; }
}

/// <summary>
/// Google Books API の書誌情報 DTO です。
/// </summary>
public sealed class GoogleBooksVolumeInfo
{
	/// <summary>タイトルを取得または設定します。</summary>
	[JsonPropertyName("title")]
	public string Title { get; set; } = string.Empty;

	/// <summary>著者一覧を取得または設定します。</summary>
	[JsonPropertyName("authors")]
	public List<string>? Authors { get; set; }

	/// <summary>出版社を取得または設定します。</summary>
	[JsonPropertyName("publisher")]
	public string? Publisher { get; set; }

	/// <summary>出版日を取得または設定します。</summary>
	[JsonPropertyName("publishedDate")]
	public string? PublishedDate { get; set; }

	/// <summary>あらすじを取得または設定します。</summary>
	[JsonPropertyName("description")]
	public string? Description { get; set; }

	/// <summary>カテゴリ一覧を取得または設定します。</summary>
	[JsonPropertyName("categories")]
	public List<string>? Categories { get; set; }

	/// <summary>書籍情報 URL を取得または設定します。</summary>
	[JsonPropertyName("infoLink")]
	public string? InfoLink { get; set; }

	/// <summary>画像リンク情報を取得または設定します。</summary>
	[JsonPropertyName("imageLinks")]
	public GoogleBooksImageLinks? ImageLinks { get; set; }

	/// <summary>業界識別子（ISBN 等）の一覧を取得または設定します。</summary>
	[JsonPropertyName("industryIdentifiers")]
	public List<GoogleBooksIndustryIdentifier>? IndustryIdentifiers { get; set; }

	/// <summary>シリーズ情報を取得または設定します。</summary>
	[JsonPropertyName("seriesInfo")]
	public GoogleBooksSeriesInfo? SeriesInfo { get; set; }
}

/// <summary>
/// Google Books API の画像リンク情報 DTO です。
/// </summary>
public sealed class GoogleBooksImageLinks
{
	/// <summary>サムネイル画像 URL を取得または設定します。</summary>
	[JsonPropertyName("thumbnail")]
	public string? Thumbnail { get; set; }
}

/// <summary>
/// Google Books API の業界識別子 DTO です。
/// </summary>
public sealed class GoogleBooksIndustryIdentifier
{
	/// <summary>識別子の種別（ISBN_10 / ISBN_13 等）を取得または設定します。</summary>
	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	/// <summary>識別子の値を取得または設定します。</summary>
	[JsonPropertyName("identifier")]
	public string Identifier { get; set; } = string.Empty;
}

/// <summary>
/// Google Books API のシリーズ情報 DTO です。
/// </summary>
public sealed class GoogleBooksSeriesInfo
{
	/// <summary>シリーズタイトルを取得または設定します。</summary>
	[JsonPropertyName("bookDisplayNumber")]
	public string? BookDisplayNumber { get; set; }

	/// <summary>シリーズ書籍情報を取得または設定します。</summary>
	[JsonPropertyName("volumeSeries")]
	public List<GoogleBooksVolumeSeries>? VolumeSeries { get; set; }

	/// <summary>JSON 未定義プロパティを拡張データとして取得または設定します。</summary>
	[JsonExtensionData]
	public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Google Books API のシリーズ巻情報 DTO です。
/// </summary>
public sealed class GoogleBooksVolumeSeries
{
	/// <summary>シリーズ UID を取得または設定します。</summary>
	[JsonPropertyName("seriesId")]
	public string? SeriesId { get; set; }

	/// <summary>シリーズタイトルを取得または設定します。</summary>
	[JsonPropertyName("seriesBookType")]
	public string? SeriesBookType { get; set; }

	/// <summary>シリーズ内の巻番号を取得または設定します。</summary>
	[JsonPropertyName("orderNumber")]
	public double? OrderNumber { get; set; }
}
