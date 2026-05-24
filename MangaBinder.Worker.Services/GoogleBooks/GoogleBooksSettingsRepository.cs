using MangaBinder.Settings;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books 設定ファイル（JSON）の読み書きを担当するリポジトリクラスです。
/// ファイルが存在しない場合は例外をスローします。
/// </summary>
public class GoogleBooksSettingsRepository
{
	/// <summary>JSON シリアライズオプション。</summary>
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	};

	/// <summary>設定ファイルの絶対パス。</summary>
	private readonly string filePath;

	/// <summary>
	/// <see cref="GoogleBooksSettingsRepository"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="filePath">設定ファイルの絶対パス。</param>
	public GoogleBooksSettingsRepository(string filePath)
		=> this.filePath = filePath;

	/// <summary>
	/// 設定ファイルから <see cref="GoogleBooksSettings"/> を読み込みます。
	/// ファイルが存在しない場合は <see cref="FileNotFoundException"/> をスローします。
	/// </summary>
	/// <param name="excludeWords">DB から取得した除外ワード一覧。</param>
	/// <param name="ct">キャンセルトークン。</param>
	/// <returns>読み込んだ設定インスタンス。</returns>
	/// <exception cref="FileNotFoundException">設定ファイルが見つからない場合。</exception>
	/// <exception cref="JsonException">JSON のデシリアライズに失敗した場合。</exception>
	public async ValueTask<GoogleBooksSettings> LoadAsync(
		IReadOnlyList<string> excludeWords,
		CancellationToken ct = default)
	{
		if (!File.Exists(this.filePath))
			throw new FileNotFoundException(
				$"Google Books 設定ファイルが見つかりません: {this.filePath}{Environment.NewLine}" +
				$"ファイルを配置してから再実行してください。");

		await using var stream = File.OpenRead(this.filePath);
		var json = await JsonSerializer.DeserializeAsync<GoogleBooksSettingsJson>(stream, JsonOptions, ct)
			?? throw new JsonException($"設定ファイルのデシリアライズ結果が null でした: {this.filePath}");

		return new GoogleBooksSettings
		{
			ApiKey           = json.ApiKey,
			TimeoutSeconds   = json.TimeoutSeconds,
			QuotaLimitPerDay = json.QuotaLimitPerDay,
			QuotaDate        = json.QuotaDate,
			CallCount        = json.CallCount,
			ExcludeWords     = excludeWords,
		};
	}

	/// <summary>
	/// <see cref="GoogleBooksSettings"/> の内容を設定ファイルへ保存します。
	/// </summary>
	/// <param name="settings">保存する設定インスタンス。</param>
	/// <param name="ct">キャンセルトークン。</param>
	public async ValueTask SaveAsync(GoogleBooksSettings settings, CancellationToken ct = default)
	{
		var json = new GoogleBooksSettingsJson(
			settings.ApiKey,
			settings.TimeoutSeconds,
			settings.QuotaLimitPerDay,
			settings.QuotaDate,
			settings.CallCount);

		await using var stream = File.Open(this.filePath, FileMode.Create, FileAccess.Write, FileShare.None);
		await JsonSerializer.SerializeAsync(stream, json, JsonOptions, ct);
	}

	/// <summary>JSON ファイルとのマッピング用内部レコードです。</summary>
	private sealed record GoogleBooksSettingsJson(
		[property: JsonPropertyName("apiKey")]           string ApiKey,
		[property: JsonPropertyName("timeoutSeconds")]   int TimeoutSeconds,
		[property: JsonPropertyName("quotaLimitPerDay")] int QuotaLimitPerDay,
		[property: JsonPropertyName("quotaDate")]        string QuotaDate,
		[property: JsonPropertyName("callCount")]        int CallCount)
	{
		/// <summary>JSON デシリアライズ用のデフォルトコンストラクタです。</summary>
		public GoogleBooksSettingsJson() : this(string.Empty, 10, 800, string.Empty, 0) { }
	}
}
