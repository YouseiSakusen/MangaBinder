namespace MangaBinder.Settings;

/// <summary>
/// Google Books API に関する設定値と Quota 状態を保持するクラスです。
/// </summary>
public sealed class GoogleBooksSettings
{
	/// <summary>Google Books API キーを取得または設定します。</summary>
	public string ApiKey { get; set; } = string.Empty;

	/// <summary>API リクエストのタイムアウト秒数を取得または設定します。</summary>
	public int TimeoutSeconds { get; set; } = 10;

	/// <summary>1日あたりのクォータ上限を取得または設定します。</summary>
	public int QuotaLimitPerDay { get; set; } = 800;

	/// <summary>クォータカウントを記録した日付文字列（yyyy-MM-dd 形式、PT: Pacific Time 基準）を取得または設定します。</summary>
	public string QuotaDate { get; set; } = string.Empty;

	/// <summary>当日の API 呼び出し回数を取得または設定します。</summary>
	public int CallCount { get; set; }

	/// <summary>Google Books 検索結果の除外ワード一覧を取得または設定します。</summary>
	public IReadOnlyList<string> ExcludeWords { get; set; } = Array.Empty<string>();
}
