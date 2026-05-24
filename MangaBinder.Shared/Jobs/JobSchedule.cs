namespace MangaBinder.Jobs;

/// <summary>
/// JobSchedules テーブルの 1 レコードを表すモデルクラスです。
/// </summary>
public class JobSchedule
{
    /// <summary>主キー。</summary>
    public long Id { get; set; }

    /// <summary>ジョブの種別。</summary>
    public JobType JobType { get; set; }

    /// <summary>スケジュールが有効かどうか。</summary>
    public bool Enabled { get; set; }

    /// <summary>スケジュールの実行サイクル。</summary>
    public JobScheduleType ScheduleType { get; set; }

    /// <summary>週次実行時の曜日（0=日曜〜6=土曜）。</summary>
    public int DayOfWeek { get; set; }

    /// <summary>実行時刻（"HH:mm" 形式）。</summary>
    public string TimeOfDay { get; set; } = string.Empty;

    /// <summary>間隔実行時のインターバル（分）。</summary>
    public int IntervalMinutes { get; set; }

    /// <summary>次回実行予定日時（"yyyy-MM-dd HH:mm:ss" 形式）。</summary>
    public string NextRunAt { get; set; } = string.Empty;

    /// <summary>最後にキューへ登録した日時（"yyyy-MM-dd HH:mm:ss" 形式）。</summary>
    public string LastQueuedAt { get; set; } = string.Empty;

    /// <summary>レコード作成日時（"yyyy-MM-dd HH:mm:ss" 形式）。</summary>
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>レコード更新日時（"yyyy-MM-dd HH:mm:ss" 形式）。</summary>
    public string UpdatedAt { get; set; } = string.Empty;
}
