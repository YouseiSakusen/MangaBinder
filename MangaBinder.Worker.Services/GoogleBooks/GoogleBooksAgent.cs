using MangaBinder.Settings;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using ZLogger;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books API の呼び出しを担当するクラスです。
/// </summary>
public class GoogleBooksAgent
{
	/// <summary>Google Books API の ベース URL です。</summary>
	private const string VolumesBaseUrl = "https://www.googleapis.com/books/v1/volumes";

	/// <summary>HttpClient。</summary>
	private readonly HttpClient httpClient;

	/// <summary>Google Books API 設定。</summary>
	private GoogleBooksSettings settings;

	/// <summary>ロガー。</summary>
	private readonly ILogger<GoogleBooksAgent> logger;

	/// <summary>
	/// <see cref="GoogleBooksAgent"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="httpClient">HttpClient。</param>
	/// <param name="logger">ロガー。</param>
	public GoogleBooksAgent(
		HttpClient httpClient,
		ILogger<GoogleBooksAgent> logger)
	{
		this.httpClient = httpClient;
		this.settings = null!;
		this.logger = logger;
	}

	/// <summary>
	/// Job 実行開始時に設定を更新します。
	/// </summary>
	/// <param name="newSettings">JSON から読み込んだ設定。</param>
	public void ApplySettings(GoogleBooksSettings newSettings)
	{
		this.settings = newSettings;
		this.httpClient.Timeout = TimeSpan.FromSeconds(newSettings.TimeoutSeconds);
	}

	/// <summary>
	/// Google Books API volumes エンドポイントを非同期で呼び出し、生 JSON 文字列と DTO の両方を返します。
	/// HTTP リクエストは 1 回のみ行います。
	/// </summary>
	/// <param name="query">検索クエリ文字列。</param>
	/// <param name="startIndex">取得開始インデックス。</param>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>生 JSON と DTO を含む <see cref="GoogleBooksSearchResult"/>。取得失敗時は両プロパティが null。</returns>
	public async ValueTask<GoogleBooksSearchResult> SearchWithRawAsync(
		string query,
		int startIndex,
		CancellationToken ct)
	{
		var url = this.buildUrl(query, startIndex);
		this.logger.ZLogDebug($"Google Books API 呼び出し (raw+DTO): {url}");

		try
		{
			var rawJson = await this.httpClient.GetStringAsync(url, ct);
			var response = JsonSerializer.Deserialize<GoogleBooksResponse>(rawJson);
			return new GoogleBooksSearchResult(response, rawJson);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			this.logger.ZLogError(ex, $"Google Books API 呼び出しに失敗しました。query={query}");
			return new GoogleBooksSearchResult(null, null);
		}
	}

	/// <summary>
	/// Google Books API volumes エンドポイントを非同期で呼び出し、生 JSON 文字列を返します。
	/// DTO へのデシリアライズは行いません。
	/// </summary>
	/// <param name="query">検索クエリ文字列。</param>
	/// <param name="startIndex">取得開始インデックス。</param>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>生 JSON 文字列。取得失敗時は null。</returns>
	public async ValueTask<string?> SearchRawAsync(
		string query,
		int startIndex,
		CancellationToken ct)
	{
		var url = this.buildUrl(query, startIndex);
		this.logger.ZLogDebug($"Google Books API 生JSON呼び出し: {url}");

		try
		{
			return await this.httpClient.GetStringAsync(url, ct);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			this.logger.ZLogError(ex, $"Google Books API 生JSON呼び出しに失敗しました。query={query}");
			return null;
		}
	}

	/// <summary>
	/// Google Books API volumes エンドポイントを非同期で呼び出し、レスポンスを返します。
	/// </summary>
	/// <param name="query">検索クエリ文字列。</param>
	/// <param name="startIndex">取得開始インデックス。</param>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>レスポンス DTO。取得失敗時は null。</returns>
	public async ValueTask<GoogleBooksResponse?> SearchAsync(
		string query,
		int startIndex,
		CancellationToken ct)
	{
		var url = this.buildUrl(query, startIndex);
		this.logger.ZLogDebug($"Google Books API 呼び出し: {url}");

		try
		{
			var response = await this.httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, ct);
			return response;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			this.logger.ZLogError(ex, $"Google Books API 呼び出しに失敗しました。query={query}");
			return null;
		}
	}

	/// <summary>
	/// 検索用 URL を構築します。
	/// </summary>
	/// <param name="query">検索クエリ文字列。</param>
	/// <param name="startIndex">取得開始インデックス。</param>
	/// <returns>構築済み URL 文字列。</returns>
	private string buildUrl(string query, int startIndex)
	{
		var queryParams = HttpUtility.ParseQueryString(string.Empty);
		queryParams["q"] = query;
		queryParams["startIndex"] = startIndex.ToString();
		queryParams["maxResults"] = "40";
		queryParams["printType"] = "books";
		queryParams["langRestrict"] = "ja";

		if (!string.IsNullOrEmpty(this.settings.ApiKey))
			queryParams["key"] = this.settings.ApiKey;

		return $"{VolumesBaseUrl}?{queryParams}";
	}
}
