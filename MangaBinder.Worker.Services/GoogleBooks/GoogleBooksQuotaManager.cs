using MangaBinder.Settings;

namespace MangaBinder.Jobs.GoogleBooks;

/// <summary>
/// Google Books API のクォータ（1日あたり呼び出し上限）を管理するクラスです。
/// <see cref="GoogleBooksSettings"/> の <see cref="GoogleBooksSettings.CallCount"/> および
/// <see cref="GoogleBooksSettings.QuotaDate"/> を直接更新します。
/// </summary>
public class GoogleBooksQuotaManager
{
	/// <summary>Google Books 設定。CallCount / QuotaDate を直接更新します。</summary>
	private readonly GoogleBooksSettings settings;

	/// <summary>
	/// <see cref="GoogleBooksQuotaManager"/> の新しいインスタンスを初期化します。
	/// </summary>
	/// <param name="settings">Google Books API 設定。</param>
	public GoogleBooksQuotaManager(GoogleBooksSettings settings)
		=> this.settings = settings;

	/// <summary>
	/// 現在の呼び出し回数を取得します。
	/// </summary>
	public int CallCount => this.settings.CallCount;

	/// <summary>
	/// API 呼び出しが可能かどうかを取得します。
	/// </summary>
	public bool CanCall => this.settings.CallCount < this.settings.QuotaLimitPerDay;

	/// <summary>
	/// QuotaDate が PT(Pacific Time) 基準の本日の日付と異なる場合、CallCount をリセットして QuotaDate を更新します。
	/// </summary>
	public void ResetIfNeeded()
	{
		var pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
		var today = TimeZoneInfo
			.ConvertTimeFromUtc(DateTime.UtcNow, pacific)
			.ToString("yyyy-MM-dd");

		if (this.settings.QuotaDate == today)
			return;

		this.settings.CallCount = 0;
		this.settings.QuotaDate = today;
	}

	/// <summary>
	/// 呼び出し回数を 1 インクリメントします。
	/// </summary>
	public void Increment() => this.settings.CallCount++;

	/// <summary>
	/// 呼び出し回数を指定の値で初期化します。
	/// 「今日は既に N 回使った」状態を Sandbox で再現するために使用します。
	/// </summary>
	/// <param name="initialCount">初期値として設定する呼び出し回数。</param>
	public void InitializeCallCount(int initialCount)
		=> this.settings.CallCount = Math.Max(0, initialCount);
}
